from __future__ import annotations

import logging

from fastapi import FastAPI

from app.agent.loop import AnalyzeRequest, AnalyzeResponse, analyze
from app.classify import ClassifyRequest, ClassifyResponse, classify
from app.critique.judge import CritiqueRequest, CritiqueResponse, critique
from app.errors import register_handlers
from app.openapi_compat import install
from app.rag.factory import get_retriever
from app.review_chat import ReviewChatRequest, ReviewChatResponse, review_chat
from app.schemas import EmbedRequest, EmbedResponse

# httpx logs full request URLs (incl. the law API OC) at INFO/DEBUG
for _name in ("httpx", "httpcore"):
    logging.getLogger(_name).setLevel(logging.WARNING)

app = FastAPI(title="LexWatch AI")
register_handlers(app)
install(app)


@app.get("/health", operation_id="health")
async def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/embed", response_model=EmbedResponse, operation_id="embed")
async def embed(req: EmbedRequest) -> EmbedResponse:
    retriever = get_retriever()
    vectors = await retriever.embed(req.texts, req.input_type)
    return EmbedResponse(vectors=vectors, provider=retriever.info)


@app.post("/classify", response_model=ClassifyResponse, operation_id="classify")
async def classify_endpoint(req: ClassifyRequest) -> ClassifyResponse:
    return await classify(req)


@app.post("/analyze", response_model=AnalyzeResponse, operation_id="analyze")
async def analyze_endpoint(req: AnalyzeRequest) -> AnalyzeResponse:
    return await analyze(req)


@app.post("/critique", response_model=CritiqueResponse, operation_id="critique")
async def critique_endpoint(req: CritiqueRequest) -> CritiqueResponse:
    return await critique(req)


@app.post("/review-chat", response_model=ReviewChatResponse, operation_id="reviewChat")
async def review_chat_endpoint(req: ReviewChatRequest) -> ReviewChatResponse:
    return await review_chat(req)
