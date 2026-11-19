"""NVIDIA NIM (Nemotron) adapter - OpenAI-compatible chat completions.

Adapted from hyunolike/reroute-irops-agent (apps/api/app/providers/llm/nim.py): GovernedHttpClient removed;
kept: capped jittered retry honouring Retry-After, text tool-call parsing, <think> splitting.
"""

from __future__ import annotations

import asyncio
import json
import random
import re
import time
from collections.abc import Awaitable, Callable
from typing import Any

import httpx

from app.providers.llm.base import LLMError, LLMProvider, LLMResponse, OutputInvalid, RateLimited, ToolCall
from app.schemas import ProviderInfo

_THINK = re.compile(r"<think>(.*?)</think>", re.S)
_TEXT_CALLS = [
    re.compile(r"<TOOLCALL>\s*(.*?)\s*</TOOLCALL>", re.S),
    re.compile(r"<tool_call>\s*(.*?)\s*</tool_call>", re.S),
]
_RETRY_STATUS = {408, 409, 425, 429, 500, 502, 503, 504}


def _retry_after(r: httpx.Response) -> float:
    try:
        return max(0.0, float(r.headers.get("retry-after", 0)))
    except ValueError:
        return 0.0


def split_thinking(content: str | None) -> tuple[str | None, str | None]:
    if not content:
        return content, None
    thoughts = _THINK.findall(content)
    visible = _THINK.sub("", content)
    if "</think>" in visible:  # opening tag consumed by the chat template
        head, _, visible = visible.partition("</think>")
        thoughts.insert(0, head)
    visible = visible.strip()
    return (visible or None), ("\n".join(t.strip() for t in thoughts) or None)


def _as_calls(obj: Any) -> list[dict[str, Any]]:
    items = obj if isinstance(obj, list) else [obj]
    calls = []
    for it in items:
        if not isinstance(it, dict):
            continue
        fn = it.get("function") if isinstance(it.get("function"), dict) else it
        name = fn.get("name")
        args = fn.get("arguments", fn.get("parameters", {}))
        if isinstance(args, str):
            try:
                args = json.loads(args or "{}")
            except json.JSONDecodeError:
                continue
        if isinstance(name, str) and isinstance(args, dict):
            calls.append({"name": name, "arguments": args})
    return calls


def parse_text_tool_calls(content: str | None, tool_names: set[str]) -> tuple[list[ToolCall], str | None]:
    """Extract tool calls a model wrote into `content` instead of `tool_calls`."""
    if not content or not tool_names:
        return [], content
    found: list[dict[str, Any]] = []
    rest = content
    for pattern in _TEXT_CALLS:
        for block in pattern.findall(content):
            for piece in [block, *re.findall(r"\{.*\}", block, re.S)]:
                try:
                    found += _as_calls(json.loads(piece))
                    break
                except json.JSONDecodeError:
                    continue
        rest = pattern.sub("", rest)
    if not found:
        stripped = content.strip().removeprefix("```json").removeprefix("```").removesuffix("```").strip()
        if stripped[:1] in "[{":
            try:
                found = _as_calls(json.loads(stripped))
                rest = ""
            except json.JSONDecodeError:
                found = []
    calls = [
        ToolCall(id=f"text_call_{i}", name=c["name"], arguments=c["arguments"], raw_arguments=json.dumps(c["arguments"]))
        for i, c in enumerate(found)
        if c["name"] in tool_names
    ]
    return calls, (rest.strip() or None) if calls else content


class NvidiaNimProvider(LLMProvider):
    info = ProviderInfo(name="nvidia-nim", kind="nvidia")

    def __init__(
        self,
        api_key: str,
        base_url: str,
        model: str,
        *,
        temperature: float = 0.0,
        enable_thinking: bool = False,
        max_retries: int = 4,
        timeout: float = 60.0,
        transport: httpx.AsyncBaseTransport | None = None,
        sleep: Callable[[float], Awaitable[None]] = asyncio.sleep,
        clock: Callable[[], float] = time.monotonic,
        budget_s: float = 90.0,
        attempt_timeout_s: float = 30.0,
    ) -> None:
        if not api_key:
            raise LLMError("NVIDIA_API_KEY is required", code="LLM_NO_KEY")
        self.api_key = api_key
        self.base_url = base_url.rstrip("/")
        self.model = model
        self.temperature = temperature
        self.enable_thinking = enable_thinking
        self.max_retries = max_retries
        self.timeout = timeout
        self.transport = transport
        self.sleep = sleep
        self.clock = clock
        self.budget_s = budget_s
        self.attempt_timeout_s = attempt_timeout_s
        self.retry_base_delay = 1.0
        self.retry_max_delay = 8.0

    def _prepare(self, messages: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], dict[str, Any]]:
        extra: dict[str, Any] = {}
        msgs = [dict(m) for m in messages]
        if "nemotron-3" in self.model:
            extra["chat_template_kwargs"] = {"enable_thinking": self.enable_thinking}
        elif "nemotron" in self.model and msgs and msgs[0]["role"] == "system":
            tag = "/think" if self.enable_thinking else "/no_think"
            msgs[0]["content"] = f"{tag}\n{msgs[0]['content']}"
        return msgs, extra

    async def _post(self, body: dict[str, Any], deadline: float) -> httpx.Response:
        backoff = self.retry_base_delay
        last_429: float | None = None
        async with httpx.AsyncClient(transport=self.transport) as client:
            for attempt in range(self.max_retries + 1):
                remaining = deadline - self.clock()
                if remaining <= 0:
                    break
                server_wait = 0.0
                try:
                    r = await client.post(
                        f"{self.base_url}/chat/completions",
                        headers={"Authorization": f"Bearer {self.api_key}", "Accept": "application/json"},
                        json=body,
                        timeout=min(self.attempt_timeout_s, remaining),
                    )
                except httpx.TransportError as e:
                    last_429 = None
                    if attempt == self.max_retries:
                        raise LLMError(f"NIM unreachable: {e}", code="LLM_UNREACHABLE") from e
                else:
                    if r.status_code not in _RETRY_STATUS or attempt == self.max_retries:
                        return r
                    last_429 = _retry_after(r) if r.status_code == 429 else None
                    server_wait = min(_retry_after(r), self.retry_max_delay)
                # never sleep less than the server's Retry-After; jitter only the computed backoff
                wait = max(server_wait, min(backoff, self.retry_max_delay) * random.uniform(0.5, 1.0))
                if deadline - self.clock() < wait:
                    break
                await self.sleep(wait)
                backoff = self.retry_base_delay * 2 ** (attempt + 1)
        if last_429 is not None:
            raise RateLimited(last_429)
        raise LLMError("NIM deadline exceeded", code="LLM_TIMEOUT")

    async def chat(
        self,
        messages: list[dict[str, Any]],
        tools: list[dict[str, Any]] | None = None,
        response_format: dict[str, Any] | None = None,
        deadline: float | None = None,
    ) -> LLMResponse:
        msgs, extra = self._prepare(messages)
        body: dict[str, Any] = {
            "model": self.model,
            "messages": msgs,
            "temperature": self.temperature,
            "max_tokens": 2048,
            **extra,
        }
        if tools:
            body["tools"] = tools
            body["tool_choice"] = "auto"
        if response_format:
            body["response_format"] = response_format
        r = await self._post(body, deadline if deadline is not None else self.clock() + self.budget_s)
        if r.status_code == 429:
            raise RateLimited(_retry_after(r))
        if r.status_code != 200:
            raise LLMError(f"NIM {r.status_code}: {r.text[:300]}", code="LLM_UPSTREAM")
        try:
            data = r.json()
            msg = data["choices"][0]["message"]
        except (ValueError, KeyError, IndexError, TypeError) as e:
            raise OutputInvalid(f"malformed NIM response: {e}") from e
        content, thinking = split_thinking(msg.get("content"))
        calls: list[ToolCall] = []
        for tc in msg.get("tool_calls") or []:
            raw = tc["function"].get("arguments") or "{}"
            try:
                args = json.loads(raw) if isinstance(raw, str) else dict(raw)
            except json.JSONDecodeError as e:
                raise OutputInvalid(f"invalid tool arguments: {raw[:200]}") from e
            calls.append(
                ToolCall(
                    id=tc.get("id") or f"call_{len(calls)}",
                    name=tc["function"]["name"],
                    arguments=args,
                    raw_arguments=raw if isinstance(raw, str) else json.dumps(raw),
                )
            )
        if not calls and tools:
            names = {t["function"]["name"] for t in tools}
            calls, content = parse_text_tool_calls(content, names)
        thinking = msg.get("reasoning_content") or msg.get("reasoning") or thinking
        return LLMResponse(
            content=content,
            provider=self.info,
            tool_calls=calls,
            thinking=thinking,
            model=data.get("model", self.model),
            usage=data.get("usage") or {},
        )


CLOUDFLARE_DEFAULT_MODEL = "@cf/openai/gpt-oss-120b"


class CloudflareWorkersAIProvider(NvidiaNimProvider):
    """Cloudflare Workers AI. 같은 OpenAI 호환 Chat Completions라 재시도와 도구 호출 해석을 그대로 쓰고,
    출처만 cloudflare로 남긴다. docs/features/cloudflare-ai/spec.md"""

    info = ProviderInfo(name="cloudflare-workers-ai", kind="cloudflare")

    def __init__(self, api_token: str, account_id: str, model: str = CLOUDFLARE_DEFAULT_MODEL, **kwargs: Any) -> None:
        if not api_token or not account_id:
            raise LLMError("CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID are required", code="LLM_NO_KEY")
        super().__init__(
            api_token, f"https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1", model, **kwargs
        )
