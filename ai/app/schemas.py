from __future__ import annotations

from datetime import date
from typing import Literal

from pydantic import BaseModel, Field


class ProviderInfo(BaseModel):
    name: str
    kind: Literal["nvidia", "fallback"]


class Evidence(BaseModel):
    id: str
    kind: str
    title: str
    body: str


class LawSummary(BaseModel):
    law_id: str
    name: str
    promulgation_date: date
    effective_date: date | None = None
    summary: str


class MemoSentence(BaseModel):
    sentence: str
    citations: list[str] = Field(min_length=1)


class EmbedRequest(BaseModel):
    texts: list[str]
    input_type: Literal["query", "passage"]


class EmbedResponse(BaseModel):
    vectors: list[list[float]]
    provider: ProviderInfo
