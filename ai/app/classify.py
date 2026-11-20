from __future__ import annotations

import json
import time
from typing import Any

from pydantic import BaseModel, Field, ValidationError

from app.agent.prompts import CLASSIFY_SYSTEM
from app.providers.llm.base import OutputInvalid
from app.providers.llm.factory import get_llm
from app.providers.llm.mock import MockLLMProvider
from app.providers.llm.failover import FailoverLLM
from app.providers.llm.nim import GptOssProvider
from app.schemas import LawSummary, ProviderInfo

MISSING_REASON = "판정 누락"
KEYLESS_REASON = "키 없음: 임베딩 유사도 기준 대체 판정"
KEYLESS_PROVIDER = ProviderInfo(name="similarity-fallback", kind="fallback")
# 운영에서 gpt-oss 기본 추론 강도로는 분류 한 번이 30초를 넘겨 재시도 끝에 시간 초과가 났다(CF-06).
CLASSIFY_REASONING_EFFORT = "low"
# 백엔드는 분류를 60초 기다린다. 그보다 먼저 끝나야 시간 초과 대신 오류 코드가 남는다(CF-07).
CLASSIFY_BUDGET_S = 50.0


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
    confidence: float = Field(
        ge=0, le=1, description="관련성일 확률(0~1). 관련 없다고 판단하면 0에 가깝게, 관련 있다고 확신하면 1에 가깝게."
    )
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
    if isinstance(llm, MockLLMProvider):
        return ClassifyResponse(
            groups=[
                GroupVerdict(
                    group_code=c.group_code,
                    relevant=c.similarity >= 0.5,
                    confidence=round(min(max(c.similarity, 0.0), 1.0), 3),
                    reason=KEYLESS_REASON,
                )
                for c in req.candidates
            ],
            provider=KEYLESS_PROVIDER,
        )
    if isinstance(llm, (GptOssProvider, FailoverLLM)):
        llm.reasoning_effort = CLASSIFY_REASONING_EFFORT
    codes = [c.group_code for c in req.candidates]
    messages = [
        {"role": "system", "content": CLASSIFY_SYSTEM},
        {"role": "user", "content": _user_prompt(req)},
    ]
    verdicts: dict[str, GroupVerdict] = {}
    last_err: OutputInvalid | None = None
    resp = None
    deadline = time.monotonic() + CLASSIFY_BUDGET_S
    for _ in range(2):  # one retry on invalid or incomplete output
        resp = await llm.chat(messages, response_format=_FORMAT, deadline=deadline)
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
