"""Cloudflare Workers AI bge-m3 임베딩(hybrid-rag design 6절).

OpenAI 호환 /ai/v1/embeddings를 쓴다. 재순위는 지금처럼 BM25(lexical)에 맡긴다.
FallbackRetriever는 Cloudflare가 실패하면(한도 초과, 5xx, 연결) lexical 임베딩으로 바꾸고 출처를 fallback으로 단다.
그래서 /embed는 실패하지 않고, 백엔드는 출처와 차원으로 저하를 알아챈다.
"""

from __future__ import annotations

from typing import Literal

import httpx

from app.rag.base import RetrieverError, RetrieverProvider
from app.rag.lexical import LexicalRetrieverProvider
from app.rag.nvidia import NvidiaRetrieverProvider
from app.schemas import ProviderInfo

CLOUDFLARE_EMBED_MODEL = "@cf/baai/bge-m3"


class CloudflareRetrieverProvider(NvidiaRetrieverProvider):
    info = ProviderInfo(name=CLOUDFLARE_EMBED_MODEL, kind="cloudflare")

    def __init__(self, api_token: str, account_id: str, *, transport: httpx.AsyncBaseTransport | None = None) -> None:
        super().__init__(
            api_token,
            base_url=f"https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1",
            embed_model=CLOUDFLARE_EMBED_MODEL,
            transport=transport,
        )

    async def embed(self, texts: list[str], input_type: Literal["query", "passage"]) -> list[list[float]]:
        # bge-m3는 질의와 문서에 접두가 필요 없다. NVIDIA 전용 필드(input_type, truncate)는 보내지 않는다.
        try:
            async with self._client() as c:
                r = await c.post(self.embed_url, json={"model": self.embed_model, "input": texts})
        except httpx.HTTPError as e:
            raise RetrieverError(f"Cloudflare embeddings unreachable: {e}") from e
        if r.status_code != 200:
            raise RetrieverError(f"Cloudflare embeddings {r.status_code}: {r.text[:200]}")
        try:
            data = sorted(r.json()["data"], key=lambda d: d["index"])
            vectors = [[float(x) for x in d["embedding"]] for d in data]
        except (ValueError, KeyError, TypeError) as e:
            raise RetrieverError(f"malformed Cloudflare embedding response: {e}") from e
        if len(vectors) != len(texts):
            raise RetrieverError("embedding count mismatch")
        return vectors

    async def rerank(self, query: str, passages: list[str]) -> list[int]:
        return await LexicalRetrieverProvider().rerank(query, passages)


class FallbackRetriever(RetrieverProvider):
    """요청마다 새로 만든다(get_retriever). info는 마지막 embed에 실제로 쓴 공급자를 가리킨다."""

    def __init__(self, primary: RetrieverProvider, fallback: RetrieverProvider) -> None:
        self.primary, self.fallback = primary, fallback
        self.info = primary.info

    async def embed(self, texts: list[str], input_type: Literal["query", "passage"]) -> list[list[float]]:
        try:
            out = await self.primary.embed(texts, input_type)
            self.info = self.primary.info
            return out
        except RetrieverError:
            self.info = self.fallback.info
            return await self.fallback.embed(texts, input_type)

    async def rerank(self, query: str, passages: list[str]) -> list[int]:
        return await self.primary.rerank(query, passages)
