import json
import time
from pathlib import Path

import httpx
import pytest
from pydantic import ValidationError

import app.agent.loop as loop
from app.agent.loop import AnalyzeRequest, analyze
from app.lawapi import LawApi
from app.main import app
from app.providers.llm.base import LLMError, LLMProvider, LLMResponse, RateLimited, ToolCall
from app.schemas import Evidence, MemoSentence, ProviderInfo

FIX = Path(__file__).resolve().parents[2] / "fixtures" / "lawapi"
LAW = {"law_id": "001872", "name": "근로기준법", "promulgation_date": "2026-11-04", "summary": "개정"}


def law_api(error_for: set[str] | None = None) -> LawApi:
    def handler(request: httpx.Request) -> httpx.Response:
        p = request.url.params
        target = p.get("target")
        if error_for and target in error_for:
            return httpx.Response(200, json=json.loads((FIX / "error_body.json").read_text()))
        if target == "law":
            return httpx.Response(200, text=(FIX / f"law_{p['ID']}.json").read_text())
        if target == "oldAndNew":
            return httpx.Response(200, text=(FIX / f"oldnew_{p['ID']}.json").read_text())
        if target == "prec":
            return httpx.Response(200, json={"PrecSearch": {"prec": [
                {"판례일련번호": "7", "사건명": "근로시간 사건", "법원명": "대법원", "선고일자": "20240101", "사건번호": "2023다1"},
                {"판례일련번호": "8", "사건명": "무관 사건", "법원명": "서울고법", "선고일자": "20240201", "사건번호": "2023나2"},
            ]}})
        return httpx.Response(404)

    return LawApi(client=httpx.AsyncClient(transport=httpx.MockTransport(handler)), oc="test-oc")


class Scripted(LLMProvider):
    info = ProviderInfo(name="scripted", kind="nvidia")

    def __init__(self, *items):
        self.items = list(items)
        self.calls = 0
        self.deadlines = []

    async def chat(self, messages, tools=None, response_format=None, deadline=None):
        self.calls += 1
        self.deadlines.append(deadline)
        self.last_messages = [dict(m) for m in messages]
        it = self.items.pop(0)
        if isinstance(it, Exception):
            raise it
        if isinstance(it, ToolCall):
            return LLMResponse(content=None, provider=self.info, tool_calls=[it])
        return LLMResponse(content=json.dumps(it, ensure_ascii=False), provider=self.info)


def tc(name, **args):
    return ToolCall(id=f"c{name}{len(args)}", name=name, arguments=args)


def req(evidence=()):
    return AnalyzeRequest(
        run_id="r", group_code="labor", interest_text="근로시간", law=LAW, evidence=list(evidence)
    )


@pytest.fixture(autouse=True)
def _api(monkeypatch):
    monkeypatch.setattr(loop, "get_law_api", lambda: law_api())


def final(cites):
    return {"memo": [{"sentence": "근로시간 규정이 바뀔 수 있어 확인이 필요하다.", "citations": cites}], "open_questions": ["적용 범위"]}


async def test_agent_collects_evidence_and_returns_memo_with_citations():
    llm = Scripted(tc("get_article", law_id="001872", article_no="50"), final(["law:001872:50"]))
    r = await analyze(req(), llm)
    assert r.status == "ok"
    assert r.memo[0].citations == ["law:001872:50"]
    assert [e.id for e in r.new_evidence] == ["law:001872:50"]
    assert "40시간" in r.new_evidence[0].body
    assert r.provider.name == "scripted"
    assert all(d is not None for d in llm.deadlines)


async def test_tool_budget_exhausted_returns_insufficient_evidence():
    llm = Scripted(*[tc("get_article", law_id="001872", article_no="50") for _ in range(9)])
    r = await analyze(req(), llm)
    assert r.status == "insufficient_evidence"
    assert len(r.tool_calls) == 8
    assert llm.calls == 9


async def test_final_answer_without_evidence_is_redirected():
    llm = Scripted(final(["x"]), tc("get_article", law_id="001872", article_no="56"), final(["law:001872:56"]))
    r = await analyze(req(), llm)
    assert r.status == "ok"
    # second call saw the guard message
    assert "먼저 get_article 또는 compare_old_new로 조문을 확보하세요" in json.dumps(llm.last_messages, ensure_ascii=False)


async def test_redirect_gives_up_after_two_nudges():
    llm = Scripted(final(["x"]), final(["x"]), final(["x"]))
    r = await analyze(req(), llm)
    assert r.status == "insufficient_evidence"


def test_empty_citations_rejected_by_schema():
    with pytest.raises(ValidationError):
        MemoSentence(sentence="s", citations=[])


async def test_tool_calls_are_recorded_in_order():
    llm = Scripted(
        tc("get_article", law_id="001872", article_no="50"),
        tc("compare_old_new", law_id="001872"),
        tc("search_precedent", query="근로시간"),
        tc("get_group_interest"),
        final(["law:001872:50", "prec:7"]),
    )
    r = await analyze(req(), llm)
    assert [(c.seq, c.tool) for c in r.tool_calls] == [
        (1, "get_article"), (2, "compare_old_new"), (3, "search_precedent"), (4, "get_group_interest"),
    ]
    assert r.tool_calls[1].evidence_ids == ["oldnew:001872:50", "oldnew:001872:56"]
    assert {"prec:7", "prec:8", "group:labor"} <= {e.id for e in r.new_evidence}


async def test_tool_error_is_recorded_and_loop_continues(monkeypatch):
    monkeypatch.setattr(loop, "get_law_api", lambda: law_api(error_for={"law"}))
    llm = Scripted(tc("get_article", law_id="001872", article_no="50"), tc("compare_old_new", law_id="001872"),
                   final(["oldnew:001872:50"]))
    r = await analyze(req(), llm)
    assert r.tool_calls[0].result_summary.startswith("오류:")
    assert r.tool_calls[0].evidence_ids == []
    assert r.status == "ok"


async def test_citations_outside_evidence_get_one_correction_then_pass_through():
    llm = Scripted(tc("get_article", law_id="001872", article_no="50"), final(["law:999:1"]), final(["law:999:1"]))
    r = await analyze(req(), llm)
    assert llm.calls == 3
    assert r.memo[0].citations == ["law:999:1"]  # returned as-is for the .NET critic


async def test_citations_corrected_on_retry():
    llm = Scripted(tc("get_article", law_id="001872", article_no="50"), final(["law:999:1"]), final(["law:001872:50"]))
    r = await analyze(req(), llm)
    assert r.memo[0].citations == ["law:001872:50"]


async def test_request_evidence_counts_and_is_not_new():
    ev = Evidence(id="law:001872:50", kind="law_article", title="t", body="x" * 5000)
    llm = Scripted(final(["law:001872:50"]))
    r = await analyze(req([ev]), llm)
    assert r.status == "ok" and r.new_evidence == []
    assert "이하 생략" in json.dumps(llm.last_messages, ensure_ascii=False)


async def test_rate_limited_propagates():
    with pytest.raises(RateLimited):
        await analyze(req(), Scripted(RateLimited(3.0)))


async def test_llm_timeout_is_insufficient_evidence():
    r = await analyze(req(), Scripted(LLMError("x", code="LLM_TIMEOUT")))
    assert r.status == "insufficient_evidence"


async def test_deadline_exhausted_is_insufficient(monkeypatch):
    monkeypatch.setattr(loop, "DEADLINE_S", -1.0)
    r = await analyze(req(), Scripted())
    assert r.status == "insufficient_evidence"


async def test_keyless_extract_fallback(monkeypatch):
    monkeypatch.setenv("LLM_PROVIDER", "mock")
    evs = [Evidence(id=f"law:1:{i}", kind="k", title="t", body=f"제{i}조 본문이다. 둘째 문장.") for i in range(4)]
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://t") as c:
        r = await c.post("/analyze", json=json.loads(req(evs).model_dump_json()))
    b = r.json()
    assert r.status_code == 200 and b["status"] == "ok"
    assert len(b["memo"]) == 3
    assert b["memo"][0] == {"sentence": "[키 없음 · 원문 발췌] 제0조 본문이다.", "citations": ["law:1:0"]}
    assert b["open_questions"] == ["키 없음: 모델 분석을 수행하지 않았습니다."]
    assert b["tool_calls"] == [] and b["new_evidence"] == []
    assert b["provider"] == {"name": "extract-fallback", "kind": "fallback"}


async def test_keyless_without_evidence(monkeypatch):
    monkeypatch.setenv("LLM_PROVIDER", "mock")
    r = await analyze(req())
    assert r.status == "insufficient_evidence" and r.memo == []
