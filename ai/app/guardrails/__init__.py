"""Input rails for the review-assistant chat.

Active engine: deterministic rules (this file). NVIDIA NeMo Guardrails is NOT installed in the
deployed bundle; ``config.yml`` / ``rails.co`` in this folder are its declarative twin, kept for the
optional ``guardrails`` dependency group and are not executed by the service. ``ENGINE`` is what
logs and docs report, so nothing claims NeMo is active when it is not.
"""

from __future__ import annotations

import re
from collections.abc import Sequence

from app.schemas import Evidence

ENGINE = "deterministic-input-rails"
REFUSAL = "이 개정안의 근거 안에서만 답할 수 있어요."

_INJECTION = re.compile(
    r"(이전|위의|앞의|기존)\s*(지시|명령|규칙|프롬프트).{0,6}(무시|잊)|ignore\s+(all\s+)?(previous|prior)|system\s*prompt|시스템\s*프롬프트|프롬프트.{0,4}(보여|알려|출력)",
    re.I,
)
_LEGAL_ADVICE = re.compile(
    r"소송|승소|패소|고소|고발|형사|처벌|법적\s*책임|손해배상|자문|변호사\s*(없이|대신)|"
    r"(우리|저희|제)\s*(회사|기업|계약|사건).{0,12}(적용|해당|위반)|"
    r"위반(인가|일까|이냐|입니까|이 되나|인지)|(해야|하면)\s*(하나요|할까요|되나요|돼요)"
)
_DOMAIN = re.compile(r"법령|법률|개정|조문|조항|신구|대비|시행|공포|인용|근거|제\s*\d+\s*조|메모|문장|규정|변경|영향|항목")
_STOP = {"무엇", "어떻", "어떤", "알려", "설명", "대해", "해줘", "주세", "있나", "있는", "이란", "어때", "무슨", "이거", "저거"}


def _shingles(text: str) -> set[str]:
    out: set[str] = set()
    for tok in re.findall(r"[가-힣A-Za-z0-9]+", text):
        for i in range(len(tok) - 1):
            out.add(tok[i : i + 2])
    return out - _STOP


def check_input(question: str, evidence: Sequence[Evidence]) -> str | None:
    """Return a rail name when the question must be refused, else None."""
    if _INJECTION.search(question):
        return "injection"
    if _LEGAL_ADVICE.search(question):
        return "legal_advice"
    if _DOMAIN.search(question):
        return None
    ev = _shingles(" ".join(f"{e.title} {e.body}" for e in evidence))
    return None if _shingles(question) & ev else "out_of_scope"
