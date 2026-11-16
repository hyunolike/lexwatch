import httpx

from app.main import app


async def test_embed_returns_provider_info(monkeypatch):
    monkeypatch.delenv("NVIDIA_API_KEY", raising=False)
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://t") as c:
        assert (await c.get("/health")).json() == {"status": "ok"}
        r = await c.post("/embed", json={"texts": ["개인정보"], "input_type": "query"})
    body = r.json()
    assert r.status_code == 200
    assert body["provider"] == {"name": "lexical-bm25", "kind": "fallback"}
    assert len(body["vectors"]) == 1


async def test_error_handlers_map_contract_codes():
    from fastapi import FastAPI

    from app.errors import register_handlers
    from app.providers.llm.base import OutputInvalid, RateLimited
    from app.rag.base import RetrieverError

    a = FastAPI()
    register_handlers(a)

    @a.get("/{k}")
    async def boom(k: str):
        raise {"rl": RateLimited(3.0), "oi": OutputInvalid("bad"), "re": RetrieverError("x")}[k]

    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=a), base_url="http://t") as c:
        r = await c.get("/rl")
        assert (r.status_code, r.json()) == (429, {"code": "LLM_RATE_LIMITED", "retry_after_s": 3.0})
        r = await c.get("/oi")
        assert (r.status_code, r.json()) == (422, {"code": "LLM_OUTPUT_INVALID", "detail": "bad"})
        r = await c.get("/re")
        assert r.status_code == 502 and r.json()["code"] == "RETRIEVER_ERROR"
