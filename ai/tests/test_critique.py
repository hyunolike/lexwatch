import json
from pathlib import Path

import httpx

import app.critique.judge as judge_mod
from app.critique.rules import RULES
from app.main import app
from app.providers.llm.base import LLMProvider, LLMResponse
from app.schemas import ProviderInfo

CASES = Path(__file__).resolve().parents[2] / "eval" / "critic_cases.jsonl"
EV = [{"id": "law:1:9", "kind": "law_article", "title": "t", "body": "제9조 본문"}]
REQ = {"run_id": "r", "memo": [{"sentence": "s0", "citations": ["law:1:9"]}, {"sentence": "s1", "citations": ["law:1:9"]}], "evidence": EV}


class Scripted(LLMProvider):
    info = ProviderInfo(name="scripted", kind="fallback")

    def __init__(self, payload):
        self.payload = payload
        self.seen: list[list[dict]] = []

    async def chat(self, messages, tools=None, response_format=None, deadline=None):
        self.seen.append(messages)
        return LLMResponse(content=json.dumps(self.payload, ensure_ascii=False), provider=self.info)


async def post(monkeypatch, llm, body):
    monkeypatch.setattr(judge_mod, "get_llm", lambda: llm)
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://t") as c:
        return await c.post("/critique", json=body)


async def test_rules_are_included_in_prompt_when_enabled(monkeypatch):
    llm = Scripted({"findings": []})
    r = await post(monkeypatch, llm, REQ)
    assert r.status_code == 200
    system = llm.seen[0][0]["content"]
    assert all(k in system for k in RULES)
    llm2 = Scripted({"findings": []})
    await post(monkeypatch, llm2, {**REQ, "use_rules": False})
    assert "OC-01" not in llm2.seen[0][0]["content"]


async def test_findings_out_of_range_sentence_index_are_dropped(monkeypatch):
    f = lambda i, r: {"sentence_index": i, "rule_id": r, "message": "m"}  # noqa: E731
    llm = Scripted({"findings": [f(1, "OC-04"), f(5, "OC-04"), f(-1, "OC-04"), f(0, "OC-99")]})
    b = (await post(monkeypatch, llm, REQ)).json()
    assert [(x["sentence_index"], x["rule_id"]) for x in b["findings"]] == [(1, "OC-04")]


async def test_keyless_pattern_judge_flags_expected_rules(monkeypatch):
    from app.providers.llm.mock import MockLLMProvider

    cases = [json.loads(line) for line in CASES.read_text().splitlines()]
    for c in cases:
        r = await post(monkeypatch, MockLLMProvider(), {"run_id": c["id"], "memo": c["memo"], "evidence": c["evidence"]})
        b = r.json()
        assert b["provider"] == {"name": "pattern-fallback", "kind": "fallback"}
        got = {f["rule_id"] for f in b["findings"]}
        assert got == set(c["expected_rule_ids"]), c["id"]


def test_cases_file_has_two_per_rule_and_six_clean():
    cases = [json.loads(line) for line in CASES.read_text().splitlines()]
    assert len(cases) == 16
    for rule in RULES:
        assert sum(1 for c in cases if c["expected_rule_ids"] == [rule]) == 2
    assert sum(1 for c in cases if c["expected_rule_ids"] == []) == 6
    for c in cases:
        assert c["source"] == "placeholder-fixture"
        if c["expected_rule_ids"]:
            assert 0 <= c["overclaim_sentence_index"] < len(c["memo"])
        else:
            assert "overclaim_sentence_index" not in c
        ids = {e["id"] for e in c["evidence"]}
        assert all(cit in ids for m in c["memo"] for cit in m["citations"])
