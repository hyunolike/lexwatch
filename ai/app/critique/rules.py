"""과잉해석(OC) 규칙: 문구와, 키 없을 때 쓰는 패턴 판정을 한 곳에 둔다."""

from __future__ import annotations

import re
from collections.abc import Callable

from app.schemas import Evidence

RULES: dict[str, str] = {
    "OC-01": "경과규정(부칙) 근거 없이 시행일 이전 계약·행위에 새 규정이 적용된다고 단정한다.",
    "OC-02": "하위법령(시행령·고시 등)이 확인되지 않은 위임 조항을 구체적 의무처럼 기술한다.",
    "OC-03": "판례 하나를 일반 원칙처럼 기술한다.",
    "OC-04": "'반드시'·'모든' 등으로 근거 조문보다 적용 범위를 넓힌다.",
    "OC-05": "벌칙·과태료 금액을 개정 전(구조문) 조문에서 인용한다.",
}

Detector = Callable[[str, list[Evidence]], bool]

_OC01 = re.compile(r"시행일?\s*(이전|전)에?\s*(체결|발생|행해진|이루어진|한)")
_OC02_WORD = re.compile(r"위임|대통령령으로 정하는|시행령으로 정하는")
_OC02_CONCRETE = re.compile(r"\d+\s*(일|시간|%|퍼센트|개월|년|만\s?원|억\s?원)|제출|보고|구축|보관|하여야 한다|해야 한다")
_OC03 = re.compile(r"(판례|판결|대법원).*(항상|일반적으로|원칙)")
_OC04 = re.compile(r"반드시|모든\s*(기업|사업자|근로자|개인정보처리자|금융회사|계약)")
_AMOUNT = re.compile(r"\d[\d,]*\s*(천만|백만|만|억)?\s*원")
_PENALTY = re.compile(r"과태료|벌금|벌칙|과징금")


def _has_transition(evidence: list[Evidence]) -> bool:
    return any(re.search(r"부칙|경과", e.body + e.title) for e in evidence)


def _oc01(s: str, ev: list[Evidence]) -> bool:
    return bool(_OC01.search(s)) and not _has_transition(ev)


def _oc02(s: str, ev: list[Evidence]) -> bool:
    if not _OC02_WORD.search(s) or not _OC02_CONCRETE.search(s):
        return False
    return not any(e.kind == "subordinate" or e.id.startswith("sub:") for e in ev)


def _oc03(s: str, ev: list[Evidence]) -> bool:
    return bool(_OC03.search(s))


def _oc04(s: str, ev: list[Evidence]) -> bool:
    return bool(_OC04.search(s))


def _oc05(s: str, ev: list[Evidence]) -> bool:
    if not (_AMOUNT.search(s) and _PENALTY.search(s)) or not ev:
        return False
    if not all(e.kind == "old_new" or e.id.startswith("oldnew:") for e in ev):
        return False
    new_side = [e.body.split("[신조문]", 1)[1] if "[신조문]" in e.body else "" for e in ev]
    amount = _AMOUNT.search(s).group(0).replace(" ", "")  # type: ignore[union-attr]
    return not any(amount in n.replace(" ", "") for n in new_side)


PATTERNS: dict[str, Detector] = {
    "OC-01": _oc01,
    "OC-02": _oc02,
    "OC-03": _oc03,
    "OC-04": _oc04,
    "OC-05": _oc05,
}

PATTERN_MESSAGE = "패턴 판정(키 없음): "


def rules_text() -> str:
    return "\n".join(f"- {k}: {v}" for k, v in RULES.items())
