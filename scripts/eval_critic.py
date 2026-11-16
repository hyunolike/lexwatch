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


async def run_mode(cases: list[dict], use_rules: bool) -> dict:
    rows, provider = [], None
    for c in cases:
        req = CritiqueRequest(run_id=c["id"], memo=c["memo"], evidence=c["evidence"], use_rules=use_rules)
        resp = await critique(req)
        provider = resp.provider.name
        got = sorted({f.rule_id for f in resp.findings})
        exp = c["expected_rule_ids"]
        # 규칙 없음 모드는 rule_id를 모르므로 '무언가 적발했는가'로 센다
        hit = bool(got) if not use_rules else set(exp) <= set(got)
        rows.append({"id": c["id"], "expected": exp, "got": got, "hit": hit, "findings": [f.model_dump() for f in resp.findings]})
    over = [r for r in rows if r["expected"]]
    clean = [r for r in rows if not r["expected"]]
    return {
        "mode": "rules" if use_rules else "norules",
        "provider": provider,
        "source": "placeholder-fixture",
        "detected": sum(r["hit"] for r in over),
        "overclaims": len(over),
        "false_positives": sum(bool(r["got"]) for r in clean),
        "clean": len(clean),
        "rows": rows,
    }


async def main() -> None:
    cases = [json.loads(line) for line in CASES.read_text().splitlines() if line.strip()]
    OUT.mkdir(parents=True, exist_ok=True)
    for use_rules in (True, False):
        res = await run_mode(cases, use_rules)
        name = f"critic_{res['mode']}_{res['provider']}_{date.today().isoformat()}.json"
        (OUT / name).write_text(json.dumps(res, ensure_ascii=False, indent=2) + "\n")
        print(f"[{res['mode']}] provider={res['provider']} 적발 {res['detected']}/{res['overclaims']}, "
              f"거짓 양성 {res['false_positives']}/{res['clean']} -> eval/results/{name}")
        if res["provider"] == "pattern-fallback" and not use_rules:
            print("  (키 없음: 규칙 없음 모드도 같은 패턴 판정이라 비교 의미 없음)")
    print("주의: 케이스는 placeholder 조문 기반이다. 실녹화 후 재실행 전까지 수치는 placeholder 기준이다.")


asyncio.run(main())
