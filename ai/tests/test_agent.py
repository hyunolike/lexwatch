import asyncio
import json
import logging
from pathlib import Path

import httpx
import pytest
from pydantic import ValidationError

import app.agent.loop as loop
from app.agent.loop import AnalyzeRequest, analyze
from app.lawapi import LawApi
from app.providers.llm.base import OutputInvalid
from app.main import app
from app.providers.llm.base import LLMError, LLMProvider, LLMResponse, RateLimited, ToolCall
from app.schemas import Evidence, MemoSentence, ProviderInfo

FIX = Path(__file__).resolve().parents[2] / "fixtures" / "lawapi"
LAW = {"law_id": "001872", "name": "근로기준법", "promulgation_date": "2026-11-04", "summary": "개정", "mst": "100001"}


def law_api(error_for: set[str] | None = None) -> LawApi:
    def handler(request: httpx.Request) -> httpx.Response:
        p = request.url.params
        target = p.get("target")
        if error_for and target in error_for:
            return httpx.Response(200, json=json.loads((FIX / "error_body.json").read_text()))
        if target == "law":
            law = p.get("ID") or {"100001": "001872"}[p["MST"]]
            return httpx.Response(200, text=(FIX / f"law_{law}.json").read_text())
        if target == "oldAndNew":
            if "MST" not in p:  # real API requires MST here
                return httpx.Response(200, json=json.loads((FIX / "error_body.json").read_text()))
            return httpx.Response(200, text=(FIX / "oldnew_001872.json").read_text())
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


async def test_compare_old_new_uses_mst_and_id_only_is_rejected():
    api = law_api()
    from app.lawapi import LawApiError
    with pytest.raises(LawApiError):
        await api.old_new("001872")  # ID-only: stub returns the error body
    assert set(await api.old_new("001872", "100001")) == {"50", "56"}


async def test_old_new_via_agent_succeeds_with_mst_in_request():
    llm = Scripted(tc("compare_old_new", law_id="001872"), final(["oldnew:001872:50"]))
    r = await analyze(req(), llm)
    assert r.tool_calls[0].evidence_ids == ["oldnew:001872:50", "oldnew:001872:56"]


async def test_agent_falls_back_to_id_without_mst():
    request = AnalyzeRequest(run_id="r", group_code="g", interest_text="x", law={**LAW, "mst": None}, evidence=[])
    llm = Scripted(tc("compare_old_new", law_id="001872"), tc("get_article", law_id="001872", article_no="50"),
                   final(["law:001872:50"]))
    r = await analyze(request, llm)
    assert r.tool_calls[0].result_summary.startswith("오류:")
    assert r.tool_calls[1].evidence_ids == ["law:001872:50"]


async def test_slow_tool_hits_deadline(monkeypatch):
    now = [0.0]
    monkeypatch.setattr(loop, "_now", lambda: now[0])
    monkeypatch.setattr(loop, "DEADLINE_S", 0.05)

    async def slow(self, name, args):
        await asyncio.sleep(5)
        return []

    monkeypatch.setattr(loop.Tools, "run", slow)
    t0 = asyncio.get_event_loop().time()
    r = await analyze(req(), Scripted(tc("get_article", law_id="001872", article_no="50")))
    assert r.status == "insufficient_evidence"
    assert asyncio.get_event_loop().time() - t0 < 2


async def test_deadline_checked_between_tool_calls(monkeypatch):
    now = [0.0]
    monkeypatch.setattr(loop, "_now", lambda: now[0])
    ran = []

    async def tick(self, name, args):
        ran.append(name)
        now[0] += 200  # clock jumps past the 120s deadline
        return []

    monkeypatch.setattr(loop.Tools, "run", tick)

    class Two(Scripted):
        async def chat(self, *a, **k):
            return LLMResponse(content=None, provider=self.info, tool_calls=[
                ToolCall(id="a", name="get_group_interest", arguments={}),
                ToolCall(id="b", name="get_group_interest", arguments={})])

    r = await analyze(req(), Two())
    assert r.status == "insufficient_evidence" and ran == ["get_group_interest"]


async def test_unexpected_tool_exception_is_fed_back(monkeypatch):
    async def boom(self, name, args):
        raise RuntimeError("https://x/?OC=secret")

    monkeypatch.setattr(loop.Tools, "run", boom)
    llm = Scripted(tc("get_article", law_id="1", article_no="1"), final(["x"]))
    r = await analyze(req([Evidence(id="x", kind="k", title="t", body="b")]), llm)
    assert r.tool_calls[0].result_summary == "오류: RuntimeError"
    assert "secret" not in json.dumps(llm.last_messages)


async def test_non_dict_arguments_do_not_crash():
    bad = ToolCall(id="z", name="get_article", arguments=["oops"])  # type: ignore[arg-type]
    r = await analyze(req([Evidence(id="x", kind="k", title="t", body="b")]), Scripted(bad, final(["x"])))
    assert r.tool_calls[0].result_summary.startswith("오류:")


async def test_clause_and_item_text_is_flattened(monkeypatch):
    unit = {"조문번호": "50", "조문여부": "조문", "조문제목": "근로시간", "조문내용": "제50조(근로시간)",
            "항": [{"항내용": "① 첫째 항", "호": [{"호내용": "1. 첫 호", "목": [{"목내용": "가. 첫 목"}]}, {"호내용": "2. 둘째 호"}]},
                   {"항내용": "② 둘째 항"}]}
    body = {"법령": {"조문": {"조문단위": [unit]}}}
    api = LawApi(client=httpx.AsyncClient(transport=httpx.MockTransport(lambda r: httpx.Response(200, json=body))), oc="k")
    got = (await api.law_articles("1"))["50"]["body"]
    assert got.splitlines() == ["제50조(근로시간)", "① 첫째 항", "1. 첫 호", "가. 첫 목", "2. 둘째 호", "② 둘째 항"]


async def test_oc_never_logged(caplog):
    import app.main  # noqa: F401  (sets httpx/httpcore log levels at startup)
    caplog.set_level(logging.DEBUG)
    api = law_api()
    await api.law_articles("001872", "100001")
    assert "test-oc" not in caplog.text


ENGLISH = {"memo": [{"sentence": "The amendment may change working hour rules.", "citations": ["law:001872:50"]}],
           "open_questions": ["Scope of application"]}


def test_prompts_require_korean_output():
    # Cloudflare gpt-oss가 한국어 프롬프트에도 영어 초안을 썼다(2026-11-20 고등교육법 시행령).
    from app.agent.prompts import ANALYZE_SYSTEM
    from app.critique.judge import build_system
    from app.review_chat import SYSTEM as CHAT_SYSTEM

    for p in (ANALYZE_SYSTEM, build_system(True), build_system(False), CHAT_SYSTEM):
        assert "한국어로" in p


async def test_english_memo_gets_one_korean_correction():
    llm = Scripted(tc("get_article", law_id="001872", article_no="50"), ENGLISH, final(["law:001872:50"]))
    r = await analyze(req(), llm)
    assert r.status == "ok"
    assert r.memo[0].sentence.startswith("근로시간")
    assert "한국어로 다시" in json.dumps(llm.last_messages, ensure_ascii=False)


async def test_english_memo_twice_is_output_invalid():
    llm = Scripted(tc("get_article", law_id="001872", article_no="50"), ENGLISH, ENGLISH)
    with pytest.raises(OutputInvalid):
        await analyze(req(), llm)


def test_korean_check_allows_law_names_and_numbers():
    assert loop.is_korean("개인정보 보호법 제29조(안전조치의무)에 따라 접속기록을 보관해야 할 수 있다.")
    assert loop.is_korean("GDPR Article 32와 비교하면 개인정보 보호법 제29조가 더 구체적이다.")
    assert not loop.is_korean("The amendment introduces a mandatory student residence survey (제4조의11).")


def test_analyze_prompt_names_group_in_korean():
    from app.agent.loop import group_name
    assert group_name("privacy") == "개인정보" and group_name("labor") == "노동"
    assert group_name("tax") == "tax"  # unknown codes are shown as-is
