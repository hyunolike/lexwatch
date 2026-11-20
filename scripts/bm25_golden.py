"""BM25 동등성 골든 파일을 만든다(hybrid-rag 작업 6).

실행: cd ai && uv run python ../scripts/bm25_golden.py
녹화 법령(근로기준법) 조문 본문을 문서로, ai/app/rag/lexical.py의 BM25 점수를 정답으로 저장한다.
백엔드 HybridSearch(SQL BM25)가 같은 순서와 점수를 내는지 C# 테스트가 확인한다.
"""

from __future__ import annotations

import asyncio
import json
import math
import sys
from collections import Counter
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ai"))

from app.lawapi import LawApi  # noqa: E402
from app.rag.lexical import LexicalRetrieverProvider, tokenize  # noqa: E402

REC = ROOT / "fixtures" / "lawapi" / "recorded" / "current"
OUT = ROOT / "fixtures" / "retrieval" / "bm25_golden.json"
QUERIES = [
    "연장근로 가산임금",
    "휴일에 일하면 임금을 더 줘야 하나",
    "1주 근로시간 40시간",
    "해고 예고",
    "임산부 야간근로 제한",
    "연차 유급휴가 일수",
]


def scores(query: str, passages: list[str], k1: float = 1.4, b: float = 0.75) -> list[float]:
    """lexical.py rerank와 같은 공식. 순서가 같은지 아래에서 rerank로 확인한다."""
    docs = [Counter(tokenize(p)) for p in passages]
    n = len(docs)
    lengths = [sum(d.values()) for d in docs]
    avgdl = sum(lengths) / max(1, n) or 1.0
    df: Counter[str] = Counter()
    for d in docs:
        df.update(d.keys())
    out = []
    for i, d in enumerate(docs):
        s = 0.0
        for t in tokenize(query):
            if t in d:
                idf = math.log(1 + (n - df[t] + 0.5) / (df[t] + 0.5))
                tf = d[t]
                s += idf * tf * (k1 + 1) / (tf + k1 * (1 - b + b * lengths[i] / avgdl))
        out.append(s)
    return out


async def main() -> None:
    def handler(req: httpx.Request) -> httpx.Response:
        return httpx.Response(200, text=(REC / f"law_{req.url.params['ID']}.json").read_text(encoding="utf-8"))

    arts = await LawApi(httpx.AsyncClient(transport=httpx.MockTransport(handler)), oc="x").law_articles("001872")
    passages = [a["body"] for a in list(arts.values())[:60]]
    cases = []
    for q in QUERIES:
        sc = scores(q, passages)
        order = await LexicalRetrieverProvider().rerank(q, passages)
        assert order == sorted(range(len(passages)), key=lambda i: (-sc[i], i)), q
        cases.append({"query": q, "expected": [{"index": i, "score": sc[i]} for i in order if sc[i] > 0]})
    OUT.write_text(json.dumps({"passages": passages, "cases": cases}, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"{len(passages)} passages, {len(cases)} queries -> {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    asyncio.run(main())
