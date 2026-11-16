"""Nemotron 한국어 시험 + NIM 응답 녹화. 실행: uv run --with httpx --with python-dotenv scripts/try_nemotron_ko.py"""
import glob, json, os, pathlib, sys, time
import httpx
from dotenv import load_dotenv

load_dotenv()
KEY = os.environ.get("NVIDIA_API_KEY")
if not KEY:
    sys.exit("NVIDIA_API_KEY 가 없습니다. .env 에 설정한 뒤 다시 실행하세요.")
BASE = os.environ.get("NIM_BASE_URL", "https://integrate.api.nvidia.com/v1")
MODEL = os.environ.get("NIM_MODEL") or sys.exit("NIM_MODEL 을 .env 에 설정하세요.")
EMB = os.environ.get("EMBED_MODEL", "nvidia/nemotron-3-embed-1b")
ROOT = pathlib.Path(__file__).resolve().parent.parent
H = {"Authorization": f"Bearer {KEY}"}
n429 = 0

def chat(msgs, tools=None):
    global n429
    body = {"model": MODEL, "messages": msgs, "temperature": 0, "max_tokens": 1024}
    if tools: body.update(tools=tools, tool_choice="auto")
    t = time.perf_counter()
    r = httpx.post(f"{BASE}/chat/completions", headers=H, json=body, timeout=120)
    ms = int((time.perf_counter() - t) * 1000)
    if r.status_code == 429:
        n429 += 1
        (ROOT / "fixtures/nim/chat_429.json").write_text(json.dumps({"status": 429, "headers": dict(r.headers), "body": r.json() if r.content else None}, ensure_ascii=False, indent=2))
    return r, ms

docs = [json.load(open(p)) for p in sorted(glob.glob(str(ROOT / "fixtures/lawapi/law_*.json")))[:3]]
tool = [{"type": "function", "function": {"name": "classify_change", "description": "법령 개정 분류", "parameters": {"type": "object", "properties": {"domain": {"type": "string", "enum": ["노동", "개인정보", "금융", "기타"]}, "confidence": {"type": "number"}, "evidence": {"type": "string"}}, "required": ["domain"]}}}]
for d in docs:
    text = json.dumps(d["법령"], ensure_ascii=False)[:3000]
    print("== (a) 분류")
    r, ms = chat([{"role": "user", "content": f"다음 법령 개정을 노동/개인정보/금융 중 하나로 분류하라.\n{text}"}], tool)
    print(r.status_code, ms, "ms", r.text[:500])
    if r.status_code == 200 and d is docs[0]:
        (ROOT / "fixtures/nim/chat_tool_call.json").write_text(r.text, encoding="utf-8")
    print("== (b) 근거 인용 초안 3문장")
    r, ms = chat([{"role": "user", "content": f"아래 조문만 근거로 영향 요약 3문장을 쓰고 각 문장 끝에 [조문번호]를 인용하라.\n{text}"}])
    print(r.status_code, ms, "ms", r.text[:800])
    print("== (c) 과잉해석 판정")
    r, ms = chat([{"role": "user", "content": f"초안: '이 개정으로 모든 기업은 즉시 벌금을 낸다.' 위 조문에 비추어 과잉해석인지 판정하고 이유를 한 문장으로 써라.\n{text}"}])
    print(r.status_code, ms, "ms", r.text[:600])
r = httpx.post(f"{BASE}/embeddings", headers=H, json={"model": EMB, "input": ["개인정보 안전조치의무"], "input_type": "query", "encoding_format": "float"}, timeout=60)
print("== embed", r.status_code)
if r.status_code == 200:
    (ROOT / "fixtures/nim/embed.json").write_text(r.text, encoding="utf-8")
    print("EMBED_DIM =", len(r.json()["data"][0]["embedding"]))
print("429 횟수:", n429)
