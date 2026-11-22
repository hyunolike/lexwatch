from __future__ import annotations

import os

from app.providers.llm.base import LLMProvider
from app.providers.llm.mock import MockLLMProvider
from app.providers.llm.failover import FailoverLLM
from app.providers.llm.nim import (
    CLOUDFLARE_DEFAULT_MODEL,
    GROQ_DEFAULT_MODEL,
    CloudflareWorkersAIProvider,
    GroqProvider,
    NvidiaNimProvider,
)


def _nvidia() -> LLMProvider | None:
    key = os.environ.get("NVIDIA_API_KEY", "").strip()
    if not key:
        return None
    return NvidiaNimProvider(
        key,
        os.environ.get("NIM_BASE_URL", "https://integrate.api.nvidia.com/v1"),
        os.environ.get("NIM_MODEL", "nvidia/nemotron-3.5-super-vl-120b-a12b"),
    )


def _cloudflare() -> LLMProvider | None:
    token = os.environ.get("CLOUDFLARE_API_TOKEN", "").strip()
    account = os.environ.get("CLOUDFLARE_ACCOUNT_ID", "").strip()
    if not token or not account:
        return None
    return CloudflareWorkersAIProvider(
        token,
        account,
        os.environ.get("CLOUDFLARE_MODEL", CLOUDFLARE_DEFAULT_MODEL),
        reasoning_effort=os.environ.get("CLOUDFLARE_REASONING_EFFORT", "").strip() or None,
    )


def _groq() -> LLMProvider | None:
    key = os.environ.get("GROQ_API_KEY", "").strip()
    if not key:
        return None
    return GroqProvider(key, os.environ.get("GROQ_MODEL", GROQ_DEFAULT_MODEL))


def get_llm() -> LLMProvider:
    """LLM_PROVIDER: auto(기본), nvidia, cloudflare, groq, mock.

    auto는 값이 있는 공급자를 NVIDIA → Cloudflare → Groq 순서로 묶어, 앞이 한도 초과나 장애로 실패하면
    다음으로 넘긴다(docs/features/llm-failover/spec.md). 하나뿐이면 그대로, 없으면 대체 구현.
    지정한 공급자의 값이 없으면 대체 구현으로 간다(출처 라벨도 대체 구현으로 남는다).
    """
    mode = os.environ.get("LLM_PROVIDER", "auto").lower()
    if mode == "mock":
        return MockLLMProvider()
    if mode == "nvidia":
        return _nvidia() or MockLLMProvider()
    if mode == "cloudflare":
        return _cloudflare() or MockLLMProvider()
    if mode == "groq":
        return _groq() or MockLLMProvider()
    chain = [p for p in (_nvidia(), _cloudflare(), _groq()) if p is not None]
    if not chain:
        return MockLLMProvider()
    return chain[0] if len(chain) == 1 else FailoverLLM(chain)
