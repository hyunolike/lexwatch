"""크리틱 평가: 규칙 있음/없음 두 모드를 돌려 적발률과 거짓 양성을 출력·저장한다.

실행: make eval-critic  (cd ai && uv run python ../scripts/eval_critic.py)
키가 없으면 pattern-fallback으로 돈다. 그 경우 의미 있는 것은 규칙 있음(패턴) 모드뿐이다.
"""

from __future__ import annotations

import asyncio
import json
import sys
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "ai"))

from app.critique.judge import CritiqueRequest, critique  # noqa: E402

CASES = ROOT / "eval" / "critic_cases.jsonl"
OUT = ROOT / "eval" / "results"


NOTE = ("pattern result; evidence is recorded real statutes but memo sentences are written by the same author "
        "as the rules; NOT model accuracy")


async def run_mode(cases: list[dict], use_rules: bool) -> dict:
    rows, provider = [], None
    for c in cases:
        req = CritiqueRequest(run_id=c["id"], memo=c["memo"], evidence=c["evidence"], use_rules=use_rules)
        resp = await critique(req)
        provider = resp.provider.name
        exp = c["expected_rule_ids"]
        idx = c.get("overclaim_sentence_index")
        at_idx = [f for f in resp.findings if f.sentence_index == idx] if exp else []
        other = [f for f in resp.findings if f not in at_idx]
        hit = bool(at_idx)  # 두 모드 공통: 과잉해석 문장에 적발이 있어야 적발
        rule_ok = any(f.rule_id in exp for f in at_idx)
        rows.append({
            "id": c["id"], "expected": exp, "overclaim_sentence_index": idx, "hit": hit, "rule_correct": rule_ok,
            "false_positive_findings": len(other), "findings": [f.model_dump() for f in resp.findings],
        })
    over = [r for r in rows if r["expected"]]
    clean = [r for r in rows if not r["expected"]]
    fallback = provider == "pattern-fallback"
    result = {
        "mode": "rules" if use_rules else "norules",
        "provider": provider,
        "source": sorted({c.get("source", "unknown") for c in cases}),
        "detected": sum(r["hit"] for r in over),
        "overclaims": len(over),
        "rule_id_correct": sum(r["rule_correct"] for r in over) if use_rules else None,
        "false_positive_findings": sum(r["false_positive_findings"] for r in rows),
        "clean_cases_flagged": sum(bool(r["false_positive_findings"]) for r in clean),
        "clean": len(clean),
        "rows": rows,
    }
    if fallback:
        result["note"] = NOTE
        if not use_rules:
            result["comparison_valid"] = False
    return result


async def main() -> None:
    cases = [json.loads(line) for line in CASES.read_text().splitlines() if line.strip()]
    OUT.mkdir(parents=True, exist_ok=True)
    for use_rules in (True, False):
        res = await run_mode(cases, use_rules)
        name = f"critic_{res['mode']}_{res['provider']}_{date.today().isoformat()}.json"
        (OUT / name).write_text(json.dumps(res, ensure_ascii=False, indent=2) + "\n")
        acc = f", 규칙ID 정확 {res['rule_id_correct']}/{res['overclaims']}" if use_rules else ""
        print(f"[{res['mode']}] provider={res['provider']} 적발 {res['detected']}/{res['overclaims']}{acc}, "
              f"거짓 양성 finding {res['false_positive_findings']}건 (정상 케이스 {res['clean_cases_flagged']}/{res['clean']}) "
              f"-> eval/results/{name}")
        if res["provider"] == "pattern-fallback" and not use_rules:
            print("  (키 없음: 규칙 없음 모드도 같은 패턴 판정이라 비교 의미 없음)")
    if res["provider"] == "pattern-fallback":
        print("!!! 경고: " + NOTE)
        print("!!! 규칙 없음 모드는 패턴 판정이 use_rules를 무시하므로 비교 무효(comparison_valid=false)")
    print("주의: 근거는 실제 법령 녹화본이지만 초안 문장은 규칙 작성자가 썼다. 일반화 성능을 뜻하지 않는다.")


asyncio.run(main())
