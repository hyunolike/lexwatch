"""Cloudflare Workers AI 공급자와 공급자 선택 규칙 (docs/features/cloudflare-ai/spec.md CF-01~03)."""

import json

import httpx
import pytest

from app.providers.llm.base import OutputInvalid
from app.providers.llm.factory import get_llm
from app.providers.llm.mock import MockLLMProvider
from app.providers.llm.nim import CloudflareWorkersAIProvider, NvidiaNimProvider

ENV = ("LLM_PROVIDER", "NVIDIA_API_KEY", "CLOUDFLARE_API_TOKEN", "CLOUDFLARE_ACCOUNT_ID", "CLOUDFLARE_MODEL",
       "CLOUDFLARE_REASONING_EFFORT")


@pytest.fixture
def env(monkeypatch):
    for k in ENV:
        monkeypatch.delenv(k, raising=False)
    return monkeypatch


def test_auto_without_keys_is_mock(env):
    assert isinstance(get_llm(), MockLLMProvider)


def test_auto_prefers_nvidia_when_both_present(env):
    env.setenv("NVIDIA_API_KEY", "nv")
    env.setenv("CLOUDFLARE_API_TOKEN", "cf")
    env.setenv("CLOUDFLARE_ACCOUNT_ID", "acct")
    # 둘 다 있으면 NVIDIA를 앞에 둔 교체 묶음이 된다(docs/features/llm-failover).
    llm = get_llm()
    assert isinstance(llm.providers[0], NvidiaNimProvider)
    assert llm.info.kind == "nvidia"


def test_auto_uses_cloudflare_without_nvidia_key(env):
    env.setenv("CLOUDFLARE_API_TOKEN", "cf")
    env.setenv("CLOUDFLARE_ACCOUNT_ID", "acct")
    llm = get_llm()
    assert isinstance(llm, CloudflareWorkersAIProvider)
    assert llm.info.kind == "cloudflare"
    assert llm.info.name == "cloudflare-workers-ai"
    assert llm.base_url == "https://api.cloudflare.com/client/v4/accounts/acct/ai/v1"
    assert llm.model == "@cf/openai/gpt-oss-120b"


def test_cloudflare_needs_both_token_and_account(env):
    env.setenv("CLOUDFLARE_API_TOKEN", "cf")
    assert isinstance(get_llm(), MockLLMProvider)


def test_explicit_provider_overrides_auto(env):
    env.setenv("NVIDIA_API_KEY", "nv")
    env.setenv("CLOUDFLARE_API_TOKEN", "cf")
    env.setenv("CLOUDFLARE_ACCOUNT_ID", "acct")
    env.setenv("LLM_PROVIDER", "cloudflare")
    assert get_llm().info.kind == "cloudflare"
    env.setenv("LLM_PROVIDER", "mock")
    assert isinstance(get_llm(), MockLLMProvider)


def test_explicit_cloudflare_without_values_falls_back_to_mock(env):
    env.setenv("LLM_PROVIDER", "cloudflare")
    assert isinstance(get_llm(), MockLLMProvider)


def test_model_can_be_overridden(env):
    env.setenv("CLOUDFLARE_API_TOKEN", "cf")
    env.setenv("CLOUDFLARE_ACCOUNT_ID", "acct")
    env.setenv("CLOUDFLARE_MODEL", "@cf/zai-org/glm-5.2")
    assert get_llm().model == "@cf/zai-org/glm-5.2"


async def test_cloudflare_request_shape_and_provider_info():
    seen = {}

    def handler(req: httpx.Request) -> httpx.Response:
        seen["url"] = str(req.url)
        seen["auth"] = req.headers["authorization"]
        seen["body"] = json.loads(req.content)
        return httpx.Response(200, json={"choices": [{"message": {"role": "assistant", "content": "안녕하세요"}}]})

    llm = CloudflareWorkersAIProvider("tok", "acct", transport=httpx.MockTransport(handler))
    r = await llm.chat([{"role": "system", "content": "s"}, {"role": "user", "content": "u"}])
    assert seen["url"] == "https://api.cloudflare.com/client/v4/accounts/acct/ai/v1/chat/completions"
    assert seen["auth"] == "Bearer tok"
    assert seen["body"]["model"] == "@cf/openai/gpt-oss-120b"
    # Nemotron 전용 사고 토글은 섞이지 않는다.
    assert "chat_template_kwargs" not in seen["body"]
    assert seen["body"]["messages"][0]["content"] == "s"
    assert r.content == "안녕하세요"
    assert r.provider.kind == "cloudflare"


def _capture():
    seen: dict = {}

    def handler(req: httpx.Request) -> httpx.Response:
        seen["body"] = json.loads(req.content)
        return httpx.Response(200, json={"choices": [{"message": {"role": "assistant", "content": "ok"}}]})

    return seen, httpx.MockTransport(handler)


async def test_default_leaves_reasoning_effort_to_the_model():
    seen, transport = _capture()
    await CloudflareWorkersAIProvider("tok", "acct", transport=transport).chat([{"role": "user", "content": "u"}])
    assert "reasoning_effort" not in seen["body"]


async def test_classify_lowers_reasoning_effort_on_cloudflare(env):
    # CF-06: 운영에서 분류 시도 하나가 30초를 넘겨 재시도 끝에 90초 502가 났다. 분류만 생각 토큰을 줄인다.
    from app import classify as classify_mod

    seen, transport = _capture()
    llm = CloudflareWorkersAIProvider("tok", "acct", transport=transport)
    env.setattr(classify_mod, "get_llm", lambda: llm)
    req = classify_mod.ClassifyRequest.model_validate({
        "run_id": "r",
        "law": {"law_id": "1", "name": "법", "promulgation_date": "2026-11-18", "summary": "s"},
        "candidates": [{"group_code": "labor", "interest_text": "노동", "similarity": 0.3}],
    })
    with pytest.raises(OutputInvalid):  # 응답 "ok"는 분류 JSON이 아니다. 요청 본문만 본다.
        await classify_mod.classify(req)
    assert seen["body"]["reasoning_effort"] == "low"


async def test_reasoning_effort_is_configurable_and_skipped_for_other_models():
    seen, transport = _capture()
    llm = CloudflareWorkersAIProvider("tok", "acct", reasoning_effort="medium", transport=transport)
    await llm.chat([{"role": "user", "content": "u"}])
    assert seen["body"]["reasoning_effort"] == "medium"

    seen, transport = _capture()
    other = CloudflareWorkersAIProvider("tok", "acct", model="@cf/zai-org/glm-5.2", transport=transport)
    await other.chat([{"role": "user", "content": "u"}])
    assert "reasoning_effort" not in seen["body"]


def test_factory_reads_reasoning_effort(env):
    env.setenv("CLOUDFLARE_API_TOKEN", "cf")
    env.setenv("CLOUDFLARE_ACCOUNT_ID", "acct")
    assert get_llm().reasoning_effort is None
    env.setenv("CLOUDFLARE_REASONING_EFFORT", "high")
    assert get_llm().reasoning_effort == "high"


class _Deadlines:
    """chat에 넘긴 deadline이 지금부터 몇 초 뒤인지 기록하는 가짜 LLM."""

    def __init__(self, content: str):
        import time

        from app.schemas import ProviderInfo

        self.info = ProviderInfo(name="cf", kind="cloudflare")
        self.content = content
        self.left: list[float] = []
        self._now = time.monotonic

    async def chat(self, messages, tools=None, response_format=None, deadline=None):
        from app.providers.llm.base import LLMResponse

        self.left.append(deadline - self._now())
        return LLMResponse(content=self.content, provider=self.info)


async def test_classify_budget_fits_inside_backend_wait(env):
    # CF-07: 백엔드는 분류를 60초 기다린다. AI 서비스가 먼저 끝내야 오류 코드가 남는다.
    from app import classify as classify_mod

    llm = _Deadlines(json.dumps({"groups": [{"group_code": "labor", "relevant": False, "confidence": 0.1, "reason": "r"}]}))
    env.setattr(classify_mod, "get_llm", lambda: llm)
    req = classify_mod.ClassifyRequest.model_validate({
        "run_id": "r",
        "law": {"law_id": "1", "name": "법", "promulgation_date": "2026-11-18", "summary": "s"},
        "candidates": [{"group_code": "labor", "interest_text": "노동", "similarity": 0.3}],
    })
    await classify_mod.classify(req)
    assert 40 < llm.left[0] <= 50


async def test_critique_budget_fits_inside_backend_wait(env):
    # CF-07: 백엔드는 크리틱을 130초 기다린다(분석과 같은 긴 대기). AI 서비스 예산은 120초.
    from app.critique import judge as judge_mod

    llm = _Deadlines(json.dumps({"findings": []}))
    env.setattr(judge_mod, "get_llm", lambda: llm)
    req = judge_mod.CritiqueRequest.model_validate({
        "run_id": "r",
        "memo": [{"sentence": "s", "citations": ["e"]}],
        "evidence": [{"id": "e", "kind": "law", "title": "t", "body": "b"}],
        "use_rules": True,
    })
    await judge_mod.critique(req)
    assert 100 < llm.left[0] <= 120


def test_cloudflare_attempts_are_not_cut_before_slow_answers():
    # CF-07: 운영에서 크리틱 응답이 27~52초 걸렸다. 30초에 끊고 다시 부르면 매번 같은 자리에서 끊긴다.
    llm = CloudflareWorkersAIProvider("tok", "acct")
    assert llm.attempt_timeout_s >= 110
    assert llm.budget_s >= 110


async def test_cloudflare_allows_longer_outputs_for_reasoning():
    # CF-07: 생각 토큰 때문에 6문장 크리틱이 2,048 토큰에서 잘렸다(finish=length).
    seen, transport = _capture()
    await CloudflareWorkersAIProvider("tok", "acct", transport=transport).chat([{"role": "user", "content": "u"}])
    assert seen["body"]["max_tokens"] == 4096
    seen, transport = _capture()
    nv = NvidiaNimProvider("k", "https://nim.example/v1", "nvidia/nemotron", transport=transport)
    await nv.chat([{"role": "user", "content": "u"}])
    assert seen["body"]["max_tokens"] == 2048
