"""Retriever port. Adapted from hyunolike/reroute-irops-agent (apps/api/app/rag/base.py)."""

from __future__ import annotations

from abc import ABC, abstractmethod
from typing import Literal

from app.schemas import ProviderInfo


class RetrieverError(RuntimeError):
    pass


class RetrieverProvider(ABC):
    info: ProviderInfo

    @abstractmethod
    async def embed(self, texts: list[str], input_type: Literal["query", "passage"]) -> list[list[float]]: ...

    @abstractmethod
    async def rerank(self, query: str, passages: list[str]) -> list[int]:
        """Passage indices, most relevant first."""
