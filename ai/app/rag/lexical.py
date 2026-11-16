"""Deterministic offline retriever: hashed bag-of-words embeddings + BM25 rerank. No network.

Adapted from hyunolike/reroute-irops-agent (apps/api/app/rag/lexical.py), tokenizer made Korean-aware.
"""

from __future__ import annotations

import hashlib
import math
import re
from collections import Counter
from typing import Literal

from app.rag.base import RetrieverProvider
from app.schemas import ProviderInfo

DIM = 256


def tokenize(text: str) -> list[str]:
    toks: list[str] = []
    for w in re.findall(r"[0-9a-z가-힣]+", text.lower()):
        toks.append(w)
        if len(w) > 1 and re.search(r"[가-힣]", w):
            toks += [w[i : i + 2] for i in range(len(w) - 1)]  # char bigrams for Korean
    return toks


class LexicalRetrieverProvider(RetrieverProvider):
    info = ProviderInfo(name="lexical-bm25", kind="fallback")

    def __init__(self, k1: float = 1.4, b: float = 0.75) -> None:
        self.k1, self.b = k1, b

    async def embed(self, texts: list[str], input_type: Literal["query", "passage"]) -> list[list[float]]:
        out = []
        for t in texts:
            v = [0.0] * DIM
            for tok in tokenize(t):
                v[int(hashlib.md5(tok.encode()).hexdigest(), 16) % DIM] += 1.0
            norm = math.sqrt(sum(x * x for x in v)) or 1.0
            out.append([x / norm for x in v])
        return out

    async def rerank(self, query: str, passages: list[str]) -> list[int]:
        docs = [Counter(tokenize(p)) for p in passages]
        n = len(docs)
        lengths = [sum(d.values()) for d in docs]
        avgdl = sum(lengths) / max(1, n) or 1.0
        df: Counter[str] = Counter()
        for d in docs:
            df.update(d.keys())
        q = tokenize(query)
        scores = []
        for i, d in enumerate(docs):
            s = 0.0
            for t in q:
                if t not in d:
                    continue
                idf = math.log(1 + (n - df[t] + 0.5) / (df[t] + 0.5))
                tf = d[t]
                s += idf * tf * (self.k1 + 1) / (tf + self.k1 * (1 - self.b + self.b * lengths[i] / avgdl))
            scores.append((-s, i))
        scores.sort()
        return [i for _, i in scores]
