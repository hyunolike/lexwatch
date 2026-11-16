#!/usr/bin/env bash
# Regenerates ai/openapi.json from the FastAPI app and the C# client from it.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
(cd "$root/ai" && uv run python -c "import json; from app.main import app; print(json.dumps(app.openapi(), ensure_ascii=False, indent=2))" > openapi.json)
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" PATH="$HOME/.dotnet:$PATH"
(cd "$root/backend" && dotnet tool restore >/dev/null && dotnet nswag run nswag.json)

# NSwag(System.Text.Json)는 EnumMember만 붙여서 enum이 "Query"처럼 PascalCase로 직렬화된다.
# System.Text.Json이 읽는 JsonStringEnumMemberName을 함께 붙여 와이어 값("query")을 지킨다.
perl -pi -e 's/^(\s*)(\[System\.Runtime\.Serialization\.EnumMember\(Value = @"([^"]*)"\)\])\s*$/$1$2\n$1\[System.Text.Json.Serialization.JsonStringEnumMemberName("$3")\]\n/' \
  "$root/backend/src/LexWatch.Api/Ai/AiClient.g.cs"
