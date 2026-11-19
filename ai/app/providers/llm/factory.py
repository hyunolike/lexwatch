from __future__ import annotations

import os

from app.providers.llm.base import LLMProvider
from app.providers.llm.mock import MockLLMProvider
from app.providers.llm.nim import CLOUDFLARE_DEFAULT_MODEL, CloudflareWorkersAIProvider, NvidiaNimProvider


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


def get_llm() -> LLMProvider:
    """LLM_PROVIDER: auto(기본), nvidia, cloudflare, mock.

    auto는 NVIDIA 키가 있으면 NVIDIA, 없고 Cloudflare 값이 둘 다 있으면 Cloudflare, 아니면 대체 구현.
    지정한 공급자의 값이 없으면 대체 구현으로 간다(출처 라벨도 대체 구현으로 남는다).
    """
    mode = os.environ.get("LLM_PROVIDER", "auto").lower()
    if mode == "mock":
        return MockLLMProvider()
    if mode == "nvidia":
        return _nvidia() or MockLLMProvider()
    if mode == "cloudflare":
        return _cloudflare() or MockLLMProvider()
    return _nvidia() or _cloudflare() or MockLLMProvider()
