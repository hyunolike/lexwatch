"""scripts/embed_cache.py: 캐시 키, 누락만 계산, 차원 기록(hybrid-rag 작업 13)."""

import importlib.util
import json
from pathlib import Path

import httpx

spec = importlib.util.spec_from_file_location("embed_cache", Path(__file__).resolve().parents[2] / "scripts" / "embed_cache.py")
ec = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ec)


def test_key_includes_model_and_input_type():
    assert ec.key("query", "x") == ec.key("query", "x")
    assert ec.key("query", "x") != ec.key("passage", "x")


def test_only_missing_texts_are_requested_and_dimension_recorded():
    cache = {"model": ec.MODEL, "dim": None, "vectors": {ec.key("passage", "있음"): [0.1]}}
    needed = [{"input_type": "passage", "text": "있음"}, {"input_type": "query", "text": "새것"}, {"input_type": "query", "text": "새것"}]
    todo = ec.missing(needed, cache)
    assert todo == [{"input_type": "query", "text": "새것"}]
    seen = []

    def handler(req: httpx.Request) -> httpx.Response:
        body = json.loads(req.content)
        seen.append(body)
        return httpx.Response(200, json={"data": [{"index": 0, "embedding": [0.123456789, 1.0]}]})

    n = ec.fill(todo, cache, httpx.Client(transport=httpx.MockTransport(handler)), "https://cf/embeddings", "tok")
    assert n == 1 and seen[0] == {"model": ec.MODEL, "input": ["새것"]}
    assert cache["dim"] == 2
    assert cache["vectors"][ec.key("query", "새것")] == [0.12346, 1.0]
