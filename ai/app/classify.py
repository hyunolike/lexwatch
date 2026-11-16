from __future__ import annotations

import json
from typing import Any

from pydantic import BaseModel, Field, ValidationError

from app.agent.prompts import CLASSIFY_SYSTEM
from app.providers.llm.base import OutputInvalid
from app.providers.llm.factory import get_llm
from app.schemas import LawSummary, ProviderInfo

MISSING_REASON = "판정 누락"


class GroupCandidate(BaseModel):
    group_code: str
    interest_text: str
    similarity: float


class ClassifyRequest(BaseModel):
    run_id: str
    law: LawSummary
    candidates: list[GroupCandidate] = Field(min_length=1)


class GroupVerdict(BaseModel):
    group_code: str
    relevant: bool
    confidence: float = Field(ge=0, le=1)
    reason: str


class ClassifyResponse(BaseModel):
    groups: list[GroupVerdict]
    provider: ProviderInfo


class _LLMOut(BaseModel):
    groups: list[GroupVerdict]


_FORMAT: dict[str, Any] = {
    "type": "json_schema",
    "json_schema": {"name": "classify", "schema": _LLMOut.model_json_schema()},
}


def _user_prompt(req: ClassifyRequest) -> str:
    lines = [
        f"법령: {req.law.name} (공포 {req.law.promulgation_date})",
        f"요약: {req.law.summary}",
        "후보 실무그룹:",
    ]
    lines += [f"- {c.group_code}: {c.interest_text} (유사도 {c.similarity:.2f})" for c in req.candidates]
    return "\n".join(lines)


def _parse(content: str | None, codes: set[str]) -> dict[str, GroupVerdict]:
    """Raise OutputInvalid on malformed/unknown output; return verdicts by code."""
    try:
        out = _LLMOut.model_validate(json.loads(content or ""))
    except (ValueError, ValidationError) as e:
        raise OutputInvalid(f"분류 응답을 해석할 수 없음: {e}") from e
    unknown = {g.group_code for g in out.groups} - codes
    if unknown:
        raise OutputInvalid(f"후보에 없는 group_code: {sorted(unknown)}")
    return {g.group_code: g for g in out.groups}


async def classify(req: ClassifyRequest) -> ClassifyResponse:
    llm = get_llm()
    codes = [c.group_code for c in req.candidates]
    messages = [
        {"role": "system", "content": CLASSIFY_SYSTEM},
        {"role": "user", "content": _user_prompt(req)},
    ]
    verdicts: dict[str, GroupVerdict] = {}
    last_err: OutputInvalid | None = None
    resp = None
    for _ in range(2):  # one retry on invalid or incomplete output
        resp = await llm.chat(messages, response_format=_FORMAT)
        try:
            verdicts = _parse(resp.content, set(codes))
        except OutputInvalid as e:
            last_err = e
            continue
        last_err = None
        if all(c in verdicts for c in codes):
            break
    if last_err is not None:
        raise last_err
    assert resp is not None
    groups = [
        verdicts.get(c) or GroupVerdict(group_code=c, relevant=False, confidence=0.0, reason=MISSING_REASON)
        for c in codes
    ]
    return ClassifyResponse(groups=groups, provider=resp.provider)
