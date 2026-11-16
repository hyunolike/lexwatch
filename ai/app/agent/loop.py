from __future__ import annotations

import asyncio
import json
import re
import time
from typing import Any, Literal

from pydantic import BaseModel, ValidationError

from app.agent.prompts import ANALYZE_SYSTEM
from app.agent.tools import EMPTY_EVIDENCE_ERROR, TOOL_NAMES, TOOL_SCHEMAS, Tools, clip
from app.lawapi import LawApi, LawApiError
from app.providers.llm.base import LLMError, LLMProvider, OutputInvalid
from app.providers.llm.factory import get_llm
from app.providers.llm.mock import MockLLMProvider
from app.rag.factory import get_retriever
from app.schemas import Evidence, LawSummary, MemoSentence, ProviderInfo

MAX_TOOL_CALLS = 8
DEADLINE_S = 120.0
MAX_NUDGES = 2
HTTP_TIMEOUT_S = 15.0
_now = time.monotonic  # patchable clock
KEYLESS_PROVIDER = ProviderInfo(name="extract-fallback", kind="fallback")
KEYLESS_PREFIX = "[키 없음 · 원문 발췌] "
KEYLESS_QUESTION = "키 없음: 모델 분석을 수행하지 않았습니다."


class AnalyzeRequest(BaseModel):
    run_id: str
    group_code: str
    interest_text: str
    law: LawSummary
    evidence: list[Evidence]


class ToolCallRecord(BaseModel):
    seq: int
    tool: str
    args: dict[str, Any]
    result_summary: str
    evidence_ids: list[str]


class AnalyzeResponse(BaseModel):
    status: Literal["ok", "insufficient_evidence"]
    memo: list[MemoSentence]
    open_questions: list[str]
    tool_calls: list[ToolCallRecord]
    new_evidence: list[Evidence]
    provider: ProviderInfo


class _Final(BaseModel):
    memo: list[MemoSentence]
    open_questions: list[str] = []


_FORMAT: dict[str, Any] = {
    "type": "json_schema",
    "json_schema": {"name": "analyze", "schema": _Final.model_json_schema()},
}


def get_law_api() -> LawApi:
    return LawApi()


def _first_sentence(body: str) -> str:
    m = re.search(r"(.+?[.!?。])(\s|$)", body.strip(), re.S)
    return (m.group(1) if m else body.strip())[:120]


def _keyless(req: AnalyzeRequest) -> AnalyzeResponse:
    if not req.evidence:
        return AnalyzeResponse(
            status="insufficient_evidence", memo=[], open_questions=[KEYLESS_QUESTION],
            tool_calls=[], new_evidence=[], provider=KEYLESS_PROVIDER,
        )
    memo = [
        MemoSentence(sentence=KEYLESS_PREFIX + _first_sentence(e.body), citations=[e.id]) for e in req.evidence[:3]
    ]
    return AnalyzeResponse(
        status="ok", memo=memo, open_questions=[KEYLESS_QUESTION],
        tool_calls=[], new_evidence=[], provider=KEYLESS_PROVIDER,
    )


def _render(e: Evidence) -> str:
    return f"[{e.id}] {e.title}\n{clip(e.body)}"


def _user_prompt(req: AnalyzeRequest) -> str:
    ev = "\n\n".join(_render(e) for e in req.evidence) or "(없음)"
    return (
        f"법령: {req.law.name} (공포 {req.law.promulgation_date}, 법령ID {req.law.law_id})\n"
        f"요약: {req.law.summary}\n"
        f"실무그룹: {req.group_code} — 관심 분야: {req.interest_text}\n\n"
        f"현재 확보한 근거:\n{ev}"
    )


def _parse(content: str | None) -> _Final:
    text = (content or "").strip()
    m = re.search(r"\{.*\}", text, re.S)
    try:
        return _Final.model_validate(json.loads(m.group(0) if m else text))
    except (ValueError, ValidationError) as e:
        raise OutputInvalid(f"최종 답을 해석할 수 없음: {e}") from e


def _insufficient(
    provider: ProviderInfo, calls: list[ToolCallRecord], new: dict[str, Evidence], questions: list[str] | None = None
) -> AnalyzeResponse:
    return AnalyzeResponse(
        status="insufficient_evidence", memo=[], open_questions=questions or [],
        tool_calls=calls, new_evidence=list(new.values()), provider=provider,
    )


async def analyze(req: AnalyzeRequest, llm: LLMProvider | None = None) -> AnalyzeResponse:
    llm = llm or get_llm()
    if isinstance(llm, MockLLMProvider):
        return _keyless(req)
    deadline = _now() + DEADLINE_S
    tools = Tools(get_law_api(), get_retriever(), req.group_code, req.interest_text, req.law.mst, req.law.law_id)
    api = tools.api
    known = {e.id for e in req.evidence}
    new: dict[str, Evidence] = {}
    calls: list[ToolCallRecord] = []
    provider = llm.info
    messages: list[dict[str, Any]] = [
        {"role": "system", "content": ANALYZE_SYSTEM},
        {"role": "user", "content": _user_prompt(req)},
    ]
    nudges = 0
    corrected = False
    used = 0

    while True:
        if _now() >= deadline:
            return _insufficient(provider, calls, new)
        try:
            resp = await llm.chat(messages, tools=TOOL_SCHEMAS, response_format=_FORMAT, deadline=deadline)
        except LLMError as e:
            if e.code == "LLM_TIMEOUT":  # RateLimited is a different code and propagates
                return _insufficient(provider, calls, new)
            raise
        provider = resp.provider

        if resp.tool_calls:
            messages.append(
                {
                    "role": "assistant",
                    "content": resp.content or "",
                    "tool_calls": [
                        {
                            "id": tc.id, "type": "function",
                            "function": {"name": tc.name, "arguments": tc.raw_arguments or json.dumps(tc.arguments, ensure_ascii=False)},
                        }
                        for tc in resp.tool_calls
                    ],
                }
            )
            for tc in resp.tool_calls:
                remaining = deadline - _now()
                if used >= MAX_TOOL_CALLS or remaining <= 0:
                    return _insufficient(provider, calls, new)
                used += 1
                ids: list[str] = []
                try:
                    if tc.name not in TOOL_NAMES:
                        raise ValueError(f"알 수 없는 도구: {tc.name}")
                    api.timeout = max(0.001, min(HTTP_TIMEOUT_S, remaining))
                    found = await asyncio.wait_for(tools.run(tc.name, tc.arguments), timeout=remaining)
                    for e in found:
                        ids.append(e.id)
                        if e.id not in known:
                            new.setdefault(e.id, e)
                    summary = f"{len(found)}건 확보" if found else "결과 없음"
                    result = "\n\n".join(_render(e) for e in found) or "결과 없음"
                except TimeoutError:
                    return _insufficient(provider, calls, new)
                except (LawApiError, ValueError) as e:  # messages are URL/OC-free by construction
                    summary = f"오류: {e}"
                    result = summary
                except Exception as e:  # noqa: BLE001 - tool failures never abort the loop
                    summary = f"오류: {type(e).__name__}"
                    result = summary
                calls.append(ToolCallRecord(seq=used, tool=tc.name, args=tc.arguments if isinstance(tc.arguments, dict) else {}, result_summary=summary, evidence_ids=ids))
                messages.append({"role": "tool", "tool_call_id": tc.id, "content": result})
            continue

        # no tool calls -> the model believes it is done
        messages.append({"role": "assistant", "content": resp.content or ""})
        if not known and not new:
            nudges += 1
            if nudges > MAX_NUDGES:
                return _insufficient(provider, calls, new)
            messages.append({"role": "user", "content": f"오류: {EMPTY_EVIDENCE_ERROR}"})
            continue
        try:
            final = _parse(resp.content)
        except OutputInvalid:
            nudges += 1
            if nudges > MAX_NUDGES:
                raise
            messages.append(
                {"role": "user", "content": "최종 답을 지정된 JSON 형식으로 출력하거나, 근거가 부족하면 도구로 더 확보하세요."}
            )
            continue
        collected = known | set(new)
        outside = sorted({c for s in final.memo for c in s.citations} - collected)
        if outside and not corrected:
            corrected = True
            messages.append(
                {
                    "role": "user",
                    "content": f"다음 인용 ID는 확보한 근거에 없습니다: {', '.join(outside)}. "
                    f"확보한 근거 ID({', '.join(sorted(collected))})만 인용하도록 고쳐서 다시 JSON으로 답하세요.",
                }
            )
            continue
        return AnalyzeResponse(
            status="ok" if final.memo else "insufficient_evidence",
            memo=final.memo, open_questions=final.open_questions,
            tool_calls=calls, new_evidence=list(new.values()), provider=provider,
        )
