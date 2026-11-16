"""국가법령정보센터 DRF client. All response-shape knowledge lives here.

Shapes follow docs/notes/2026-11-16-spike.md (일부 추정 — 실호출로 확정 필요).
OC comes from env LAW_OC and is never logged or echoed.
"""

from __future__ import annotations

import os
import re
from typing import Any

import httpx

BASE = "https://www.law.go.kr/DRF/"
_ART_NO = re.compile(r"제\s*(\d+)\s*조(?:\s*의\s*(\d+))?")


class LawApiError(RuntimeError):
    pass


def article_key(text: str) -> str | None:
    """'제50조(…)' -> '50', '제50조의2' -> '50의2'."""
    m = _ART_NO.search(text)
    if not m:
        return None
    return m.group(1) + (f"의{m.group(2)}" if m.group(2) else "")


def _as_list(v: Any) -> list[Any]:
    if v is None:
        return []
    return v if isinstance(v, list) else [v]


def _check(data: Any) -> dict[str, Any]:
    if not isinstance(data, dict):
        raise LawApiError("법령 API 응답 형식 오류")
    if "result" in data:  # errors arrive as HTTP 200 with a top-level "result"
        raise LawApiError("법령 API 오류 응답")
    return data


class LawApi:
    def __init__(self, client: httpx.AsyncClient | None = None, oc: str | None = None, timeout: float = 15.0):
        self._client = client
        self._oc = oc if oc is not None else os.environ.get("LAW_OC", "")
        self._timeout = timeout

    async def _get(self, endpoint: str, params: dict[str, str]) -> dict[str, Any]:
        if not self._oc:
            raise LawApiError("LAW_OC 미설정")
        q = {"OC": self._oc, "type": "JSON", **params}
        own = self._client is None
        client = self._client or httpx.AsyncClient(timeout=self._timeout)
        try:
            r = await client.get(BASE + endpoint, params=q)
        except httpx.HTTPError as e:
            raise LawApiError(f"법령 API 호출 실패: {type(e).__name__}") from None  # no URL (contains OC)
        finally:
            if own:
                await client.aclose()
        if r.status_code != 200:
            raise LawApiError(f"법령 API HTTP {r.status_code}")
        try:
            return _check(r.json())
        except ValueError:
            raise LawApiError("법령 API 응답이 JSON이 아님(XML/HTML 오류 가능)") from None

    async def law_articles(self, law_id: str) -> dict[str, dict[str, str]]:
        data = await self._get("lawService.do", {"target": "law", "ID": law_id})
        units = _as_list((data.get("법령") or {}).get("조문", {}).get("조문단위"))
        out: dict[str, dict[str, str]] = {}
        for u in units:
            if u.get("조문여부") not in (None, "조문"):
                continue
            no = str(u.get("조문번호", "")).strip()
            if not no:
                continue
            key = no + (f"의{u['조문가지번호']}" if u.get("조문가지번호") else "")
            out[key] = {"title": u.get("조문제목", ""), "body": u.get("조문내용", "")}
        if not out:
            raise LawApiError("조문을 찾을 수 없음")
        return out

    async def old_new(self, law_id: str) -> dict[str, dict[str, str]]:
        data = await self._get("lawService.do", {"target": "oldAndNew", "ID": law_id})
        svc = data.get("OldAndNewService") or {}
        old = {article_key(s): s for s in _as_list((svc.get("구조문목록") or {}).get("조문")) if article_key(s)}
        new = {article_key(s): s for s in _as_list((svc.get("신조문목록") or {}).get("조문")) if article_key(s)}
        keys = list(dict.fromkeys([*new, *old]))
        if not keys:
            raise LawApiError("신구조문 없음")
        return {k: {"old": old.get(k, ""), "new": new.get(k, "")} for k in keys}

    async def search_admrul(self, query: str) -> list[dict[str, str]]:
        data = await self._get("lawSearch.do", {"target": "admrul", "query": query, "display": "20"})
        rows = _as_list((data.get("AdmRulSearch") or {}).get("admrul"))
        return [
            {
                "id": str(r.get("행정규칙일련번호", "")),
                "title": r.get("행정규칙명", ""),
                "body": f"{r.get('행정규칙명', '')} ({r.get('행정규칙종류', '')}, 소관 {r.get('소관부처명', '')}, "
                f"시행 {r.get('시행일자', '')})",
            }
            for r in rows
            if r.get("행정규칙일련번호")
        ]

    async def search_prec(self, query: str) -> list[dict[str, str]]:
        data = await self._get("lawSearch.do", {"target": "prec", "query": query, "display": "20"})
        rows = _as_list((data.get("PrecSearch") or {}).get("prec"))
        return [
            {
                "id": str(r.get("판례일련번호", "")),
                "title": r.get("사건명", ""),
                "body": f"{r.get('사건명', '')} ({r.get('법원명', '')}, {r.get('선고일자', '')}, "
                f"{r.get('사건번호', '')})",
            }
            for r in rows
            if r.get("판례일련번호")
        ]
