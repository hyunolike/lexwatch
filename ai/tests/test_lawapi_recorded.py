"""2026-11-19 국가법령정보센터 실응답 녹화본(fixtures/lawapi/recorded)으로 파서를 확인한다."""

import asyncio
from pathlib import Path

import httpx
import pytest

from app.lawapi import LawApi, LawApiError

REC = Path(__file__).resolve().parents[2] / "fixtures" / "lawapi" / "recorded"
MST = {"291575": "005698", "291383": "002065", "291875": "004750"}


def api() -> LawApi:
    def handler(request: httpx.Request) -> httpx.Response:
        p = request.url.params
        law = MST[p["MST"]]
        name = f"oldnew_{law}.json" if p["target"] == "oldAndNew" else f"law_{law}.json"
        return httpx.Response(200, text=(REC / name).read_text(encoding="utf-8"))

    return LawApi(httpx.AsyncClient(transport=httpx.MockTransport(handler)), oc="x")


def test_law_article_body_includes_paragraphs():
    arts = asyncio.run(api().law_articles("005698", "291575"))
    body = arts["7"]["body"]
    assert body.startswith("제7조(검사에 대한 결정의 통지)")
    assert "① 법 제12조에 따라 송치 결정을" in body
    assert "② 임시조치" in body


def test_old_new_parses_object_entries_and_groups_paragraphs():
    on = asyncio.run(api().old_new("005698", "291575"))
    assert list(on) == ["7", "8", "10", "45"]
    # 항이 별도 항목으로 와도 같은 조문에 붙는다. "법 제12조" 같은 본문 속 인용은 새 조문으로 보지 않는다.
    assert "② 임시조치" in on["7"]["new"]
    assert "공소청" in on["7"]["new"] and "검찰청" in on["7"]["old"]
    assert "12" not in on
    # 변경 표시 <P>…</P>는 근거 본문에서 걷어낸다.
    assert all("<P>" not in v["old"] + v["new"] and "</P>" not in v["old"] + v["new"] for v in on.values())


def test_old_new_absent_raises():
    with pytest.raises(LawApiError):
        asyncio.run(api().old_new("002065", "291383"))
