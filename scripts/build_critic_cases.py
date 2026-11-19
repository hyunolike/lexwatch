"""크리틱 평가 케이스를 실제 법령 녹화본으로 만든다.

실행: cd ai && uv run python ../scripts/build_critic_cases.py
근거 본문은 운영과 같은 파서(ai/app/lawapi.py)로 fixtures/lawapi/recorded/current/의 녹화본에서 뽑는다.
초안 문장은 사람이 쓴다(과잉해석을 일부러 넣은 문장과 정확한 문장). 근거는 실제지만 문장은 작성자가 썼다는 점을
결과 해석에 반영해야 한다.
"""

from __future__ import annotations

import asyncio
import json
import re
import sys
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ai"))

from app.lawapi import LawApi  # noqa: E402

REC = ROOT / "fixtures" / "lawapi" / "recorded" / "current"
OUT = ROOT / "eval" / "critic_cases.jsonl"
SOURCE = "lawapi-recorded-2026-11-19"


def _handler(req: httpx.Request) -> httpx.Response:
    p = req.url.params
    name = "oldnew_000412.json" if p["target"] == "oldAndNew" else f"law_{p['ID']}.json"
    return httpx.Response(200, text=(REC / name).read_text(encoding="utf-8"))


def _precedent(prec_id: str) -> dict:
    d = json.loads((REC / f"prec_{prec_id}.json").read_text(encoding="utf-8"))
    s = d.get("PrecService", d)
    clean = lambda v: re.sub(r"<[^>]+>", "", str(v or "")).strip()  # noqa: E731
    body = f"{clean(s['사건명'])}\n판시사항: {clean(s['판시사항'])[:400]}"
    title = f"대법원 {clean(s['선고일자'])} 선고 {clean(s['사건번호'])}"
    return {"id": f"prec:{prec_id}", "kind": "precedent", "title": title, "body": body}


async def evidence() -> dict[str, dict]:
    api = LawApi(httpx.AsyncClient(transport=httpx.MockTransport(_handler)), oc="x")
    ev: dict[str, dict] = {}
    names = {"001872": "근로기준법", "011357": "개인정보 보호법", "010199": "전자금융거래법"}
    for lid, arts in {"001872": ["50", "56"], "011357": ["29", "34"], "010199": ["9", "21"]}.items():
        got = await api.law_articles(lid)
        for n in arts:
            ev[f"law:{lid}:{n}"] = {
                "id": f"law:{lid}:{n}",
                "kind": "law_article",
                "title": f"{names[lid]} 제{n}조({got[n]['title']})",
                "body": got[n]["body"],
            }
    on = await api.old_new("000412", "291441")
    ev["oldnew:000412:101"] = {
        "id": "oldnew:000412:101",
        "kind": "old_new",
        "title": "동물보호법 제101조(과태료) 신구조문 대비",
        "body": f"[구조문]\n{on['101']['old']}\n\n[신조문]\n{on['101']['new']}",
    }
    for pid in ["235109", "237875"]:
        ev[f"prec:{pid}"] = _precedent(pid)
    return ev


def S(sentence: str, *citations: str) -> dict:
    return {"sentence": sentence, "citations": list(citations)}


# (id, memo 문장들, 근거 ID들, 기대 규칙, 과잉해석 문장 위치)
CASES = [
    ("oc01-a", [S("개정 시행일 이전에 체결한 계약에도 제29조의 안전조치 의무가 그대로 적용될 수 있다.", "law:011357:29")], ["law:011357:29"], ["OC-01"], 0),
    ("oc01-b", [S("근로기준법 제50조의 1일 8시간 상한은 시행일 전에 체결된 근로계약에도 적용된다.", "law:001872:50")], ["law:001872:50"], ["OC-01"], 0),
    ("oc02-a", [S("대통령령으로 정하는 바에 따라 개인정보처리자는 접속기록을 1년 이상 보관해야 한다.", "law:011357:29")], ["law:011357:29"], ["OC-02"], 0),
    ("oc02-b", [S("제21조 제4항의 위임에 따라 금융회사는 정보기술부문 계획을 매년 3월 말까지 제출하여야 한다.", "law:010199:21")], ["law:010199:21"], ["OC-02"], 0),
    ("oc03-a", [S("대법원 판결은 근로자 수가 일시적으로 5명 미만인 사업장에도 항상 가산임금 규정이 적용된다는 원칙을 세웠다.", "law:001872:56", "prec:235109")], ["law:001872:56", "prec:235109"], ["OC-03"], 0),
    ("oc03-b", [S("대법원 판결에 따르면 개인정보가 유출되면 일반적으로 정보주체에 대한 손해배상 책임이 인정된다.", "law:011357:34", "prec:237875")], ["law:011357:34", "prec:237875"], ["OC-03"], 0),
    ("oc04-a", [S("모든 기업은 제29조에 따라 반드시 접속기록을 보관해야 한다.", "law:011357:29")], ["law:011357:29"], ["OC-04"], 0),
    ("oc04-b", [S("모든 근로자는 휴일근로 시간과 관계없이 통상임금의 100분의 100을 가산받는다.", "law:001872:56")], ["law:001872:56"], ["OC-04"], 0),
    ("oc05-a", [S("동물보호법 제101조 제2항을 위반하면 300만원 이하의 과태료가 부과된다.", "oldnew:000412:101")], ["oldnew:000412:101"], ["OC-05"], 0),
    ("oc05-b", [S("동물보호법 제101조 제4항 위반에 대한 과태료는 50만원 이하이다.", "oldnew:000412:101")], ["oldnew:000412:101"], ["OC-05"], 0),
    ("multi-1", [
        S("제50조 제1항은 1주 간의 근로시간을 휴게시간을 제외하고 40시간으로 제한한다.", "law:001872:50"),
        S("제2항은 1일 근로시간을 휴게시간을 제외하고 8시간으로 제한한다.", "law:001872:50"),
        S("따라서 모든 사업자는 반드시 하루 8시간을 넘겨 근로시키면 안 된다.", "law:001872:50"),
    ], ["law:001872:50"], ["OC-04"], 2),
    ("multi-2", [
        S("제34조는 개인정보 유출등을 알게 되면 지체 없이 정보주체에게 알리도록 한다.", "law:011357:34"),
        S("이번 개정은 시행일 이전에 발생한 유출에도 그대로 적용된다.", "law:011357:34"),
        S("통지 항목에는 유출등이 된 개인정보의 항목과 시점, 경위가 포함된다.", "law:011357:34"),
    ], ["law:011357:34"], ["OC-01"], 1),
    ("multi-3", [
        S("제21조는 금융회사등이 전자금융거래의 안전성 확보를 위해 금융위원회가 정하는 기준을 준수하도록 한다.", "law:010199:21"),
        S("제4항의 위임에 따라 금융회사는 계획 수립 결과를 분기마다 보고하여야 한다.", "law:010199:21"),
        S("세부 대상 금융회사의 범위는 하위법령을 함께 확인할 필요가 있다.", "law:010199:21"),
    ], ["law:010199:21"], ["OC-02"], 1),
    ("clean-1", [S("제29조는 개인정보처리자에게 내부 관리계획 수립과 접속기록 보관 등 안전성 확보 조치를 요구한다.", "law:011357:29")], ["law:011357:29"], [], None),
    ("clean-2", [S("제50조에 따라 1주 간의 근로시간은 휴게시간을 제외하고 40시간을 초과할 수 없다.", "law:001872:50")], ["law:001872:50"], [], None),
    ("clean-3", [
        S("제56조는 연장근로에 대해 통상임금의 100분의 50 이상을 가산하여 지급하도록 한다.", "law:001872:56"),
        S("이 개정이 특정 사업장에 미치는 영향은 추가 확인이 필요하다.", "law:001872:56"),
    ], ["law:001872:56"], [], None),
    ("clean-4", [S("제9조는 접근매체의 위조나 변조로 발생한 사고에 대해 금융회사 또는 전자금융업자의 배상 책임을 정한다.", "law:010199:9")], ["law:010199:9"], [], None),
    ("clean-5", [S("제34조는 개인정보 유출등을 알게 된 개인정보처리자가 지체 없이 정보주체에게 알리도록 한다.", "law:011357:34")], ["law:011357:34"], [], None),
    ("clean-6", [S("제21조는 금융회사등이 전자금융거래의 안전성 확보를 위해 금융위원회가 정하는 기준을 준수하도록 한다.", "law:010199:21")], ["law:010199:21"], [], None),
    ("clean-7", [S("개정된 동물보호법 제101조 제2항은 위반 시 500만원 이하의 과태료를 정한다.", "oldnew:000412:101")], ["oldnew:000412:101"], [], None),
]


async def main() -> None:
    ev = await evidence()
    lines = []
    for cid, memo, ev_ids, rules, idx in CASES:
        case = {"id": cid, "source": SOURCE, "memo": memo, "evidence": [ev[i] for i in ev_ids], "expected_rule_ids": rules}
        if idx is not None:
            case["overclaim_sentence_index"] = idx
        lines.append(json.dumps(case, ensure_ascii=False))
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(lines)}건 -> {OUT.relative_to(ROOT)} (과잉해석 {sum(bool(c[3]) for c in CASES)}, 정상 {sum(not c[3] for c in CASES)})")


if __name__ == "__main__":
    asyncio.run(main())
