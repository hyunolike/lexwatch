from __future__ import annotations

import os

from app.rag.base import RetrieverProvider
from app.rag.lexical import LexicalRetrieverProvider
from app.rag.nvidia import NvidiaRetrieverProvider


def get_retriever() -> RetrieverProvider:
    mode = os.environ.get("RETRIEVER_PROVIDER", "auto").lower()
    key = os.environ.get("NVIDIA_API_KEY", "").strip()
    if mode == "lexical" or not key:
        return LexicalRetrieverProvider()
    return NvidiaRetrieverProvider.from_env(key)
