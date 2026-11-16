from __future__ import annotations

from typing import Any

from app.agent.prompts import TRUNC_MARK
from app.lawapi import LawApi, LawApiError
from app.rag.base import RetrieverProvider
from app.schemas import Evidence

BODY_LIMIT = 4000
TOP_K = 5
EMPTY_EVIDENCE_ERROR = "먼저 get_article 또는 compare_old_new로 조문을 확보하세요"


def clip(text: str, limit: int = BODY_LIMIT) -> str:
    return text if len(text) <= limit else text[:limit] + TRUNC_MARK


def _fn(name: str, desc: str, props: dict[str, Any], required: list[str]) -> dict[str, Any]:
    return {
        "type": "function",
        "function": {
            "name": name,
            "description": desc,
            "parameters": {"type": "object", "properties": props, "required": required},
        },
    }


_STR = {"type": "string"}
TOOL_SCHEMAS: list[dict[str, Any]] = [
    _fn("get_article", "법령ID와 조문번호로 조문 원문을 가져온다.", {"law_id": _STR, "article_no": _STR}, ["law_id", "article_no"]),
    _fn("compare_old_new", "법령의 신구조문 대비표를 가져온다.", {"law_id": _STR}, ["law_id"]),
    _fn("search_subordinate", "행정규칙(하위규범)을 검색한다.", {"query": _STR}, ["query"]),
    _fn("search_precedent", "관련 판례를 검색한다.", {"query": _STR}, ["query"]),
    _fn("get_group_interest", "이 실무그룹의 관심 분야 설명을 가져온다.", {}, []),
]
TOOL_NAMES = {t["function"]["name"] for t in TOOL_SCHEMAS}


class Tools:
    def __init__(self, api: LawApi, retriever: RetrieverProvider, group_code: str, interest_text: str):
        self.api, self.retriever = api, retriever
        self.group_code, self.interest_text = group_code, interest_text

    async def run(self, name: str, args: dict[str, Any]) -> list[Evidence]:
        """Raises LawApiError / ValueError on failure; the loop turns that into a tool-error result."""
        if name == "get_article":
            law_id, no = str(args.get("law_id", "")), str(args.get("article_no", "")).strip()
            no = no.replace("제", "").replace("조", "").strip()
            if not law_id or not no:
                raise ValueError("law_id와 article_no가 필요합니다")
            arts = await self.api.law_articles(law_id)
            if no not in arts:
                raise LawApiError(f"조문 {no} 없음")
            a = arts[no]
            return [Evidence(id=f"law:{law_id}:{no}", kind="law_article", title=a["title"] or f"제{no}조", body=clip(a["body"]))]
        if name == "compare_old_new":
            law_id = str(args.get("law_id", ""))
            if not law_id:
                raise ValueError("law_id가 필요합니다")
            pairs = await self.api.old_new(law_id)
            return [
                Evidence(
                    id=f"oldnew:{law_id}:{k}",
                    kind="old_new",
                    title=f"제{k}조 신구조문 대비",
                    body=clip(f"[구조문] {p['old'] or '(없음)'}\n[신조문] {p['new'] or '(없음)'}"),
                )
                for k, p in pairs.items()
            ]
        if name == "search_subordinate":
            q = str(args.get("query", ""))
            rows = await self.api.search_admrul(q)
            return await self._top(q, rows, lambda r: (f"sub:{r['id']}:summary", "subordinate"))
        if name == "search_precedent":
            q = str(args.get("query", ""))
            rows = await self.api.search_prec(q)
            return await self._top(q, rows, lambda r: (f"prec:{r['id']}", "precedent"))
        if name == "get_group_interest":
            return [
                Evidence(
                    id=f"group:{self.group_code}",
                    kind="group_interest",
                    title=f"실무그룹 {self.group_code} 관심 분야",
                    body=clip(self.interest_text),
                )
            ]
        raise ValueError(f"알 수 없는 도구: {name}")

    async def _top(self, query: str, rows: list[dict[str, str]], ident: Any) -> list[Evidence]:
        if not rows:
            return []
        order = await self.retriever.rerank(query, [r["body"] for r in rows])
        out = []
        for i in order[:TOP_K]:
            r = rows[i]
            eid, kind = ident(r)
            out.append(Evidence(id=eid, kind=kind, title=r["title"], body=clip(r["body"])))
        return out
