"""국가법령정보센터 응답 녹화. 실행: uv run --with httpx --with python-dotenv scripts/record_lawapi.py
LAW_OC 없으면 종료. 응답 본문은 가공하지 않고 저장한다."""
import datetime as dt, os, pathlib, sys
import httpx
from dotenv import load_dotenv

load_dotenv()
OC = os.environ.get("LAW_OC")
if not OC:
    sys.exit("LAW_OC 가 없습니다. .env 에 설정한 뒤 다시 실행하세요 (open.law.go.kr 에서 OC 신청).")
BASE = "https://www.law.go.kr/DRF"
OUT = pathlib.Path(__file__).resolve().parent.parent / "fixtures" / "lawapi"
OUT.mkdir(parents=True, exist_ok=True)

def get(path, **p):
    r = httpx.get(f"{BASE}/{path}", params={"type": "JSON", **p}, timeout=30)
    r.raise_for_status()
    return r.text

def save(name, text):
    (OUT / name).write_text(text, encoding="utf-8")
    print("saved", name)

today = dt.date.today()
rng = f"{(today - dt.timedelta(days=30)):%Y%m%d}~{today:%Y%m%d}"
txt = get("lawSearch.do", OC=OC, target="law", ancYd=rng, display=100)
save("list_recent.json", txt)
import json
items = json.loads(txt)["LawSearch"].get("law", [])
if isinstance(items, dict): items = [items]
for it in items[:3]:
    lid, mst = it["법령ID"], it["법령일련번호"]
    save(f"law_{lid}.json", get("lawService.do", OC=OC, target="law", MST=mst))
    save(f"oldnew_{lid}.json", get("lawService.do", OC=OC, target="oldAndNew", MST=mst))
save("empty_list.json", get("lawSearch.do", OC=OC, target="law", ancYd="19000101~19000102"))
save("error_body.json", get("lawSearch.do", OC="invalid_oc_zzz", target="law"))
