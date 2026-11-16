"""W3C traceparent 수신과 로그 상관. 내보내기는 stdout 로그뿐이다.

로그에는 trace_id, 메서드, 경로(쿼리 제외), 상태 코드, 소요 시간만 남긴다. 클라이언트 IP, 헤더, 본문은 남기지 않는다.
"""

from __future__ import annotations

import contextvars
import logging
import re
import time

_TRACEPARENT = re.compile(r"^[0-9a-f]{2}-([0-9a-f]{32})-([0-9a-f]{16})-[0-9a-f]{2}$")

trace_id_var: contextvars.ContextVar[str] = contextvars.ContextVar("trace_id", default="-")

log = logging.getLogger("lexwatch.access")


def parse_traceparent(value: str | None) -> str | None:
    """유효한 traceparent면 trace_id(32자리 16진수)를, 아니면 None을 돌려준다."""
    if not value:
        return None
    m = _TRACEPARENT.match(value.strip().lower())
    if not m or m.group(1) == "0" * 32 or m.group(2) == "0" * 16:
        return None
    return m.group(1)


class TraceIdFilter(logging.Filter):
    def filter(self, record: logging.LogRecord) -> bool:
        record.trace_id = trace_id_var.get()
        return True


def configure_logging() -> None:
    """루트 로거에 trace_id가 붙는 한 줄 형식을 건다. 이미 걸려 있으면 건드리지 않는다."""
    root = logging.getLogger()
    if any(getattr(h, "_lexwatch", False) for h in root.handlers):
        return
    handler = logging.StreamHandler()
    handler._lexwatch = True  # type: ignore[attr-defined]
    handler.addFilter(TraceIdFilter())
    handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s trace_id=%(trace_id)s %(name)s %(message)s"))
    root.addHandler(handler)
    if root.level == logging.NOTSET or root.level > logging.INFO:
        root.setLevel(logging.INFO)


class TraceContextMiddleware:
    """순수 ASGI 미들웨어. 요청마다 trace_id를 컨텍스트에 두고 접근 로그 한 줄을 남긴다."""

    def __init__(self, app):
        self.app = app

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return
        headers = {k.decode("latin-1").lower(): v.decode("latin-1") for k, v in scope.get("headers", [])}
        token = trace_id_var.set(parse_traceparent(headers.get("traceparent")) or "-")
        status = {"code": 500}
        started = time.perf_counter()

        async def send_wrapper(message):
            if message["type"] == "http.response.start":
                status["code"] = message["status"]
            await send(message)

        try:
            await self.app(scope, receive, send_wrapper)
        finally:
            if scope.get("path") != "/health":
                log.info(
                    "%s %s -> %d (%d ms)",
                    scope.get("method"), scope.get("path"), status["code"],
                    int((time.perf_counter() - started) * 1000),
                )
            trace_id_var.reset(token)
