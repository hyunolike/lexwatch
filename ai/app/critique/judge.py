from __future__ import annotations

import json
import logging
from typing import Any

from pydantic import BaseModel, ValidationError

from app.critique.rules import PATTERN_MESSAGE, PATTERNS, RULES, rules_text
from app.providers.llm.base import OutputInvalid
from app.providers.llm.factory import get_llm
from app.providers.llm.mock import MockLLMProvider
from app.schemas import Evidence, MemoSentence, ProviderInfo

log = logging.getLogger(__name__)

KEYLESS_PROVIDER = ProviderInfo(name="pattern-fallback", kind="fallback")

SYSTEM_BASE = """당신은 법령 영향 메모의 과잉해석을 잡는 검수자입니다.
메모의 각 문장이 제시된 근거(evidence)가 뒷받침하는 범위를 넘어 단정하거나 확장하는지 판정합니다.
문제 있는 문장만 findings에 낸다. 문제가 없으면 빈 배열을 낸다. sentence_index는 0부터 시작한다.
JSON만 출력한다."""


class CritiqueRequest(BaseModel):
    run_id: str
    memo: list[MemoSentence]
    evidence: list[Evidence]
    use_rules: bool = True


class Finding(BaseModel):
    sentence_index: int
    rule_id: str
    message: str


class CritiqueResponse(BaseModel):
    findings: list[Finding]
    provider: ProviderInfo


class _Out(BaseModel):
    findings: list[Finding]


_FORMAT: dict[str, Any] = {
    "type": "json_schema",
    "json_schema": {"name": "critique", "schema": _Out.model_json_schema()},
}


def build_system(use_rules: bool) -> str:
    if not use_rules:
        return SYSTEM_BASE + '\n출력 형식: {"findings":[{"sentence_index":0,"rule_id":"GENERAL","message":"..."}]}'
    return (
        SYSTEM_BASE
        + "\n\n판정 규칙(rule_id는 아래 ID만 쓴다):\n"
        + rules_text()
        + '\n\n출력 형식: {"findings":[{"sentence_index":0,"rule_id":"OC-04","message":"..."}]}'
    )


def _user_prompt(req: CritiqueRequest) -> str:
    lines = ["근거:"]
    lines += [f"[{e.id}] ({e.kind}) {e.title}: {e.body}" for e in req.evidence]
    lines.append("메모:")
    lines += [f"{i}. {m.sentence} (인용: {', '.join(m.citations)})" for i, m in enumerate(req.memo)]
    return "\n".join(lines)


def _clean(findings: list[Finding], n: int, use_rules: bool) -> list[Finding]:
    out: list[Finding] = []
    for f in findings:
        if not 0 <= f.sentence_index < n:
            log.warning("critique: sentence_index %s 범위 밖, 버림", f.sentence_index)
            continue
        if use_rules and f.rule_id not in RULES:
            log.warning("critique: 알 수 없는 rule_id %s, 버림", f.rule_id)
            continue
        out.append(f)
    return out


def pattern_judge(req: CritiqueRequest) -> list[Finding]:
    by_id = {e.id: e for e in req.evidence}
    out: list[Finding] = []
    for i, m in enumerate(req.memo):
        cited = [by_id[c] for c in m.citations if c in by_id]
        for rule_id, det in PATTERNS.items():
            if det(m.sentence, cited):
                out.append(Finding(sentence_index=i, rule_id=rule_id, message=PATTERN_MESSAGE + RULES[rule_id]))
    return out


async def critique(req: CritiqueRequest) -> CritiqueResponse:
    llm = get_llm()
    if isinstance(llm, MockLLMProvider):
        return CritiqueResponse(findings=pattern_judge(req), provider=KEYLESS_PROVIDER)
    messages = [
        {"role": "system", "content": build_system(req.use_rules)},
        {"role": "user", "content": _user_prompt(req)},
    ]
    last: OutputInvalid | None = None
    for _ in range(2):
        resp = await llm.chat(messages, response_format=_FORMAT)
        try:
            out = _Out.model_validate(json.loads(resp.content or ""))
        except (ValueError, ValidationError) as e:
            last = OutputInvalid(f"크리틱 응답을 해석할 수 없음: {e}")
            continue
        return CritiqueResponse(findings=_clean(out.findings, len(req.memo), req.use_rules), provider=resp.provider)
    assert last is not None
    raise last
