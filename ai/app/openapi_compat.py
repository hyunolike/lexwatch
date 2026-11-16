"""OpenAPI 3.1 -> 3.0.3 downgrade so NSwag generates `string?`/`DateTimeOffset?`
instead of an opaque object for `anyOf: [X, null]`."""
from __future__ import annotations

from typing import Any

from fastapi import FastAPI


def _fix(node: Any) -> Any:
    if isinstance(node, list):
        return [_fix(n) for n in node]
    if not isinstance(node, dict):
        return node
    node = {k: _fix(v) for k, v in node.items()}
    any_of = node.get("anyOf")
    if isinstance(any_of, list):
        non_null = [a for a in any_of if a != {"type": "null"}]
        if len(non_null) == 1 and len(non_null) != len(any_of):
            rest = {k: v for k, v in node.items() if k != "anyOf"}
            inner = non_null[0]
            if "$ref" in inner:
                return {**rest, "allOf": [inner], "nullable": True}
            return {**inner, **rest, "nullable": True}
    return node


def install(app: FastAPI) -> None:
    def custom() -> dict[str, Any]:
        if app.openapi_schema:
            return app.openapi_schema
        schema = FastAPI.openapi(app)
        schema = _fix(schema)
        schema["openapi"] = "3.0.3"
        app.openapi_schema = schema
        return schema

    app.openapi = custom  # type: ignore[method-assign]
