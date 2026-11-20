"""검색 평가용 임베딩 캐시를 채운다(hybrid-rag RAG-06).

실행: make eval-retrieval-embed   (cd ai && uv run python ../scripts/embed_cache.py)
fixtures/retrieval/needed.jsonl(평가가 남긴 목록) 가운데 fixtures/retrieval/embeddings.json에 없는 것만
Cloudflare Workers AI bge-m3로 계산한다. 키는 sha256(모델 \\0 입력 유형 \\0 텍스트)로 백엔드 평가와 같다.
값은 소수 5자리로 줄여 저장소에 둔다. 다시 돌려도 이미 있는 것은 부르지 않는다.
"""

from __future__ import annotations

import hashlib
import json
import os
import sys
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[1]
DIR = ROOT / "fixtures" / "retrieval"
MODEL = "@cf/baai/bge-m3"
BATCH = 50


def key(input_type: str, text: str) -> str:
    return hashlib.sha256(f"{MODEL}\0{input_type}\0{text}".encode()).hexdigest()


def load(path: Path) -> dict:
    if path.exists():
        return json.loads(path.read_text(encoding="utf-8"))
    return {"model": MODEL, "dim": None, "vectors": {}}


def missing(needed: list[dict], cache: dict) -> list[dict]:
    seen: set[str] = set()
    out = []
    for n in needed:
        k = key(n["input_type"], n["text"])
        if k not in cache["vectors"] and k not in seen:
            seen.add(k)
            out.append(n)
    return out


def fill(todo: list[dict], cache: dict, client: httpx.Client, url: str, token: str) -> int:
    for i in range(0, len(todo), BATCH):
        batch = todo[i : i + BATCH]
        r = client.post(url, headers={"Authorization": f"Bearer {token}"}, json={"model": MODEL, "input": [b["text"] for b in batch]})
        r.raise_for_status()
        data = sorted(r.json()["data"], key=lambda d: d["index"])
        if len(data) != len(batch):
            raise RuntimeError(f"embedding count mismatch: {len(data)} != {len(batch)}")
        for b, d in zip(batch, data, strict=True):
            vec = [round(float(x), 5) for x in d["embedding"]]
            if cache["dim"] is None:
                cache["dim"] = len(vec)
            if len(vec) != cache["dim"]:
                raise RuntimeError(f"dimension changed: {len(vec)} != {cache['dim']}")
            cache["vectors"][key(b["input_type"], b["text"])] = vec
    return len(todo)


def main() -> None:
    account, token = os.environ.get("CLOUDFLARE_ACCOUNT_ID", ""), os.environ.get("CLOUDFLARE_API_TOKEN", "")
    if not account or not token:
        sys.exit("CLOUDFLARE_ACCOUNT_ID와 CLOUDFLARE_API_TOKEN이 필요합니다")
    needed = [json.loads(line) for line in (DIR / "needed.jsonl").read_text(encoding="utf-8").splitlines() if line.strip()]
    path = DIR / "embeddings.json"
    cache = load(path)
    todo = missing(needed, cache)
    url = f"https://api.cloudflare.com/client/v4/accounts/{account}/ai/v1/embeddings"
    with httpx.Client(timeout=120) as client:
        n = fill(todo, cache, client, url, token)
    path.write_text(json.dumps(cache, separators=(",", ":")), encoding="utf-8")
    print(f"새로 계산 {n}건, 캐시 {len(cache['vectors'])}건, 차원 {cache['dim']} -> {path.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
