"""NVIDIA NeMo Retriever adapter (hosted embedding + reranking NIMs).

Adapted from hyunolike/reroute-irops-agent (apps/api/app/rag/nvidia.py).
Embeddings: POST {NIM_BASE_URL}/embeddings ; Rerank: POST RERANK_URL -> {"rankings":[{"index","logit"}]}
"""

from __future__ import annotations

import os
from typing import Literal

import httpx

from app.rag.base import RetrieverError, RetrieverProvider
from app.schemas import ProviderInfo

DEFAULT_RERANK_URL = (
    "https://ai.api.nvidia.com/v1/retrieval/nvidia/llama-nemotron-rerank-vl-1b-v2/reranking"
)


class NvidiaRetrieverProvider(RetrieverProvider):
    info = ProviderInfo(name="nemo-retriever", kind="nvidia")

    def __init__(
        self,
        api_key: str,
        base_url: str = "https://integrate.api.nvidia.com/v1",
        embed_model: str = "nvidia/nemotron-3-embed-1b",
        rerank_url: str = DEFAULT_RERANK_URL,
        rerank_model: str = "nvidia/llama-nemotron-rerank-vl-1b-v2",
        timeout: float = 30.0,
        transport: httpx.AsyncBaseTransport | None = None,
    ) -> None:
        self.api_key = api_key
        self.embed_url = f"{base_url.rstrip('/')}/embeddings"
        self.embed_model = embed_model
        self.rerank_url = rerank_url
        self.rerank_model = rerank_model
        self.timeout = timeout
        self.transport = transport

    @classmethod
    def from_env(cls, api_key: str) -> NvidiaRetrieverProvider:
        return cls(
            api_key,
            os.environ.get("NIM_BASE_URL", "https://integrate.api.nvidia.com/v1"),
            os.environ.get("EMBED_MODEL", "nvidia/nemotron-3-embed-1b"),
            os.environ.get("RERANK_URL", DEFAULT_RERANK_URL),
            os.environ.get("RERANK_MODEL", "nvidia/llama-nemotron-rerank-vl-1b-v2"),
        )

    def _client(self) -> httpx.AsyncClient:
        return httpx.AsyncClient(
            timeout=self.timeout,
            transport=self.transport,
            headers={"Authorization": f"Bearer {self.api_key}", "Accept": "application/json"},
        )

    async def embed(self, texts: list[str], input_type: Literal["query", "passage"]) -> list[list[float]]:
        try:
            async with self._client() as c:
                r = await c.post(
                    self.embed_url,
                    json={
                        "model": self.embed_model,
                        "input": texts,
                        "input_type": input_type,
                        "encoding_format": "float",
                        "truncate": "END",
                    },
                )
        except httpx.HTTPError as e:
            raise RetrieverError(f"embedding NIM unreachable: {e}") from e
        if r.status_code != 200:
            raise RetrieverError(f"embedding NIM {r.status_code}: {r.text[:200]}")
        data = sorted(r.json()["data"], key=lambda d: d["index"])
        return [d["embedding"] for d in data]

    async def rerank(self, query: str, passages: list[str]) -> list[int]:
        try:
            async with self._client() as c:
                r = await c.post(
                    self.rerank_url,
                    json={
                        "model": self.rerank_model,
                        "query": {"text": query},
                        "passages": [{"text": p} for p in passages],
                    },
                )
        except httpx.HTTPError as e:
            raise RetrieverError(f"rerank NIM unreachable: {e}") from e
        if r.status_code != 200:
            raise RetrieverError(f"rerank NIM {r.status_code}: {r.text[:200]}")
        ranks = sorted(r.json()["rankings"], key=lambda x: -float(x["logit"]))
        return [int(x["index"]) for x in ranks]
