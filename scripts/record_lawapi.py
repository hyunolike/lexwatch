"""국가법령정보센터 응답 녹화. 실행: uv run --with httpx --with python-dotenv scripts/record_lawapi.py [출력 디렉터리]
LAW_OC 없으면 종료. 응답 본문은 가공하지 않고 저장한다.
출력 디렉터리를 주지 않으면 fixtures/lawapi/recorded 에 쓴다. fixtures/lawapi 바로 아래 파일은
테스트가 쓰는 합성 fixture라서 덮어쓰지 않는다."""
import datetime as dt, json, os, pathlib, sys
import httpx
from dotenv import load_dotenv

load_dotenv()
OC = os.environ.get("LAW_OC")
if not OC:
    sys.exit("LAW_OC 가 없습니다. .env 에 설정한 뒤 다시 실행하세요 (open.law.go.kr 에서 OC 신청).")
BASE = "https://www.law.go.kr/DRF"
OUT = (pathlib.Path(sys.argv[1]).resolve() if len(sys.argv) > 1
       else pathlib.Path(__file__).resolve().parent.parent / "fixtures" / "lawapi" / "recorded")
OUT.mkdir(parents=True, exist_ok=True)

def is_error_body(text):
    """오류는 HTTP 200 + 최상위 "result" 키(JSON) / <Response><result> (XML)로 온다."""
    s = text.lstrip()
    if s.startswith("<"):
        return s.startswith("<?xml") and "<Response>" in s and "<result>" in s
    try:
        obj = json.loads(s)
    except ValueError:
        return True  # JSON이 아니면(HTML 등) 정상 응답이 아니다
    return isinstance(obj, dict) and "result" in obj

def get(path, expect_error=False, **p):
    r = httpx.get(f"{BASE}/{path}", params={"type": "JSON", **p}, timeout=30)
    r.raise_for_status()
    if is_error_body(r.text) and not expect_error:
        sys.exit(f"법령 API 오류 응답: {r.text[:300]}\nOC 값과 호출 서버 IP/도메인 등록을 확인하세요.")
    return r.text

def save(name, text):
    # 목록의 법령상세링크에 OC가 그대로 실려 온다. 커밋되는 fixture에는 남기지 않는다.
    text = text.replace(f"OC={OC}", "OC=")
    (OUT / name).write_text(text, encoding="utf-8")
    print("saved", name)

def main():
    today = dt.date.today()
    rng = f"{(today - dt.timedelta(days=30)):%Y%m%d}~{today:%Y%m%d}"
    txt = get("lawSearch.do", OC=OC, target="law", ancYd=rng, display=100)
    save("list_recent.json", txt)
    items = json.loads(txt)["LawSearch"].get("law", [])
    if isinstance(items, dict): items = [items]
    for it in items[:3]:
        lid, mst = it["법령ID"], it["법령일련번호"]
        save(f"law_{lid}.json", get("lawService.do", OC=OC, target="law", MST=mst))
        save(f"oldnew_{lid}.json", get("lawService.do", OC=OC, target="oldAndNew", MST=mst))
    save("empty_list.json", get("lawSearch.do", OC=OC, target="law", ancYd="19000101~19000102"))
    save("error_body.json", get("lawSearch.do", OC="invalid_oc_zzz", target="law", expect_error=True))

if __name__ == "__main__":
    main()
