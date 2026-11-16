from __future__ import annotations

import logging

from fastapi import FastAPI

from app.agent.loop import AnalyzeRequest, AnalyzeResponse, analyze
from app.classify import ClassifyRequest, ClassifyResponse, classify
from app.critique.judge import CritiqueRequest, CritiqueResponse, critique
from app.errors import register_handlers
from app.rag.factory import get_retriever
from app.schemas import EmbedRequest, EmbedResponse

# httpx logs full request URLs (incl. the law API OC) at INFO/DEBUG
for _name in ("httpx", "httpcore"):
    logging.getLogger(_name).setLevel(logging.WARNING)

app = FastAPI(title="LexWatch AI")
register_handlers(app)


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/embed", response_model=EmbedResponse)
async def embed(req: EmbedRequest) -> EmbedResponse:
    retriever = get_retriever()
    vectors = await retriever.embed(req.texts, req.input_type)
    return EmbedResponse(vectors=vectors, provider=retriever.info)


@app.post("/classify", response_model=ClassifyResponse)
async def classify_endpoint(req: ClassifyRequest) -> ClassifyResponse:
    return await classify(req)


@app.post("/analyze", response_model=AnalyzeResponse)
async def analyze_endpoint(req: AnalyzeRequest) -> AnalyzeResponse:
    return await analyze(req)


@app.post("/critique", response_model=CritiqueResponse)
async def critique_endpoint(req: CritiqueRequest) -> CritiqueResponse:
    return await critique(req)
