from __future__ import annotations

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from app.providers.llm.base import LLMError, OutputInvalid, RateLimited
from app.rag.base import RetrieverError


def register_handlers(app: FastAPI) -> None:
    @app.exception_handler(RateLimited)
    async def _rate_limited(_: Request, e: RateLimited) -> JSONResponse:
        return JSONResponse(
            status_code=429, content={"code": "LLM_RATE_LIMITED", "retry_after_s": e.retry_after_s}
        )

    @app.exception_handler(OutputInvalid)
    async def _invalid(_: Request, e: OutputInvalid) -> JSONResponse:
        return JSONResponse(status_code=422, content={"code": "LLM_OUTPUT_INVALID", "detail": e.detail})

    @app.exception_handler(LLMError)
    async def _llm(_: Request, e: LLMError) -> JSONResponse:
        return JSONResponse(status_code=502, content={"code": e.code, "detail": str(e)})

    @app.exception_handler(RetrieverError)
    async def _retriever(_: Request, e: RetrieverError) -> JSONResponse:
        return JSONResponse(status_code=502, content={"code": "RETRIEVER_ERROR", "detail": str(e)})
