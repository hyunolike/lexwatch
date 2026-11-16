import logging

import httpx

from app.main import app
from app.tracing import parse_traceparent, trace_id_var

TRACE = "4bf92f3577b34da6a3ce929d0e0e4736"
VALID = f"00-{TRACE}-00f067aa0ba902b7-01"


def test_parse_traceparent():
    assert parse_traceparent(VALID) == TRACE
    assert parse_traceparent(VALID.upper()) == TRACE
    assert parse_traceparent(f"00-{'0' * 32}-00f067aa0ba902b7-01") is None
    assert parse_traceparent("garbage") is None
    assert parse_traceparent(None) is None


def _client():
    return httpx.AsyncClient(transport=httpx.ASGITransport(app=app, client=("203.0.113.7", 1)), base_url="http://t")


async def test_access_log_carries_trace_id_without_query_or_ip(caplog):
    seen = []

    class Grab(logging.Filter):
        def filter(self, record):
            seen.append(trace_id_var.get())
            return True

    logging.getLogger("lexwatch.access").addFilter(Grab())
    try:
        with caplog.at_level(logging.INFO, logger="lexwatch.access"):
            async with _client() as c:
                r = await c.post("/classify?secret=abc", json={}, headers={"traceparent": VALID})
    finally:
        logging.getLogger("lexwatch.access").filters.clear()
    assert r.status_code == 422
    assert seen == [TRACE]
    rec = next(x for x in caplog.records if x.name == "lexwatch.access")
    msg = rec.getMessage()
    assert "/classify -> 422" in msg
    assert "secret" not in msg and "203.0.113.7" not in msg


async def test_trace_id_resets_after_request():
    async with _client() as c:
        await c.get("/health", headers={"traceparent": VALID})
    assert trace_id_var.get() == "-"
