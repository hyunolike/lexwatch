import httpx
import pytest

from app.main import app


def _client():
    return httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://t")


@pytest.fixture
def no_key(monkeypatch):
    monkeypatch.delenv("NVIDIA_API_KEY", raising=False)
    monkeypatch.delenv("AI_TOKEN", raising=False)
    monkeypatch.delenv("AI_REQUIRE_TOKEN", raising=False)


EMBED = {"texts": ["개인정보"], "input_type": "query"}


async def test_without_token_configured_service_is_open(no_key):
    async with _client() as c:
        assert (await c.post("/embed", json=EMBED)).status_code == 200
        assert (await c.get("/openapi.json")).status_code == 200


async def test_require_token_without_token_fails_closed(no_key, monkeypatch):
    monkeypatch.setenv("AI_REQUIRE_TOKEN", "true")
    async with _client() as c:
        r = await c.post("/embed", json=EMBED)
        assert r.status_code == 503 and r.json() == {"code": "AI_TOKEN_NOT_CONFIGURED"}
        assert (await c.get("/health")).status_code == 200


async def test_token_required_on_every_path_but_health(no_key, monkeypatch):
    monkeypatch.setenv("AI_TOKEN", "s3cret-token")
    async with _client() as c:
        assert (await c.get("/health")).status_code == 200
        for path in ("/embed", "/classify", "/analyze", "/critique", "/review-chat"):
            r = await c.post(path, json={})
            assert r.status_code == 401, path
            assert r.json() == {"code": "AI_TOKEN_INVALID"}
        r = await c.post("/embed", json=EMBED, headers={"X-Ai-Token": "wrong"})
        assert r.status_code == 401
        r = await c.post("/embed", json=EMBED, headers={"X-Ai-Token": "s3cret-token"})
        assert r.status_code == 200


async def test_docs_hidden_when_token_set(no_key, monkeypatch):
    monkeypatch.setenv("AI_TOKEN", "s3cret-token")
    async with _client() as c:
        for path in ("/docs", "/redoc", "/openapi.json"):
            r = await c.get(path, headers={"X-Ai-Token": "s3cret-token"})
            assert r.status_code == 404, path
    assert app.openapi()["openapi"] == "3.0.3"  # 계약 생성은 그대로 동작한다


async def test_token_never_logged(no_key, monkeypatch, caplog):
    monkeypatch.setenv("AI_TOKEN", "s3cret-token")
    with caplog.at_level("DEBUG"):
        async with _client() as c:
            await c.post("/embed", json=EMBED, headers={"X-Ai-Token": "wrong-value"})
            await c.post("/embed", json=EMBED, headers={"X-Ai-Token": "s3cret-token"})
    assert "s3cret-token" not in caplog.text and "wrong-value" not in caplog.text
