import httpx
import pytest

from app.rag.base import RetrieverError
from app.rag.factory import get_retriever
from app.rag.nvidia import NvidiaRetrieverProvider


async def test_lexical_fallback_when_no_key(monkeypatch):
    monkeypatch.delenv("NVIDIA_API_KEY", raising=False)
    r = get_retriever()
    assert r.info.kind == "fallback"
    order = await r.rerank("개인정보 안전조치", ["날씨 이야기", "개인정보 안전조치 의무", "기타"])
    assert order[0] == 1


async def test_nvidia_embed_and_rerank():
    def handler(req):
        if req.url.path.endswith("/embeddings"):
            return httpx.Response(200, json={"data": [
                {"index": 1, "embedding": [2.0]}, {"index": 0, "embedding": [1.0]}]})
        assert str(req.url).endswith("llama-nemotron-rerank-vl-1b-v2/reranking")
        return httpx.Response(200, json={"rankings": [{"index": 0, "logit": -1}, {"index": 1, "logit": 3}]})

    p = NvidiaRetrieverProvider("k", transport=httpx.MockTransport(handler))
    assert await p.embed(["a", "b"], "passage") == [[1.0], [2.0]]
    assert await p.rerank("q", ["a", "b"]) == [1, 0]


async def test_rerank_returns_full_permutation_skipping_malformed():
    body = {"rankings": [{"index": 2, "logit": 1}, {"nope": 1}, {"index": 9, "logit": 5}, {"index": 2, "logit": 0}]}
    p = NvidiaRetrieverProvider("k", transport=httpx.MockTransport(lambda r: httpx.Response(200, json=body)))
    assert await p.rerank("q", ["a", "b", "c"]) == [2, 0, 1]


async def test_malformed_responses_raise_retriever_error():
    p = NvidiaRetrieverProvider("k", transport=httpx.MockTransport(lambda r: httpx.Response(200, json={"x": 1})))
    with pytest.raises(RetrieverError):
        await p.embed(["a"], "query")
    with pytest.raises(RetrieverError):
        await p.rerank("q", ["a"])
