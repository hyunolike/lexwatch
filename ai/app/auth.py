"""공유 토큰(`X-Ai-Token`) 검사. .NET API만 이 서비스를 부르게 한다.

- `AI_TOKEN`이 있으면 `/health`를 뺀 모든 경로가 같은 값의 헤더를 요구한다(상수 시간 비교). 틀리면 401.
  이때 `/docs`, `/redoc`, `/openapi.json`은 404다.
- `AI_TOKEN`이 비어 있으면 검사하지 않는다(로컬·테스트). 단 `AI_REQUIRE_TOKEN=true`면 503으로 닫는다.
- 토큰 값은 어디에도 로그로 남기지 않는다.
"""

from __future__ import annotations

import hmac
import json
import os

HEADER = "x-ai-token"
OPEN_PATHS = frozenset({"/health"})
DOC_PATHS = frozenset({"/docs", "/docs/oauth2-redirect", "/redoc", "/openapi.json"})


def configured_token() -> str:
    return os.environ.get("AI_TOKEN", "").strip()


def token_required() -> bool:
    return os.environ.get("AI_REQUIRE_TOKEN", "").strip().lower() in {"1", "true", "yes"}


async def _reply(send, status: int, body: dict) -> None:
    payload = json.dumps(body).encode()
    await send({
        "type": "http.response.start",
        "status": status,
        "headers": [(b"content-type", b"application/json"), (b"content-length", str(len(payload)).encode())],
    })
    await send({"type": "http.response.body", "body": payload})


class TokenAuthMiddleware:
    """순수 ASGI 미들웨어. 설정은 요청마다 환경 변수에서 읽는다."""

    def __init__(self, app):
        self.app = app

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http" or scope.get("path") in OPEN_PATHS:
            await self.app(scope, receive, send)
            return
        expected = configured_token()
        if not expected:
            if token_required():
                await _reply(send, 503, {"code": "AI_TOKEN_NOT_CONFIGURED"})
                return
            await self.app(scope, receive, send)
            return
        if scope.get("path") in DOC_PATHS:
            await _reply(send, 404, {"detail": "Not Found"})
            return
        given = ""
        for k, v in scope.get("headers", []):
            if k.decode("latin-1").lower() == HEADER:
                given = v.decode("latin-1")
                break
        if not hmac.compare_digest(given.encode("utf-8"), expected.encode("utf-8")):
            await _reply(send, 401, {"code": "AI_TOKEN_INVALID"})
            return
        await self.app(scope, receive, send)
