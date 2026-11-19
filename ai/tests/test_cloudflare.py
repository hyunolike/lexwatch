"""Cloudflare Workers AI 공급자와 공급자 선택 규칙 (docs/features/cloudflare-ai/spec.md CF-01~03)."""

import json

import httpx
import pytest

from app.providers.llm.factory import get_llm
from app.providers.llm.mock import MockLLMProvider
from app.providers.llm.nim import CloudflareWorkersAIProvider, NvidiaNimProvider

ENV = ("LLM_PROVIDER", "NVIDIA_API_KEY", "CLOUDFLARE_API_TOKEN", "CLOUDFLARE_ACCOUNT_ID", "CLOUDFLARE_MODEL")


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
    assert isinstance(get_llm(), NvidiaNimProvider)
    assert get_llm().info.kind == "nvidia"


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
