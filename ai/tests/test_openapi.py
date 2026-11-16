from app.main import app


def test_operation_ids_and_nullable_downgrade():
    spec = app.openapi()
    assert spec["openapi"].startswith("3.0")
    ops = {m["operationId"] for p in spec["paths"].values() for m in p.values()}
    assert ops == {"health", "embed", "classify", "analyze", "critique", "reviewChat"}
    mst = spec["components"]["schemas"]["LawSummary"]["properties"]["mst"]
    assert mst["type"] == "string" and mst["nullable"] is True
    assert "anyOf" not in mst


def test_committed_openapi_json_is_current():
    import json
    from pathlib import Path

    committed = json.loads((Path(__file__).parents[1] / "openapi.json").read_text(encoding="utf-8"))
    assert committed == app.openapi(), "ai/openapi.json이 낡았다: scripts/gen-ai-client.sh를 실행하라"
