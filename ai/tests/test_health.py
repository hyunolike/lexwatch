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
