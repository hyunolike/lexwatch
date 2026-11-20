from __future__ import annotations

import os

from app.rag.base import RetrieverProvider
from app.rag.cloudflare import CloudflareRetrieverProvider, FallbackRetriever
from app.rag.lexical import LexicalRetrieverProvider
from app.rag.nvidia import NvidiaRetrieverProvider


def get_retriever() -> RetrieverProvider:
    """RETRIEVER_PROVIDER: auto(기본), cloudflare, nvidia, lexical.

    auto는 Cloudflare 값이 있으면 Cloudflare(bge-m3, 1,024차원, 실패 시 lexical 대체), 없고 NVIDIA 키가 있으면 NVIDIA,
    둘 다 없으면 lexical. LLM 선택 순서(NVIDIA → Cloudflare)와 반대인 것은 의도다: 조문 청크 열이 1,024차원이고
    NVIDIA 임베딩은 2,048차원이라 저장되지 않는다(docs/features/hybrid-rag/design.md 6절).
    """
    mode = os.environ.get("RETRIEVER_PROVIDER", "auto").lower()
    if mode == "lexical":
        return LexicalRetrieverProvider()
    token = os.environ.get("CLOUDFLARE_API_TOKEN", "").strip()
    account = os.environ.get("CLOUDFLARE_ACCOUNT_ID", "").strip()
    key = os.environ.get("NVIDIA_API_KEY", "").strip()
    if mode in ("auto", "cloudflare") and token and account:
        return FallbackRetriever(CloudflareRetrieverProvider(token, account), LexicalRetrieverProvider())
    if mode in ("auto", "nvidia") and key:
        return NvidiaRetrieverProvider.from_env(key)
    return LexicalRetrieverProvider()
