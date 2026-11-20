"""Cloudflare bge-m3 임베딩 공급자와 대체(hybrid-rag 작업 12, design 6절)."""

import json

import httpx
import pytest

from app.rag.cloudflare import CLOUDFLARE_EMBED_MODEL, CloudflareRetrieverProvider, FallbackRetriever
from app.rag.factory import get_retriever
from app.rag.lexical import LexicalRetrieverProvider

ENV = ("RETRIEVER_PROVIDER", "NVIDIA_API_KEY", "CLOUDFLARE_API_TOKEN", "CLOUDFLARE_ACCOUNT_ID")


@pytest.fixture
def env(monkeypatch):
    for k in ENV:
        monkeypatch.delenv(k, raising=False)
    return monkeypatch


def cf(handler):
    return CloudflareRetrieverProvider("tok", "acct", transport=httpx.MockTransport(handler))


async def test_embed_calls_openai_compatible_endpoint():
    seen = {}

    def handler(req):
        seen["url"], seen["auth"], seen["body"] = str(req.url), req.headers["authorization"], json.loads(req.content)
        return httpx.Response(200, json={"data": [{"index": 1, "embedding": [0.0, 1.0]}, {"index": 0, "embedding": [1.0, 0.0]}]})

    p = cf(handler)
    vs = await p.embed(["가", "나"], "passage")
    assert seen["url"] == "https://api.cloudflare.com/client/v4/accounts/acct/ai/v1/embeddings"
    assert seen["auth"] == "Bearer tok"
    assert seen["body"] == {"model": CLOUDFLARE_EMBED_MODEL, "input": ["가", "나"]}
    assert vs == [[1.0, 0.0], [0.0, 1.0]]
    assert (p.info.kind, p.info.name) == ("cloudflare", CLOUDFLARE_EMBED_MODEL)


async def test_rerank_is_lexical_bm25():
    p = cf(lambda req: httpx.Response(500))
    passages = ["휴게시간", "연장근로 가산임금", "야간근로"]
    assert await p.rerank("가산임금", passages) == await LexicalRetrieverProvider().rerank("가산임금", passages)


@pytest.mark.parametrize("status", [429, 500, 503])
async def test_failures_fall_back_to_lexical_and_say_so(status):
    r = FallbackRetriever(cf(lambda req: httpx.Response(status, json={"errors": [{"code": 4006}]})), LexicalRetrieverProvider())
    vs = await r.embed(["가산임금"], "query")
    assert len(vs[0]) == 256
    assert r.info.kind == "fallback"


async def test_fallback_keeps_primary_info_on_success():
    r = FallbackRetriever(cf(lambda req: httpx.Response(200, json={"data": [{"index": 0, "embedding": [1.0]}]})), LexicalRetrieverProvider())
    await r.embed(["가"], "query")
    assert r.info.kind == "cloudflare"


def test_selection_order(env):
    assert isinstance(get_retriever(), LexicalRetrieverProvider)
    env.setenv("NVIDIA_API_KEY", "nv")
    assert get_retriever().info.kind == "nvidia"
    env.setenv("CLOUDFLARE_API_TOKEN", "cf")
    env.setenv("CLOUDFLARE_ACCOUNT_ID", "acct")
    r = get_retriever()
    assert isinstance(r, FallbackRetriever) and r.info.kind == "cloudflare"   # 검색은 Cloudflare 우선(1,024차원 열)
    env.setenv("RETRIEVER_PROVIDER", "lexical")
    assert isinstance(get_retriever(), LexicalRetrieverProvider)
