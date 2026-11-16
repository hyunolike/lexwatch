"""Deterministic offline LLM used when no NVIDIA key is configured.

Adapted from hyunolike/reroute-irops-agent (providers/llm/mock.py), stripped of airline logic.
"""

from __future__ import annotations

from typing import Any

from app.providers.llm.base import LLMProvider, LLMResponse
from app.schemas import ProviderInfo


class MockLLMProvider(LLMProvider):
    info = ProviderInfo(name="mock-llm", kind="fallback")

    async def chat(
        self,
        messages: list[dict[str, Any]],
        tools: list[dict[str, Any]] | None = None,
        response_format: dict[str, Any] | None = None,
        deadline: float | None = None,
    ) -> LLMResponse:
        return LLMResponse(content="", provider=self.info, model="mock")
