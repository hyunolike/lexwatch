import json

import httpx
import pytest

import app.classify as classify_mod
from app.main import app
from app.providers.llm.base import LLMProvider, LLMResponse, RateLimited
from app.schemas import ProviderInfo

REQ = {
    "run_id": "r1",
    "law": {"law_id": "L1", "name": "개인정보 보호법", "promulgation_date": "2026-11-01", "summary": "개정"},
    "candidates": [
        {"group_code": "privacy", "interest_text": "개인정보", "similarity": 0.9},
        {"group_code": "labor", "interest_text": "근로", "similarity": 0.2},
    ],
}


class Scripted(LLMProvider):
    info = ProviderInfo(name="scripted", kind="fallback")

    def __init__(self, *items):
        self.items = list(items)
        self.calls = 0

    async def chat(self, messages, tools=None, response_format=None, deadline=None):
        self.calls += 1
        it = self.items.pop(0)
        if isinstance(it, Exception):
            raise it
        return LLMResponse(content=json.dumps(it, ensure_ascii=False), provider=self.info)


def v(code, rel=True, conf=0.8):
    return {"group_code": code, "relevant": rel, "confidence": conf, "reason": "근거"}


async def post(llm, monkeypatch, body=REQ):
    monkeypatch.setattr(classify_mod, "get_llm", lambda: llm)
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://t") as c:
        return await c.post("/classify", json=body)


async def test_classify_returns_verdict_per_candidate(monkeypatch):
    llm = Scripted({"groups": [v("privacy"), v("labor", False, 0.1)]})
    r = await post(llm, monkeypatch)
    assert r.status_code == 200
    b = r.json()
    assert [g["group_code"] for g in b["groups"]] == ["privacy", "labor"]
    assert b["provider"]["name"] == "scripted"


async def test_unknown_group_code_from_llm_is_422(monkeypatch):
    bad = {"groups": [v("privacy"), v("finance")]}
    llm = Scripted(bad, bad)
    r = await post(llm, monkeypatch)
    assert r.status_code == 422 and r.json()["code"] == "LLM_OUTPUT_INVALID"
    assert llm.calls == 2


async def test_unknown_group_code_then_valid_retry_succeeds(monkeypatch):
    llm = Scripted({"groups": [v("finance")]}, {"groups": [v("privacy"), v("labor")]})
    assert (await post(llm, monkeypatch)).status_code == 200


async def test_rate_limit_propagates_as_429_with_retry_after(monkeypatch):
    r = await post(Scripted(RateLimited(7.0)), monkeypatch)
    assert r.status_code == 429
    assert r.json() == {"code": "LLM_RATE_LIMITED", "retry_after_s": 7.0}


async def test_omitted_candidate_retried_then_filled_as_irrelevant(monkeypatch):
    llm = Scripted({"groups": [v("privacy")]}, {"groups": [v("privacy")]})
    r = await post(llm, monkeypatch)
    assert llm.calls == 2
    labor = r.json()["groups"][1]
    assert labor == {"group_code": "labor", "relevant": False, "confidence": 0.0, "reason": "판정 누락"}


async def test_omitted_candidate_filled_by_retry(monkeypatch):
    llm = Scripted({"groups": [v("privacy")]}, {"groups": [v("privacy"), v("labor", True, 0.7)]})
    r = await post(llm, monkeypatch)
    assert r.json()["groups"][1]["relevant"] is True


async def test_invalid_json_is_422(monkeypatch):
    llm = Scripted({"nope": 1}, {"nope": 1})
    assert (await post(llm, monkeypatch)).status_code == 422


@pytest.mark.parametrize("mut", [lambda b: b.update(candidates=[])])
async def test_empty_candidates_rejected(monkeypatch, mut):
    body = json.loads(json.dumps(REQ))
    mut(body)
    assert (await post(Scripted(), monkeypatch, body)).status_code == 422


async def test_confidence_out_of_range_from_llm_is_422(monkeypatch):
    bad = {"groups": [v("privacy", True, 1.5), v("labor")]}
    assert (await post(Scripted(bad, bad), monkeypatch)).status_code == 422


def test_system_prompt_states_relevance_only():
    from app.agent.prompts import CLASSIFY_SYSTEM

    assert "적용 여부" in CLASSIFY_SYSTEM and "JSON만" in CLASSIFY_SYSTEM
