import httpx
import pytest

from app.providers.llm.base import OutputInvalid, RateLimited
from app.providers.llm.nim import NvidiaNimProvider

TOOLS = [{"type": "function", "function": {"name": "classify_change", "parameters": {}}}]


def make(handler, sleeps):
    async def sleep(s):
        sleeps.append(s)

    return NvidiaNimProvider(
        "k", "https://x/v1", "nvidia/nemotron-3-super-120b-a12b",
        transport=httpx.MockTransport(handler), sleep=sleep,
    )


async def test_retries_429_using_retry_after(fixture):
    f429, ok = fixture("chat_429.json"), fixture("chat_tool_call.json")
    calls = []

    def handler(req):
        calls.append(req)
        if len(calls) == 1:
            return httpx.Response(429, headers=f429["headers"], json=f429["body"])
        return httpx.Response(200, json=ok)

    sleeps: list[float] = []
    r = await make(handler, sleeps).chat([{"role": "user", "content": "x"}], tools=TOOLS)
    assert len(calls) == 2
    assert sleeps[0] >= 2.0  # never below Retry-After
    assert r.tool_calls[0].name == "classify_change"
    assert r.tool_calls[0].arguments["confidence"] == 0.86
    assert r.provider.kind == "nvidia"


async def test_exhausted_retries_raise_rate_limited_with_retry_after(fixture):
    f429 = fixture("chat_429.json")
    calls = []

    def handler(req):
        calls.append(req)
        return httpx.Response(429, headers=f429["headers"], json=f429["body"])

    with pytest.raises(RateLimited) as e:
        await make(handler, []).chat([{"role": "user", "content": "x"}])
    assert len(calls) == 5  # 1 + 4 retries
    assert e.value.retry_after_s == 2.0 and e.value.code == "LLM_RATE_LIMITED"


async def test_text_tool_call_is_parsed():
    content = '<think>reasoning</think>ok <TOOLCALL>[{"name":"classify_change","arguments":{"domain":"노동"}}]</TOOLCALL>'
    body = {"model": "m", "choices": [{"message": {"content": content}}]}
    r = await make(lambda req: httpx.Response(200, json=body), []).chat([{"role": "user", "content": "x"}], tools=TOOLS)
    assert r.tool_calls[0].arguments == {"domain": "노동"}
    assert r.thinking == "reasoning"
    assert r.content == "ok"


async def test_response_format_is_sent():
    seen = {}

    def handler(req):
        import json
        seen.update(json.loads(req.content))
        return httpx.Response(200, json={"choices": [{"message": {"content": "{}"}}]})

    rf = {"type": "json_schema", "json_schema": {"name": "x", "schema": {}}}
    await make(handler, []).chat([{"role": "user", "content": "x"}], response_format=rf)
    assert seen["response_format"] == rf


async def test_deadline_stops_retries_with_rate_limited(fixture):
    f429 = fixture("chat_429.json")
    now = [0.0]
    sleeps: list[float] = []

    async def sleep(s):
        sleeps.append(s)
        now[0] += s

    calls = []

    def handler(req):
        calls.append(req)
        return httpx.Response(429, headers={"retry-after": "5"}, json=f429["body"])

    p = NvidiaNimProvider("k", "https://x/v1", "m", transport=httpx.MockTransport(handler),
                          sleep=sleep, clock=lambda: now[0], budget_s=12.0)
    with pytest.raises(RateLimited) as e:
        await p.chat([{"role": "user", "content": "x"}])
    assert e.value.retry_after_s == 5.0
    assert now[0] <= 12.0 and len(calls) < 5


async def test_explicit_deadline_already_passed_raises_llm_error():
    from app.providers.llm.base import LLMError
    p = NvidiaNimProvider("k", "https://x/v1", "m", transport=httpx.MockTransport(lambda r: httpx.Response(200)),
                          clock=lambda: 100.0)
    with pytest.raises(LLMError) as e:
        await p.chat([{"role": "user", "content": "x"}], deadline=50.0)
    assert e.value.code == "LLM_TIMEOUT"


async def test_bad_tool_arguments_raise_output_invalid():
    body = {"choices": [{"message": {"tool_calls": [
        {"id": "1", "function": {"name": "f", "arguments": "{not json"}}]}}]}
    with pytest.raises(OutputInvalid):
        await make(lambda req: httpx.Response(200, json=body), []).chat([{"role": "user", "content": "x"}])
