from __future__ import annotations

import os

from app.providers.llm.base import LLMProvider
from app.providers.llm.mock import MockLLMProvider
from app.providers.llm.nim import NvidiaNimProvider


def get_llm() -> LLMProvider:
    mode = os.environ.get("LLM_PROVIDER", "auto").lower()
    key = os.environ.get("NVIDIA_API_KEY", "").strip()
    if mode == "mock" or not key:
        return MockLLMProvider()
    return NvidiaNimProvider(
        key,
        os.environ.get("NIM_BASE_URL", "https://integrate.api.nvidia.com/v1"),
        os.environ.get("NIM_MODEL", "nvidia/nemotron-3.5-super-vl-120b-a12b"),
    )
