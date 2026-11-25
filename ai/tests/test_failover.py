"""무료 모델 한도 소진 시 자동 교체(docs/features/llm-failover/spec.md FO-01~06)."""

import json
import time

import httpx
import pytest

from app.providers.llm import failover
from app.providers.llm.base import LLMError, LLMProvider, LLMResponse, OutputInvalid, RateLimited
from app.providers.llm.factory import get_llm
from app.providers.llm.failover import FailoverLLM
from app.providers.llm.mock import MockLLMProvider
from app.providers.llm.nim import CloudflareWorkersAIProvider, GroqProvider, NvidiaNimProvider
from app.schemas import ProviderInfo

ENV = ("LLM_PROVIDER", "NVIDIA_API_KEY", "CLOUDFLARE_API_TOKEN", "CLOUDFLARE_ACCOUNT_ID", "GROQ_API_KEY", "GROQ_MODEL")


@pytest.fixture(autouse=True)
def _clean(monkeypatch):
    for k in ENV:
        monkeypatch.delenv(k, raising=False)
    failover.reset_cooldowns()
    yield
    failover.reset_cooldowns()


class Fake(LLMProvider):
    def __init__(self, name, *outcomes):
        self.info = ProviderInfo(name=name, kind="cloudflare" if name == "cf" else "groq")
        self.outcomes = list(outcomes)
        self.calls = 0
        self.deadlines = []
        self.reasoning_effort = None

    async def chat(self, messages, tools=None, response_format=None, deadline=None):
        self.calls += 1
        self.deadlines.append(deadline)
        o = self.outcomes.pop(0) if self.outcomes else "ok"
        if isinstance(o, Exception):
            raise o
        return LLMResponse(content=o, provider=self.info)


MSG = [{"role": "user", "content": "u"}]


@pytest.mark.parametrize("err", [RateLimited(30.0), LLMError("x", code="LLM_TIMEOUT"), LLMError("x", code="LLM_UNREACHABLE"), LLMError("NIM 503", code="LLM_UPSTREAM")])
async def test_failure_moves_to_next_provider_and_reports_who_answered(err):
    cf, groq = Fake("cf", err), Fake("groq")
    llm = FailoverLLM([cf, groq])
    r = await llm.chat(MSG)
    assert (r.content, r.provider.name, llm.info.name) == ("ok", "groq", "groq")
    assert (cf.calls, groq.calls) == (1, 1)


async def test_output_invalid_is_not_failed_over():
    cf, groq = Fake("cf", OutputInvalid("bad json")), Fake("groq")
    with pytest.raises(OutputInvalid):
        await FailoverLLM([cf, groq]).chat(MSG)
    assert groq.calls == 0


async def test_rate_limited_provider_is_skipped_for_rest_of_request_and_cooled_down():
    cf, groq = Fake("cf", RateLimited(5.0)), Fake("groq")
    llm = FailoverLLM([cf, groq])
    await llm.chat(MSG)
    await llm.chat(MSG)                        # 에이전트의 다음 턴
    assert (cf.calls, groq.calls) == (1, 2)
    cf2, groq2 = Fake("cf"), Fake("groq")       # 같은 인스턴스의 다음 요청
    await FailoverLLM([cf2, groq2]).chat(MSG)
    assert (cf2.calls, groq2.calls) == (0, 1)


async def test_cooldown_expires(monkeypatch):
    now = [1000.0]
    monkeypatch.setattr(failover, "_now", lambda: now[0])
    await FailoverLLM([Fake("cf", RateLimited(1.0)), Fake("groq")]).chat(MSG)
    now[0] += failover.MIN_COOLDOWN_S + 1
    cf = Fake("cf")
    await FailoverLLM([cf, Fake("groq")]).chat(MSG)
    assert cf.calls == 1


async def test_all_fail_raises_last_error():
    with pytest.raises(RateLimited):
        await FailoverLLM([Fake("cf", LLMError("x", code="LLM_TIMEOUT")), Fake("groq", RateLimited(60.0))]).chat(MSG)


async def test_no_time_left_does_not_fail_over():
    cf, groq = Fake("cf", LLMError("x", code="LLM_TIMEOUT")), Fake("groq")
    with pytest.raises(LLMError):
        await FailoverLLM([cf, groq]).chat(MSG, deadline=time.monotonic() - 1)
    assert groq.calls == 0


async def test_same_deadline_is_passed_to_each_provider():
    cf, groq = Fake("cf", LLMError("x", code="LLM_TIMEOUT")), Fake("groq")
    d = time.monotonic() + 50
    await FailoverLLM([cf, groq]).chat(MSG, deadline=d)
    assert cf.deadlines == groq.deadlines == [d]


def test_reasoning_effort_is_forwarded_to_members():
    cf, groq = Fake("cf"), Fake("groq")
    llm = FailoverLLM([cf, groq])
    llm.reasoning_effort = "low"
    assert cf.reasoning_effort == groq.reasoning_effort == "low"


async def test_groq_request_shape():
    seen = {}

    def handler(req):
        seen["url"], seen["auth"], seen["body"] = str(req.url), req.headers["authorization"], json.loads(req.content)
        return httpx.Response(200, json={"choices": [{"message": {"role": "assistant", "content": "안녕"}}]})

    g = GroqProvider("gk", transport=httpx.MockTransport(handler), reasoning_effort="low")
    r = await g.chat(MSG)
    assert seen["url"] == "https://api.groq.com/openai/v1/chat/completions"
    assert seen["auth"] == "Bearer gk"
    assert seen["body"]["model"] == "openai/gpt-oss-120b"
    assert seen["body"]["reasoning_effort"] == "low"
    assert "chat_template_kwargs" not in seen["body"]
    assert (r.provider.kind, r.provider.name) == ("groq", "groq")


def test_factory_builds_chain_in_order(monkeypatch):
    assert isinstance(get_llm(), MockLLMProvider)
    monkeypatch.setenv("GROQ_API_KEY", "g")
    assert isinstance(get_llm(), GroqProvider)                 # 하나뿐이면 그대로
    monkeypatch.setenv("CLOUDFLARE_API_TOKEN", "t")
    monkeypatch.setenv("CLOUDFLARE_ACCOUNT_ID", "a")
    monkeypatch.setenv("NVIDIA_API_KEY", "n")
    llm = get_llm()
    assert isinstance(llm, FailoverLLM)
    assert [type(p) for p in llm.providers] == [NvidiaNimProvider, CloudflareWorkersAIProvider, GroqProvider]
    monkeypatch.setenv("LLM_PROVIDER", "groq")
    assert isinstance(get_llm(), GroqProvider)


def test_groq_waits_longer_for_per_minute_token_limit():
    # 분당 8,000 토큰 한도는 1분 안에 풀린다. 8초에서 포기하지 않는다.
    assert GroqProvider("g").retry_max_delay >= 30



async def test_groq_drops_optional_tools_and_keeps_json_format():
    # Groq는 response_format과 tools를 함께 보내면 400을 낸다(2026-11-25 운영 검토 보조 실패).
    from app.providers.llm.base import OptionalTools

    seen = []

    def handler(req):
        body = json.loads(req.content)
        seen.append(("tools" in body, "response_format" in body))
        return httpx.Response(200, json={"choices": [{"message": {"role": "assistant", "content": "{}"}}]})

    g = GroqProvider("gk", transport=httpx.MockTransport(handler))
    fmt = {"type": "json_schema", "json_schema": {"name": "x", "schema": {}}}
    tool = {"type": "function", "function": {"name": "get_article", "parameters": {}}}
    await g.chat(MSG, tools=OptionalTools([tool]), response_format=fmt)
    await g.chat(MSG, tools=[tool], response_format=fmt)  # 분석 단계의 필수 도구는 그대로 보낸다(동작 변경 없음)
    assert seen == [(False, True), (True, True)]
