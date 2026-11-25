"""LLM port. Adapted from hyunolike/reroute-irops-agent (apps/api/app/providers/llm/base.py)."""

from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Any

from app.schemas import ProviderInfo


class LLMError(RuntimeError):
    def __init__(self, message: str, code: str = "LLM_ERROR", retry_after_s: float | None = None) -> None:
        super().__init__(message)
        self.code = code
        self.retry_after_s = retry_after_s


class RateLimited(LLMError):
    def __init__(self, retry_after_s: float, message: str = "rate limited") -> None:
        super().__init__(message, code="LLM_RATE_LIMITED", retry_after_s=retry_after_s)


class OutputInvalid(Exception):
    def __init__(self, detail: str) -> None:
        super().__init__(detail)
        self.detail = detail


@dataclass
class ToolCall:
    id: str
    name: str
    arguments: dict[str, Any]
    raw_arguments: str = ""


@dataclass
class LLMResponse:
    content: str | None
    provider: ProviderInfo
    tool_calls: list[ToolCall] = field(default_factory=list)
    thinking: str | None = None
    model: str = ""
    usage: dict[str, Any] = field(default_factory=dict)


class LLMProvider(ABC):
    info: ProviderInfo

    @abstractmethod
    async def chat(
        self,
        messages: list[dict[str, Any]],
        tools: list[dict[str, Any]] | None = None,
        response_format: dict[str, Any] | None = None,
        deadline: float | None = None,
    ) -> LLMResponse: ...


class OptionalTools(list):
    """Tools the caller can do without (the answer can come from the prompt alone).

    Providers that cannot combine tools with a JSON response format (Groq) drop these tools and keep the format.
    """
