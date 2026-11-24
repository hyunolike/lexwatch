# 디자인 시스템 v2 외부 조사 (2026-11-24)

- 상태: 조사본. 아직 명세(spec)와 설계(design)로 옮기지 않았다.
- 이전 조사: [design-system/research.md](../design-system/research.md) (2026-11-19). 그때 확인한 Carbon 상태 표시, KRDS 타이포그래피, WCAG 기준은 그대로 유효해서 여기서 반복하지 않는다.
- 이미 정해진 방향(다시 논의하지 않음): 리걸 AI 전문 도구의 편집 디자인 톤(Harvey, Legora 계열), 마스코트 렉스는 로고와 빈 화면과 서버 깨우기 대기 화면에만, 다크 모드 포함(시스템 설정을 따르고 토글로 바꿀 수 있으며 두 테마 모두 대비 테스트 통과).

## 0. 요약

| 항목 | 권고 |
|---|---|
| 브랜드 색 | 잉크 남색 하나. 밝은 테마 `#1F3A68`, 어두운 테마 `#8FA9E0`. 렉스 재킷 남색과 이어지고, 빨강(위험)과 초록(성공)을 피한다 |
| 제목 글꼴 | Noto Serif KR 가변 글꼴. npm `@fontsource-variable/noto-serif-kr@5.3.1` (OFL-1.1) |
| UI와 본문 글꼴 | Pretendard 유지. npm `pretendard@1.3.9` (OFL-1.1). `tnum` 기능이 있음을 이번에 확인했다 |
| 새 라이브러리 | `class-variance-authority@0.7.1`, `tailwind-merge@3.7.0`(설정 확장 필수), `clsx@2.1.1` |
| 쓰지 않을 라이브러리 | `next-themes`, `sonner`, `cmdk`, `vaul`, `motion`, `@radix-ui/colors`(설치하지 않고 방법만 참고). 대신 Base UI의 Toast, Autocomplete, Dialog와 Next.js 공식 인라인 스크립트 방식을 쓴다 |
| 먼저 넣을 패턴 | 문서와 근거를 나란히 보는 분할 화면, 인용 미리보기 카드, 문장별 검증 상태, 승인 대기함 키보드 단축키, 명령 팔레트(⌘K), 짧은 결과 알림 토스트 |

v1 설계의 결정 두 가지를 뒤집는 권고가 들어 있다. 하나는 cva와 tailwind-merge를 쓰지 않는다는 결정(design.md 1.1)이고, 다른 하나는 토스트를 쓰지 않는다는 결정이다. 3절과 5절에 이유를 적었다. 작성자 승인이 필요하다.

## 1. 레퍼런스 분석

표의 "확인 방법"은 내가 실제로 연 페이지인지, 검색 결과 요약으로만 본 것인지를 구분한다. 리걸테크 다섯 곳은 공개 디자인 가이드가 없다. 그래서 제품 소개 페이지, 릴리스 노트, 블로그, 마케팅 사이트의 HTML에서 확인한 사실만 적었다. 실제 제품 화면(로그인 뒤)은 하나도 직접 보지 못했다.

### 1.1 리걸 AI 제품

#### Harvey

| 확인한 사실 | 출처 | 확인 방법 |
|---|---|---|
| 마케팅 사이트가 자체 세리프 `HarveySerifFont`(Regular, Italic)와 자체 산세리프 `HarveySansFont`(파일명 `HarveySansDiatypeVariable`)를 함께 쓴다. CSS에서 산세리프 사용이 28곳, 세리프가 22곳이다. 본문 짙은 색은 순검정이 아닌 따뜻한 검정 `#1F1D1A` | https://www.harvey.ai/ | HTML을 받아 `font-family`와 woff2 파일명을 셌다 |
| 제목용 서체는 TWK Ghost, 본문과 UI는 Dinamo의 ABC Diatype. 브랜드 기초 작업은 포틀랜드의 스튜디오 Geist가 했다 | https://fontsinuse.com/uses/77027/harvey | 직접 열람 |
| Review Table은 "셀마다 인용이 있다". 사실 그래프에서 변호사가 제안된 사실을 근거와 대조해 "수락, 수정, 반려"한다. 셀을 "확인됨"으로 표시하고 셀 단위 활동 기록이 남는다 | https://www.harvey.ai/blog/how-harvey-is-building-reviews-and-verification | 직접 열람 |
| 준비 중인 Cite Check는 인용마다 "verified / needs review / issue found" 세 상태를 붙이고, 고칠 내용을 추적 변경으로 제안한다 | 위 글 | 직접 열람 |
| 작업 중 상태(thinking state)에 어떤 파일을 찾고 어떤 검색어를 썼고 무엇을 읽었는지 보여 준다. "212개 계약서를 모두 읽었다"처럼 읽은 범위를 밝히는 기능을 준비 중이다 | 위 글 | 직접 열람 |
| 출처 패널 개편(2025-11-05): 출처 종류 아이콘, 패널 안 검색, 스레드를 내리면 출처가 함께 바뀜 | https://eu.help.harvey.ai/release-notes/improved-sources-experience | **열지 못함.** 로그인 페이지로 이동했다. 검색 결과 요약으로만 확인 |

LexWatch에 가져올 것:
- 제목은 세리프, 나머지는 산세리프로 나누는 이중 서체 구조. 한국어에서는 Noto Serif KR과 Pretendard로 같은 구조를 만든다(2절).
- 순검정 대신 살짝 따뜻한 검정과 종이색 바탕. 제안 토큰의 `text-primary #1B1D22`, `canvas #F7F6F3`이 이 방향이다(4절).
- 근거 대조 결과를 세 단계 상태로 보여 주는 방식. 크리틱 결과를 문장마다 "확인됨, 확인 필요, 문제 있음"으로 표시하는 근거가 된다(5절 P3).
- AI가 무엇을 읽었는지 밝히는 범위 표시. 실행 타임라인에 "근거 N건 중 N건 확인" 같은 줄을 둔다.

#### Legora

| 확인한 사실 | 출처 | 확인 방법 |
|---|---|---|
| 마케팅 사이트(Framer)는 자체 산세리프 `Legora Sans`와 Inter, Inter Display를 쓴다. 장식용으로 Playfair Display, 고정폭으로 Fragment Mono를 불러온다. 가장 많이 쓰인 색은 흰색, 짙은 초록 `rgb(0,80,50)` (`#005032`), 짙은 남흑 `rgb(13,16,22)`, 따뜻한 회색 `rgb(104,101,94)` | https://legora.com/ | HTML을 받아 글꼴 이름과 `rgb()` 값을 셌다 |
| Tabular Review는 문서가 행, 질문이 열인 표다. 모든 값이 "출처에 인용"된다. "Mark as Reviewed, Lock Cells, Review Mode"로 협업 검토를 통제한다 | https://legora.com/product/tabular-review | 직접 열람. 셀 안에서 인용이 어떻게 보이는지는 페이지에 없다 |
| 편집기에서 AI 제안을 "수락하거나 거절"하고, 초안은 "근거 데이터에 대한 인용"과 함께 만들어진다 | https://legora.com/product/editor | 직접 열람. 추적 변경 표시 방식은 없다 |
| 표의 셀에 마우스를 올리면 해당 조항이 강조된 카드와 "View in document" 링크가 뜬다 | Contrary Research 보고서(검색 결과) | **검색 결과 요약만.** 화면을 직접 보지 못했다 |

LexWatch에 가져올 것:
- 브랜드 색 하나에 따뜻한 무채색을 받치는 구성. Legora는 초록을 골랐지만 LexWatch는 초록이 성공 상태와 겹쳐서 쓰지 않는다(4.1).
- "검토 완료 표시"와 "잠금"을 행 단위 상태로 두는 방식. 승인 대기함의 행에 "내가 확인함" 표시를 붙이는 근거.

#### Thomson Reuters CoCounsel

| 확인한 사실 | 출처 | 확인 방법 |
|---|---|---|
| Verify(2026-02 출시)는 조사 보고서의 "모든 주장을 관련 원문 구절과 나란히" 보여 준다. 인용 장부(citation ledger)는 AI가 쓴 출처를 실제로 읽었는지 기록한다. KeyCite는 일부 파기된 판례와 개정 예정 조문을 경고한다 | https://www.thomsonreuters.com/en-us/posts/innovation/if-you-cant-verify-it-you-cant-sign-it/ | 직접 열람. 화면 배치는 글에 없다 |
| 대화 안의 인용을 누르면 Citation Viewer가 열리고 그 안에서 검색과 확대가 된다. Tabular Analysis의 셀을 누르면 원문의 해당 구간이 강조된다 | CoCounsel 도움말과 2026-09 릴리스 글(검색 결과) | **검색 결과 요약만** |

LexWatch에 가져올 것:
- 주장과 근거 구절을 나란히 놓는 레이아웃. 검토 상세를 "초안 문장 / 근거" 두 칸으로 나누는 근거(P1).
- "개정 예정" 경고. 법안 레이더에서 이미 본 법안이 개정안으로 넘어오면 검토 상세에 알림 줄을 다는 아이디어로 쓸 수 있다.

#### Spellbook

| 확인한 사실 | 출처 | 확인 방법 |
|---|---|---|
| 수정 제안마다 "무엇이 왜 바뀌었는지" 설명이 붙고, 변호사가 제안을 "승인, 수정, 거절"한다. 수정 요약에서 각 수정 위치로 바로 이동한다. 문서 비교 결과를 "Important"와 "Notable"로 나눈다 | https://spellbook.com/learn/contract-redline-software-features | 직접 열람. 판매사 자체 글이라 화면 배치는 확인하지 못했다 |

LexWatch에 가져올 것:
- 변경 목록에서 해당 위치로 뛰는 방식. 신구조문 비교에 "다음 변경(]) / 이전 변경([)" 이동을 넣는다(이전 조사의 "변경 덩어리 사이 키보드 이동"과 같다).
- 변경을 중요도 두 단계로 나누는 생각. 다만 LexWatch는 중요도를 AI가 판단하지 않고 공식 변경 표시만 그린다. 가져오지 않는다.

#### Hebbia

| 확인한 사실 | 출처 | 확인 방법 |
|---|---|---|
| Chat, Matrix, Draft에 인용 미리보기를 넣었다. 미리보기는 "작업 흐름을 끊지 않고" 원문 데이터를 보여 준다 | https://www.hebbia.com/blog/the-disclosure-december-2025 | 직접 열람 |
| Matrix의 셀을 누르면 답이 나온 문서 위치와 답을 만든 단계가 보인다 | a16z 소개와 datastudios 글(검색 결과) | **검색 결과 요약만** |

LexWatch에 가져올 것:
- 페이지를 떠나지 않는 인용 미리보기. 지금의 인용 칩 팝오버(DS-09)를 유지하되 마우스 올림에도 열리는 미리보기 카드로 넓힌다(P2).

### 1.2 디자인 시스템

#### Vercel Geist

- 글자 척도는 제목(72~14), 버튼(16, 14, 12), 라벨(20~12, 고정폭 14/13/12), 본문(24~13)의 네 묶음이다. 라벨은 한 줄짜리, 본문(Copy)은 여러 줄짜리로 줄높이가 다르다. 메뉴는 Label 14, 가장 흔한 글은 Copy 14, 숫자는 tabular 변형을 쓴다고 규정한다. https://vercel.com/geist/typography (직접 열람. 각 클래스의 px과 줄높이 값은 페이지 본문에 없었다)
- 색은 회색, 회색 알파, 일곱 유채색 척도가 각각 100~1000의 열 단계다. 100~300은 배경(기본, 마우스 올림, 누름), 400~600은 테두리, 700~800은 강한 배경, 900은 보조 글자, 1000은 기본 글자다. 회색 알파는 배경이 비쳐야 할 때만 쓴다. https://vercel.com/geist/colors (직접 열람. 다크 모드 처리 방식은 이 페이지에 없었다)

가져올 것: "한 줄 라벨"과 "여러 줄 본문"을 다른 토큰으로 나누는 구분. LexWatch의 `text-ui`(한 줄)와 `text-reading`(여러 줄) 구분과 같은 생각이라 유지한다. 단계 번호마다 쓰임새를 정해 두는 규칙은 4절 토큰 이름에 반영했다.

#### Linear

- 테마 생성을 HSL에서 LCH로 옮겼다. 테마 하나에 변수 98개가 필요했는데, 이제 기본색, 강조색, 대비 세 값으로 밝은 테마와 어두운 테마를 만든다. 대비 값을 올려 고대비 테마도 만든다. 강조색 파랑이 계산에 섞이는 양을 줄여 더 중립적으로 보이게 했다. 제목은 Inter Display, 나머지는 Inter. https://linear.app/now/how-we-redesigned-the-linear-ui (직접 열람. 간격 수치는 글에 없었다)

가져올 것: 무채색에 강조색을 아주 조금만 섞는 방식. 제안한 어두운 테마의 바탕(`#111214`, `#18191C`)은 거의 무채색이고 강조색은 버튼과 링크에만 쓴다. 테마 생성기는 만들지 않는다. 토큰이 30개 남짓이라 손으로 정하고 테스트로 지키는 편이 싸다.

#### GitHub Primer (다크 모드 토큰 방식)

- 토큰은 세 층이다. 기본(base) 토큰은 원시 값이고 "코드나 디자인에서 직접 쓰면 안 된다". 기능(functional) 토큰은 `bgColor-default`, `fgColor-muted`, `borderColor-default`처럼 역할 이름이고 색 모드에 따라 값이 바뀐다. 컴포넌트 토큰은 꼭 필요할 때만 둔다. 강조 배경(emphasis) 위의 글자는 `fgColor-onEmphasis`와 짝지어야 한다. 중립 척도를 밝은 테마는 흰색부터, 어두운 테마는 검정부터 뒤집어 두 테마가 같은 기능 토큰을 쓴다. 고대비 테마는 글자 대비 7:1을 목표로 한다. https://primer.style/foundations/color/overview (직접 열람)

가져올 것: 이게 이번 다크 모드의 핵심 방식이다. 화면 코드는 `bg-surface`, `text-secondary` 같은 역할 이름만 쓰고, 값은 테마마다 바뀐다. 지금 `tokens.css`가 이미 역할 이름이라 이름 체계만 정리하면 된다. `on-accent` 짝 규칙도 지금의 `on-brand`와 같다. 원시 척도 층은 두지 않는다. Primer처럼 수천 개 화면이 아니라서 층 하나를 줄이는 쪽이 낫다.

#### IBM Carbon (데이터 밀도가 높은 표)

- 표 행 높이는 24, 32, 40(기본), 48, 64px. 두 줄짜리 행은 64px. 머리 행은 본문 행과 같은 높이로 두고, 도구 막대 높이도 행 크기에 맞춘다. 줄무늬(zebra)는 가로로 훑기 쉽게 하는 선택 사항이다. 행을 고르면 표 위에 일괄 작업 막대가 뜨고 행별 아이콘은 잠긴다. 펼치는 행은 "모두 펼치기"를 기본으로 두지 않는다. https://carbondesignsystem.com/components/data-table/usage/ (직접 열람)
- 테마는 White, Gray 10, Gray 90, Gray 100 네 가지다. 층 토큰 `layer-01/02/03`이 아래 층 위에 번갈아 쌓이고, `border-subtle-0N`은 같은 층의 구분선, `border-strong-0N`은 3:1이 필요한 입력 테두리다. https://carbondesignsystem.com/elements/color/tokens/ (직접 열람. 어두운 테마에서 층 값이 어떻게 바뀌는지는 이 페이지 본문에 없어 확인하지 못했다)

가져올 것: 밀도 전환(P7)의 수치. 승인 대기함과 운영 현황 표는 "편안하게 48px / 촘촘하게 32px" 두 단계를 두고 기본은 40px. 테두리를 "장식용"과 "3:1 필요" 둘로 나누는 규칙은 v1에도 있고(`line`, `line-strong`) 그대로 간다. 층마다 바탕을 번갈아 쓰는 방식은 어두운 테마에서 `surface`, `surface-raised`를 나누는 근거다.

#### Radix Colors

- 척도 하나가 12단계다. 1 앱 바탕, 2 은은한 바탕(카드, 사이드바), 3~5 UI 요소 바탕(기본, 마우스 올림, 누름), 6 은은한 테두리, 7 대화형 요소 테두리, 8 강한 테두리와 포커스 링, 9 단색 바탕(가장 순수한 색), 10 단색 마우스 올림, 11 낮은 대비 글자, 12 높은 대비 글자. 11과 12는 같은 척도의 2단계 위에서 APCA Lc 60, Lc 90을 보장한다. 어두운 테마는 앱 바탕을 회색 척도의 1이나 2단계로 두고, 별칭 변수를 테마마다 다른 값에 연결한다. https://www.radix-ui.com/colors/docs/palette-composition/understanding-the-scale (직접 열람)
- 회색 척도는 Gray, Mauve, Slate, Sage, Olive, Sand 여섯이고 강조 척도는 Gold부터 Sky까지 스물다섯이다. 모든 척도에 밝은, 밝은 알파, 어두운, 어두운 알파 네 벌이 있다. https://www.radix-ui.com/colors/docs/palette-composition/scales (직접 열람)
- 설치는 `@radix-ui/colors`이고 현재 3.0.0이다. https://www.radix-ui.com/colors/docs/overview/installation (직접 열람)

가져올 것: 12단계의 "단계마다 역할" 규칙을 토큰 설계 기준으로 쓴다. 패키지는 설치하지 않는다. 이유는 3절.

## 2. 한국어 글꼴

### 2.1 후보 비교

| 글꼴 | 용도 | 라이선스 | 자체 호스팅 방법 | 크기 | 한글 범위 |
|---|---|---|---|---|---|
| **Pretendard** | UI, 본문 (유지) | SIL OFL 1.1 | `pretendard@1.3.9` (지금 `web/package.json`에 있는 버전). 동적 서브셋 CSS 그대로 | 가변 woff2 전체 2,057,688B(직접 받아 확인). 동적 서브셋 92조각, 평균 약 32KB(이전 조사) | 한글 음절 11,172자 모두. **한자 0자**. OpenType `tnum`, `case`, `zero`, `ss01~ss16` 있음 (fontTools로 직접 확인) |
| **Noto Serif KR** | 제목, 선택적으로 긴 읽기 | SIL OFL 1.1 (패키지 `metadata.json`: `"license": "OFL-1.1"`, 저작자 Google) | `@fontsource-variable/noto-serif-kr@5.3.1` (2026-10-11 배포). 가변 축 `wght 200~900` | woff2 124조각, 합계 6,111KB, 조각당 45~90KB. `unicode-range`로 화면에 나온 글자의 조각만 받는다 | 한글 음절 11,172자 모두, CJK 한자 4,620자 (패키지 CSS의 `unicode-range`를 직접 계산) |
| 마루 부리 (MaruBuri) | 제목 대안 | SIL OFL 1.1. 저작권은 네이버와 네이버문화재단, 제작은 AG 타이포그라피연구소 | 공식 배포는 네이버 한글한글 아름답게(hangeul.naver.com) 사이트지만 **열지 못했다**(도구가 접근을 거부). npm은 개인이 올린 `@kfonts/maruburi@0.1.0` (2024-05-05, 저자 item4)뿐 | 굵기 5종 정적 woff2, 각 783~871KB, 합계 4,145KB. **서브셋 없음** | 한글 음절 11,172자 모두, 한자 0자 (fontTools로 Regular 파일 직접 확인) |
| 리디바탕 (RIDIBatang) | 긴 읽기 대안 | SIL OFL 1.1, 저작권 리디주식회사 (산돌구름에 옮겨 둔 라이선스 원문 기준. 리디 공식 페이지는 찾지 못함) | npm `@noonnu/ridibatang@0.0.1`은 **글꼴 파일이 없다**. 패키지 안에 `index.css`, `package.json`, `README.md`만 있고 CSS가 가리키는 `RIDIBatang.woff`가 빠져 있다. 라이선스 표기도 MIT로 원 라이선스와 다르다. 쓰려면 원본 파일을 받아 직접 서브셋해야 한다 | 확인하지 못함 | 확인하지 못함 |
| 고운바탕 (Gowun Batang) | 참고 | SIL OFL 1.1 | `@fontsource/gowun-batang@5.3.0` (2026-07-19) | 패키지 풀었을 때 11.7MB(정적 굵기 2종, 서브셋) | 확인하지 않음 |

라이선스 근거: Noto Serif KR은 패키지 메타데이터, 마루 부리와 리디바탕은 https://www.sandollcloud.com/free-font/16193/Maru-Buri 와 https://www.sandollcloud.com/free-font/857/RIDIBatang 의 검색 결과 요약(둘 다 웹사이트 임베딩 "사용 가능"으로 표기). 마루 부리와 리디바탕은 원 배포처 문서를 열지 못했으니 도입 전에 원문을 한 번 더 확인해야 한다.

### 2.2 권고

- **UI와 본문은 Pretendard를 유지한다.** 바꿀 이유가 없다. v1에서 확인하지 못했던 `tnum`도 있다. 한 가지 새로 알게 된 점: Pretendard에는 한자가 없다. 조문이나 법령명에 한자가 섞이면(옛 법령, 괄호 속 한자 병기) 그 글자만 시스템 글꼴로 바뀐다. `--font-sans` 대체 목록에 `"Noto Serif KR Variable"`을 넣지 말고(세리프가 섞임) 지금처럼 `Apple SD Gothic Neo`, `Malgun Gothic`이 받게 둔다. 두 글꼴 모두 한자를 갖고 있다.
- **제목은 Noto Serif KR을 쓴다.** 세 후보 중 유일하게 npm에 관리되는 패키지가 있고(fontsource, 2026-10 배포), 가변 글꼴이며, 조각으로 나뉘어 있고, 한자까지 들어 있다. 제목에만 쓰면 한 화면에서 받는 조각은 몇 개뿐이다. 마루 부리는 글자 모양이 더 부드럽고 현대적이지만 굵기 하나가 800KB 안팎이라 직접 서브셋을 만들어야 하고, npm 패키지가 개인 배포 0.1.0이다. 리디바탕은 패키지가 깨져 있다.
- **긴 읽기(초안 문장, 조문)에 세리프를 쓸지는 시험 뒤에 정한다.** 세리프 본문은 편집 디자인 느낌을 크게 키우지만, 같은 화면의 표와 버튼은 Pretendard라서 화면 안에 두 서체가 섞인다. 화면 확인 없이 결정할 일이 아니다. 기본은 Pretendard 본문으로 두고 `/design-system`에서 "읽기 글꼴" 설정으로 비교하게 하자.
- 불러오기: `layout.tsx`에서 `import "@fontsource-variable/noto-serif-kr/wght.css"`. 글꼴 이름은 `"Noto Serif KR Variable"`. 대체 글꼴은 `AppleMyungjo`, `"Batang"`, `serif`에 `size-adjust`를 둔 `@font-face`를 Pretendard Fallback과 같은 방식으로 만든다. `next/font/google`도 빌드 때 받아 자체 호스팅하지만, 한국어 조각과 preload 동작은 이번에 확인하지 않았다.

### 2.3 글자 척도 제안

v1 척도(design.md 2.3)를 기반으로 제목만 세리프로 바꾸고 읽는 글을 KRDS 기준 17px로 올린다.

| 토큰 | 글꼴 | 크기/줄높이 | 굵기 | 쓰는 곳 |
|---|---|---|---|---|
| `text-display` (새로) | 세리프 | 32/40 | 600 | 빈 화면 제목, 디자인 시스템 표지 |
| `text-title-lg` | 세리프 | 26/36 | 600 | 페이지 제목 |
| `text-title-md` | 세리프 | 21/30 | 600 | 검토 상세의 법령명 |
| `text-title-sm` | 산세리프 | 16/24 | 600 | 구역 제목 |
| `text-reading` | 산세리프(시험 뒤 세리프 선택지) | 17/30 | 400 | 초안 문장, 조문, 알림 본문. 한 줄 최대 40em |
| `text-ui` | 산세리프 | 15/24 | 400 | 목록, 표 |
| `text-label` | 산세리프 | 14/20 | 500 | 버튼, 라벨 |
| `text-caption` | 산세리프 | 13/18 | 400 | 시간, 보조 설명 |
| `text-micro` | 산세리프 | 12/16 | 600 | 배지 |

세리프 제목의 굵기를 700이 아니라 600으로 둔 이유: Harvey의 세리프 제목도 400~500대로 가볍게 쓴다(fontsinuse 확인). 한글 명조는 굵어질수록 획 대비가 무너져 고딕처럼 보인다. 600은 실제 화면에서 다시 확인할 값이다.

## 3. 라이브러리

모든 버전은 `npm view`로 확인했고 배포일이 오늘(2026-11-24) 기준 2주 넘은 것만 적었다. 크기는 스크래치 폴더에 설치해 esbuild로 묶고(react 제외, 최소화) gzip -9로 잰 값이다. "증분"은 지금 쓰는 Base UI 부품(Dialog, AlertDialog, Popover, Tooltip, Tabs, gzip 54,919B) 위에 더했을 때 늘어나는 양이다.

| 패키지 | 확인한 버전 (배포일) | 라이선스 | React 19 | 크기 gzip (증분) | 권고 |
|---|---|---|---|---|---|
| `class-variance-authority` | 0.7.1 (2024-11-26) | Apache-2.0 | 피어 의존 없음 | cva+clsx 551B | **채택** |
| `tailwind-merge` | **3.7.0** (2026-09-12). 최신 3.7.1은 2026-11-19 배포라 2주가 안 됨 | MIT | 피어 의존 없음 | 8,496B | **채택, 설정 확장 필수** |
| `clsx` | 2.1.1 (2024-04-23) | MIT | 피어 의존 없음 | cva에 포함 | cva 의존으로 따라옴 |
| `next-themes` | 0.4.6 (2025-03-11) | MIT | 피어 `^19` 허용 | 1,461B | **쓰지 않음** |
| `sonner` | 2.0.8 (2026-08-09) | MIT | 피어 `^19.0.0` | 9,660B (+9,365B) | **쓰지 않음** |
| `cmdk` | 1.1.1 (2025-03-14) | MIT | 피어 `^19` | 17,889B (+17,391B) | **쓰지 않음** |
| `vaul` | 1.1.2 (2024-12-14) | MIT | 피어 `^19.0.0` | 22,367B | **쓰지 않음** |
| `motion` | **14.11.1** (2026-11-09). 최신 14.14.0은 2026-11-13 배포라 2주가 안 됨 | MIT | 피어 `^19.0.0` | `motion` 46,637B, `LazyMotion`+`m`+`domAnimation` 32,027B | **지금은 쓰지 않음** |
| `@radix-ui/colors` | 3.0.0 (2023-10-02) | MIT | 해당 없음(CSS) | 해당 없음 | **설치하지 않고 방법만 참고** |
| Base UI `Toast` | `@base-ui/react@1.9.0`에 포함 | MIT | 이미 사용 중 | +6,483B | **채택** |
| Base UI `Autocomplete` | 같은 패키지 | MIT | 이미 사용 중 | +18,790B | **채택** (명령 팔레트) |

### 3.1 채택: cva + tailwind-merge

v1은 "의존성을 줄인다"며 두 패키지를 뺐다(design.md 1.1). v2에서는 밀도 전환, 테마, 출처 네 종류, 상태 네 종류가 곱해져 변형 수가 늘어난다. 지금처럼 `cx()`로 이어 붙이면 화면에서 넘긴 클래스가 부품의 기본 클래스와 충돌해도 알 수 없다. cva는 551B로 변형 표를 타입으로 묶어 주고, tailwind-merge는 충돌한 클래스 중 뒤의 것만 남긴다.

**주의할 점을 직접 재현했다.** tailwind-merge 3.7.0 기본 설정은 LexWatch의 사용자 정의 글자 크기 토큰을 색으로 착각한다.

```
twMerge("text-ui text-ink")      → "text-ink"         (글자 크기 text-ui가 사라짐)
twMerge("text-reading text-label") → "text-label"      (이건 의도대로)
```

`extendTailwindMerge`로 `font-size` 묶음에 `title-lg, title-md, title-sm, reading, ui, label, caption, micro`(v2에서 늘어나는 이름 포함)를 알려 주면 `"text-ui text-ink"`가 그대로 남는다. 이 설정 없이 도입하면 화면 곳곳의 글자 크기가 조용히 사라진다. 도입할 때 이 경우를 단위 테스트로 고정해야 한다. 그림자(`shadow-card` 등)와 모서리 토큰도 같은 확인이 필요하다.

### 3.2 쓰지 않음: next-themes → Next.js 공식 인라인 스크립트

- next-themes 0.4.6은 2025-03 이후 새 배포가 없다. React 19와 Next 16.2에서 "Encountered a script tag while rendering React component" 경고가 나는 이슈 #385(2026-03-19 열림)가 아직 열려 있고 관리자 답이 없다. 사용자들이 dist를 패치하거나 포크(`@wrksz/themes`, `@teispace/next-themes`)로 옮기고 있다. https://github.com/pacocoursey/next-themes/issues/385 (직접 열람)
- Next.js 공식 문서 "Preventing flash before hydration"(2026-11-06 갱신)이 같은 일을 라이브러리 없이 하는 방법을 준다. `<html data-theme="light" suppressHydrationWarning>`의 `<head>`에 `localStorage`나 쿠키를 읽어 `data-theme`을 정하는 인라인 스크립트를 넣는다. 개발 모드의 Strict Mode 재마운트가 속성을 지우므로 토글 부품에서 `useLayoutEffect`로 다시 적용한다. 스크립트 경고를 피하려면 `useServerInsertedHTML`로 넣는다. https://nextjs.org/docs/app/guides/preventing-flash-before-hydration (직접 열람)
- LexWatch가 필요한 것은 "시스템 / 밝게 / 어둡게" 세 값뿐이라 30줄 안쪽이다. 시스템 값일 때는 `data-theme`을 지우고 CSS의 `prefers-color-scheme`이 결정하게 하면 스크립트가 할 일이 더 줄어든다.

### 3.3 쓰지 않음: sonner, cmdk, vaul

- **sonner 대신 Base UI Toast.** Base UI 1.9.0에 Toast가 있다. `Toast.Provider`, `useToastManager().add/close/update/promise`, 기본 3개 제한, 기본 5초, `priority: 'high'`면 제목과 설명을 화면 읽기 프로그램이 읽는다. F6으로 알림 영역에 포커스가 간다. 이미 쓰는 패키지라 증분이 6.5KB로 sonner(9.4KB)보다 작고, 스타일과 접근성 규칙을 한 라이브러리에서 맞출 수 있다. https://base-ui.com/react/components/toast (직접 열람)
- **cmdk 대신 Base UI Dialog + Autocomplete.** cmdk 1.1.1은 의존성으로 `@radix-ui/react-dialog`, `@radix-ui/react-primitive` 등을 끌고 온다. Base UI를 쓰는 앱에 Radix 대화상자가 하나 더 들어오면 포커스 가두기와 레이어 규칙이 두 벌이 된다. Base UI Autocomplete는 그룹(`Autocomplete.Group`, `GroupLabel`), 목록을 팝업 없이 펼치는 `inline`, 첫 항목 강조 `autoHighlight`, 가상화를 지원해 대화상자 안에 넣으면 명령 팔레트가 된다. 증분은 cmdk와 비슷하다(18.8KB 대 17.4KB). 공식 예제에 명령 팔레트는 없다. https://base-ui.com/react/components/autocomplete (직접 열람)
- **vaul은 필요 없다.** 아래에서 끌어올리는 서랍(drawer)은 휴대폰용이다. LexWatch의 휴대폰 사용은 확인용이고(spec 2절), vaul도 Radix Dialog에 의존한다.

### 3.4 지금은 쓰지 않음: motion

32~47KB는 이 앱에서 가장 큰 추가 비용이다. v2에 필요한 움직임(팝오버, 대화상자, 토스트 등장, 분할 화면 크기 조절, 스켈레톤 반짝임)은 CSS 전환과 Base UI의 `data-starting-style`, `data-ending-style` 속성으로 된다. 목록 항목이 자리를 바꾸는 레이아웃 애니메이션이 꼭 필요해질 때 `motion@14.11.1`의 `LazyMotion`으로 다시 검토한다.

### 3.5 설치하지 않음: @radix-ui/colors

패키지는 Radix 척도 전체(약 620KB 풀린 크기)의 CSS 변수를 준다. LexWatch는 30개 남짓한 의미 토큰만 쓰고, 대비 테스트(`src/design/tokens.ts`)가 `tokens.css`의 16진수 값을 읽는다. 척도 값을 가져다 쓰면 Radix 척도 이름과 LexWatch 의미 이름 사이의 연결 층이 하나 더 생긴다. 12단계 역할 규칙만 참고하고 값은 4절처럼 직접 정한다.

## 4. 토큰 제안

### 4.1 브랜드 색: 잉크 남색

세 후보를 비교했다.

| 후보 | 장점 | 문제 |
|---|---|---|
| 옥스블러드(짙은 적갈색) | 법률 서적 장정, 도장 색. 편집 디자인 느낌이 가장 강하다 | 위험 상태(빨강)와 색상각이 같다. 반려 버튼과 주 버튼이 같은 계열이 된다. 신구 대조 삭제색과도 겹친다 |
| 포레스트(짙은 초록) | Legora가 고른 색이라 업계 맥락이 있다 | 성공 상태와 신구 대조 추가색이 초록이다. "승인"과 "주 버튼"이 같은 색이 되면 v1이 고친 문제(초록이 여러 뜻)로 돌아간다 |
| **잉크 남색** | 렉스의 재킷, 지금 로고, 지금 브랜드 `#1F2A44`와 이어진다. 상태 색 넷 중 위험, 경고, 성공과 겹치지 않는다 | 정보(파랑) 상태와 색상각이 가깝다(218° 대 212°) |

잉크 남색을 고른다. 정보 상태와 가까운 문제는 이렇게 푼다. 브랜드는 명도가 낮고 넓은 면(주 버튼, 선택된 탭)에만 쓰고, 정보 상태는 늘 연한 배경 위의 글자와 아이콘으로만 쓴다. 정보 상태가 "진행 중", "승인 대기"처럼 브랜드와 비슷한 뜻을 가지므로 둘이 같은 계열인 것은 GitHub Primer가 `accent`를 정보 색으로 겸하는 것과 같다. 섞여도 오해가 생기지 않는다.

밝은 테마 `#1F3A68`은 지금의 `#1F2A44`보다 채도를 조금 올린 값이다. 지금 값은 거의 검정이라 링크와 일반 글자의 차이가 작았다. 어두운 테마는 `#8FA9E0`(연한 남색)을 쓰고 그 위의 글자는 `#0C1322`(짙은 남색)이다.

### 4.2 구조

- 이름은 Primer처럼 역할 이름만 둔다. 화면 코드는 `bg-canvas`, `text-secondary`처럼 쓰고 테마를 모른다.
- Tailwind 4에서는 `@theme inline { --color-canvas: var(--lw-canvas); ... }`로 클래스를 만들고, 실제 값은 `:root`(밝게), `@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {...} }`, `:root[data-theme="dark"]`에 둔다. 다크 변형은 `@custom-variant dark (&:where([data-theme=dark], [data-theme=dark] *))` 같은 선언이 필요한데, 시스템 설정과 토글을 함께 쓰는 정확한 선택자는 구현 때 확인해야 한다(이번에 실행해 보지 않았다).
- v1 이름과의 대응: `ink`→`text-primary`, `ink-muted`→`text-secondary`, `ink-subtle`→`text-tertiary`, `line`→`border`, `line-strong`→`border-strong`, `brand`→`accent`, `on-brand`→`on-accent`, `source-*`→`prov-*`. 출처는 v1의 회사별 다섯 색(NVIDIA, Cloudflare, Groq, 대체, 재현)을 "누가 만들었나" 네 종류(AI, 규칙, 사람, 재현)로 줄였다. 어느 AI 회사인지는 라벨 글자로 구분한다. 회사가 늘 때마다 색을 늘리면 상태 색과 겹치지 않는 색상각이 곧 바닥난다.
- 손볼 곳: `web/scripts/verify-design-css.mjs`(DS-16)가 지금은 "밝은 모드 규칙"을 요구하고 다크 모드 body 규칙이 있으면 실패한다. v2에서는 거꾸로 두 테마의 값이 모두 있는지 검사하도록 바꿔야 한다. `tokens.ts`의 `parseColorTokens`도 블록별로 읽어야 한다.

### 4.3 값

`surface-raised`는 밝은 테마에서 `surface`와 같은 흰색이고 그림자로 떠 보인다. 어두운 테마에서는 그림자가 잘 안 보여서 바탕을 한 단계 밝게 한다(Carbon 층 방식). `surface-sunken`은 표 머리와 구조문 칸이다.

| 토큰 | 밝게 | 어둡게 |
|---|---|---|
| `canvas` | `#F7F6F3` | `#111214` |
| `surface` | `#FFFFFF` | `#18191C` |
| `surface-raised` | `#FFFFFF` | `#202226` |
| `surface-sunken` | `#EFEDE8` | `#0B0C0E` |
| `border` | `#E2DFD8` | `#2C2E33` |
| `border-strong` | `#86888F` | `#6C707A` |
| `text-primary` | `#1B1D22` | `#ECEDEF` |
| `text-secondary` | `#474B55` | `#B3B7BF` |
| `text-tertiary` | `#61656E` | `#8E939C` |
| `accent` | `#1F3A68` | `#8FA9E0` |
| `accent-hover` | `#2A4A80` | `#A7BCE8` |
| `accent-text` | `#1F3A68` | `#9DB4E6` |
| `on-accent` | `#FFFFFF` | `#0C1322` |
| `accent-subtle` | `#E9EEF7` | `#1A2335` |
| `focus` | `#2F6FDB` | `#6EA2F7` |
| `success` | `#1C6B3A` | `#6CC48C` |
| `success-bg` | `#E7F3EB` | `#12261A` |
| `warning` | `#8A5300` | `#E3B25A` |
| `warning-bg` | `#FBF0D9` | `#2B2110` |
| `danger` | `#B3261E` | `#F08A80` |
| `danger-bg` | `#FBEAE8` | `#331719` |
| `info` | `#1D5FA8` | `#7FB0EE` |
| `info-bg` | `#E7F0FA` | `#132238` |
| `prov-ai` | `#5B3FC4` | `#B6A6F5` |
| `prov-ai-bg` | `#F0ECFC` | `#221C3B` |
| `prov-rule` | `#0B6670` | `#6FC6CE` |
| `prov-rule-bg` | `#E2F2F3` | `#0F272A` |
| `prov-human` | `#8E2C7A` | `#E69DD2` |
| `prov-human-bg` | `#F8E9F4` | `#2D1728` |
| `prov-replay` | `#585F6E` | `#A3A9B5` |
| `prov-replay-bg` | `#FFFFFF` | `#18191C` |
| `diff-del` | `#9B1C1C` | `#F4A3A3` |
| `diff-del-bg` | `#FDECEC` | `#3A1618` |
| `diff-ins` | `#14532D` | `#8FD6A6` |
| `diff-ins-bg` | `#E4F3E8` | `#10291A` |

출처 색이 상태 색과 겹치지 않는지 v1 판정 규칙(채도 0.3 이상이면 색상각 0~70°, 90~170°, 330~360° 금지)으로 다시 계산했다.

| 출처 | 밝게 (색상각, 채도) | 어둡게 (색상각, 채도) | 상태 색 범위와 겹침 |
|---|---|---|---|
| AI `prov-ai` (보라) | 253°, 0.68 | 252°, 0.32 | 없음 |
| 규칙 `prov-rule` (청록) | 186°, 0.90 | 185°, 0.46 | 없음 |
| 사람 `prov-human` (자주) | 312°, 0.69 | 316°, 0.32 | 없음 |
| 재현 `prov-replay` (회색, 점선 테두리) | 221°, 0.20 | 220°, 0.10 | 채도 0.3 미만이라 대상 아님 |

상태 색의 색상각은 성공 143°, 경고 36~39°, 위험 3~5°, 정보 212~214°다. 사람 출처의 자주(312~316°)는 금지 범위 끝(330°)과 14° 떨어져 있다. 색만으로 구분하지 않는 원칙(DS-03, 라벨마다 글자와 아이콘)은 그대로 둔다.

### 4.4 대비 계산

계산 스크립트는 저장소 밖 스크래치 폴더에서 돌렸다. 공식은 지금 `src/design/tokens.ts`의 `contrastRatio`와 같다(WCAG 2.x 상대 휘도). 글자는 4.5:1, 테두리와 포커스 링과 버튼 면은 3:1 기준이다. 47쌍 모두 두 테마에서 통과했다.

| 앞 | 뒤 | 기준 | 밝게 | 어둡게 |
|---|---|---|---|---|
| `text-primary` | `canvas` | 4.5 | 15.60 | 16.00 |
| `text-primary` | `surface` | 4.5 | 16.86 | 15.01 |
| `text-primary` | `surface-raised` | 4.5 | 16.86 | 13.60 |
| `text-primary` | `surface-sunken` | 4.5 | 14.41 | 16.70 |
| `text-secondary` | `canvas` | 4.5 | 8.08 | 9.32 |
| `text-secondary` | `surface` | 4.5 | 8.73 | 8.74 |
| `text-secondary` | `surface-raised` | 4.5 | 8.73 | 7.92 |
| `text-secondary` | `surface-sunken` | 4.5 | 7.46 | 9.73 |
| `text-tertiary` | `canvas` | 4.5 | 5.41 | 6.07 |
| `text-tertiary` | `surface` | 4.5 | 5.84 | 5.70 |
| `text-tertiary` | `surface-raised` | 4.5 | 5.84 | 5.16 |
| `text-tertiary` | `surface-sunken` | 4.5 | 4.99 | 6.34 |
| `accent-text` | `canvas` | 4.5 | 10.43 | 9.02 |
| `accent-text` | `surface` | 4.5 | 11.27 | 8.46 |
| `accent-text` | `surface-raised` | 4.5 | 11.27 | 7.67 |
| `accent-text` | `surface-sunken` | 4.5 | 9.63 | 9.42 |
| `accent-text` | `accent-subtle` | 4.5 | 9.68 | 7.56 |
| `on-accent` | `accent` | 4.5 | 11.27 | 7.88 |
| `on-accent` | `accent-hover` | 4.5 | 8.79 | 9.72 |
| `success` | `success-bg` | 4.5 | 5.73 | 7.52 |
| `success` | `surface` | 4.5 | 6.53 | 8.30 |
| `warning` | `warning-bg` | 4.5 | 5.60 | 8.12 |
| `warning` | `surface` | 4.5 | 6.33 | 9.03 |
| `danger` | `danger-bg` | 4.5 | 5.61 | 6.77 |
| `danger` | `surface` | 4.5 | 6.54 | 7.24 |
| `info` | `info-bg` | 4.5 | 5.61 | 7.12 |
| `info` | `surface` | 4.5 | 6.45 | 7.82 |
| `prov-ai` | `prov-ai-bg` | 4.5 | 6.13 | 7.53 |
| `prov-ai` | `surface` | 4.5 | 7.11 | 8.17 |
| `prov-rule` | `prov-rule-bg` | 4.5 | 5.79 | 7.92 |
| `prov-rule` | `surface` | 4.5 | 6.67 | 8.91 |
| `prov-human` | `prov-human-bg` | 4.5 | 6.43 | 8.00 |
| `prov-human` | `surface` | 4.5 | 7.52 | 8.47 |
| `prov-replay` | `prov-replay-bg` | 4.5 | 6.41 | 7.45 |
| `prov-replay` | `surface` | 4.5 | 6.41 | 7.45 |
| `diff-del` | `diff-del-bg` | 4.5 | 7.14 | 8.12 |
| `diff-ins` | `diff-ins-bg` | 4.5 | 7.93 | 9.11 |
| `border-strong` | `canvas` | 3 | 3.28 | 3.78 |
| `border-strong` | `surface` | 3 | 3.54 | 3.55 |
| `border-strong` | `surface-raised` | 3 | 3.54 | 3.21 |
| `border-strong` | `surface-sunken` | 3 | 3.03 | 3.95 |
| `focus` | `canvas` | 3 | 4.40 | 7.30 |
| `focus` | `surface` | 3 | 4.75 | 6.84 |
| `focus` | `surface-raised` | 3 | 4.75 | 6.20 |
| `focus` | `surface-sunken` | 3 | 4.06 | 7.62 |
| `accent` | `canvas` | 3 | 10.43 | 7.96 |
| `accent` | `surface` | 3 | 11.27 | 7.47 |

가장 빠듯한 쌍: 밝은 테마 `text-tertiary`/`surface-sunken` 4.99, `border-strong`/`surface-sunken` 3.03, 어두운 테마 `border-strong`/`surface-raised` 3.21.

포커스 링에 대해 하나 바꾼 점: v1은 포커스 링과 브랜드 버튼의 대비(3:1)도 검사했다. v2 값으로는 이 쌍이 밝게 2.37, 어둡게 1.09로 떨어진다. 어두운 테마에서는 배경 대비 3:1과 연한 남색 버튼 대비 3:1을 한 색으로 동시에 맞출 수 없다(계산상 필요한 휘도 구간이 겹치지 않는다). 그래서 포커스 링은 항상 `outline-offset: 2px`로 띄워 링 바로 옆이 버튼이 아니라 바탕이 되게 하고, 대비는 링과 네 바탕 사이만 검사한다. v1도 같은 이유로 2px을 띄웠다(design.md 2.4). 띄우지 않는 포커스 표시(표의 행 안쪽 링 등)를 쓰면 이 전제가 깨지니 규칙으로 금지한다.

## 5. 도입할 UI 패턴

우선순위 순서다. P1~P6이 이번 개편의 핵심이고 P7 이후는 여유가 있을 때 한다.

| # | 패턴 | 화면 | 내용 | 참고 |
|---|---|---|---|---|
| P1 | 문서와 근거 분할 화면 | 검토 상세 `/reviews/[id]` | 왼쪽은 초안(읽기 폭 48rem), 오른쪽은 근거 패널(인용 목록, 신구조문). 초안 문장을 고르면 오른쪽이 해당 근거로 스크롤하고 구간을 강조한다. 1280px 미만이면 오른쪽 패널을 접고 지금의 위아래 배치로 돌아간다. 패널 폭은 끌어서 바꾸고 기억한다 | CoCounsel Verify, Harvey 출처 패널 |
| P2 | 인용 미리보기 카드 | 검토 상세, 검토 보조 채팅 | 인용 칩에 마우스를 올리거나 포커스하면 근거 제목, 앞부분 240자, 출처 라벨, 원문 링크가 담긴 카드가 뜬다. 클릭하면 P1의 근거 패널에 고정된다. 지금 DS-09 팝오버(클릭과 Enter로 열림)를 유지하면서 마우스 올림을 더한다. Base UI Popover 트리거의 마우스 올림 열기 옵션(`openOnHover`)을 쓴다. 이 옵션 이름은 이번에 문서로 확인하지 않았다 | Hebbia 인용 미리보기, Legora 셀 카드 |
| P3 | 문장별 검증 상태 | 검토 상세 | 초안 문장마다 크리틱 결과를 "확인됨, 확인 필요, 문제 있음" 셋 중 하나로 왼쪽 여백에 표시한다. 상단에 "확인 필요 3, 문제 1" 요약과 다음 지적으로 이동하는 버튼(DS-18을 확장). 출처 라벨(누가 썼나)과 검증 상태(맞나)를 다른 컴포넌트로 둔다 | Harvey Cite Check |
| P4 | 키보드 단축키 | 승인 대기함 `/`, 검토 상세 | `j`/`k` 다음/이전 행, `Enter` 열기, `]`/`[` 다음/이전 변경이나 지적, `a` 승인, `e` 수정 후 승인, `r` 반려, `?` 단축키 도움말. 승인 단축키는 바로 승인하지 않고 지금의 확인 대화상자를 연다(DS-10 유지). 입력창에 포커스가 있거나 한글 조합 중(`isComposing`)이면 단축키를 무시한다 | Linear, 이전 조사의 IME 주의 |
| P5 | 명령 팔레트 `⌘K` / `Ctrl+K` | 모든 화면 | 검토 건, 실행 ID, 법령명, 법안 검색과 화면 이동, 테마 전환, 밀도 전환, 역할 전환(변호사/파트너/관리자 보기). Base UI Dialog + Autocomplete로 만든다. 한글 IME에서 첫 항목 강조와 Enter 동작을 꼭 시험한다(아래 확인 못 한 것) | Linear, Vercel |
| P6 | 토스트 | 검토 상세, 실행 이력, 운영 현황 | 되돌릴 수 있는 일과 화면 밖에서 끝난 일(재실행 접수, 링크 복사, 설정 저장, 다른 화면으로 넘어간 뒤 끝난 승인)은 Base UI Toast로 알린다. 승인 실패, 반려 사유 검증처럼 사용자가 고쳐야 하는 일은 지금처럼 화면 위 안내 상자(Callout)로 남긴다 | Base UI Toast |
| P7 | 밀도 전환 | 승인 대기함, 실행 이력, 운영 현황, AI 사용 기록 | 표 행 높이를 편안하게 48px, 기본 40px, 촘촘하게 32px 세 단계로. 설정은 명령 팔레트와 표 도구 막대에서 바꾸고 브라우저에 저장한다 | Carbon 표 |
| P8 | 스켈레톤 로더 | 승인 대기함, 검토 상세, 법안 레이더, 운영 현황 | 데이터를 받는 동안 실제 배치와 같은 회색 막대를 보인다. 서버가 잠든 첫 요청은 지금의 렉스 대기 화면(wake-wait)이 맡고, 스켈레톤은 서버가 깨어 있는 일반 로딩에만 쓴다. 움직임 줄이기 설정에서는 반짝임을 끈다 | 일반 관행 |
| P9 | 행 단위 "내가 확인함" | 승인 대기함, 법안 레이더 | 열어 본 건에 표시를 남겨 다시 훑을 때 건너뛰게 한다. 브라우저 저장으로 충분하다 | Legora Mark as Reviewed, Harvey 셀 확인 표시 |
| P10 | 읽은 범위 표시 | 실행 타임라인 `/runs/[id]` | 단계마다 "근거 12건 수집, 9건 인용, 3건 미사용"처럼 AI가 무엇을 읽었는지 한 줄로 | Harvey 범위 노트, CoCounsel 인용 장부 |

v1의 "토스트를 쓰지 않는다" 결정과의 관계: v1은 결정 뒤 화면이 다시 그려져 결과가 이미 보인다는 이유로 토스트를 뺐다. 그 이유는 지금도 맞다. 그래서 P6은 결과가 화면에 바로 보이지 않는 경우로만 범위를 좁혔다. 제공자를 루트에 두는 비용은 Base UI Toast로 6.5KB다.

화면별로 다시 묶으면:

| 화면 | 패턴 |
|---|---|
| 승인 대기함 `/` | P4, P5, P7, P8, P9 |
| 검토 상세 `/reviews/[id]` | P1, P2, P3, P4, P6, P8 |
| 실행 이력과 타임라인 `/runs`, `/runs/[id]` | P6, P7, P10 |
| 운영 현황 `/ops` | P6, P7, P8 |
| AI 사용 기록 `/governance` | P7 |
| 법안 레이더 `/radar` | P8, P9 |
| 알림함 `/inbox` | P5(검색) |
| 디자인 시스템 `/design-system` | 두 테마의 토큰 견본과 대비 값, 읽기 글꼴 비교, 밀도 비교 |

## 6. 확인하지 못한 것

- Harvey, Legora, CoCounsel, Hebbia의 실제 제품 화면. 모두 로그인 뒤에 있다. Harvey 도움말의 릴리스 노트는 로그인으로 넘어가 열지 못했다. Legora의 셀 미리보기 카드, CoCounsel의 Citation Viewer, Hebbia의 셀 클릭 동작은 검색 결과 요약으로만 봤다.
- Harvey의 제목 서체 이름. 사이트 CSS의 이름은 `HarveySerifFont`이고 Fonts In Use는 TWK Ghost라고 한다. 같은 글꼴의 사내 이름일 가능성이 높지만 확인하지 못했다.
- 마루 부리 공식 배포처(hangeul.naver.com)와 리디바탕 원 배포처의 라이선스 원문. 둘 다 산돌구름에 옮겨 둔 내용으로만 확인했다.
- Carbon의 어두운 테마(Gray 90, 100) 층 값. 토큰 페이지 본문에 없었다.
- Geist 글자 척도의 실제 px과 줄높이 값, Geist 다크 모드 처리 방식.
- Base UI Autocomplete와 Toast의 한국어 IME 동작. 문서에 IME 언급이 없다. 명령 팔레트를 만들기 전에 맥 한글 입력기와 윈도우 MS 입력기에서 직접 시험해야 한다.
- Tailwind 4에서 "시스템 설정 + 토글"을 함께 다루는 `@custom-variant` 선택자. 구조는 4.2절에 적었지만 이번에 빌드해 보지 않았다.
- `next/font/google`로 Noto Serif KR을 불러올 때의 한국어 조각 처리와 preload 동작.
- 세리프 제목 굵기 600과 읽는 글 17px이 실제 화면에서 적절한지. 디자인 시스템 화면에서 비교한 뒤 정한다.

## 부록: 확인 명령

```bash
npm view <패키지> version license
npm view <패키지> time --json          # 배포일
npm view <패키지>@<버전> peerDependencies --json
```

크기 측정과 대비 계산, 글꼴 범위 계산(fontTools)은 저장소 밖 스크래치 폴더에서 돌렸고 저장소에는 넣지 않았다.

## 7. 설치된 패키지로 확인한 것 (2026-11-25)

`web/node_modules/@base-ui/react@1.9.0`의 타입 정의로 6절의 확인 못 한 항목 일부를 확인했다.

- Popover 트리거에 `openOnHover`, `delay`, `closeDelay`가 있다.
- 마우스 올림 미리보기 전용 부품 `PreviewCard`(Root, Trigger, Portal, Positioner, Popup, Arrow, Viewport)가 있다. 인용 미리보기(P2)는 Popover의 마우스 올림 옵션보다 이 부품이 맞다. 클릭으로 고정하는 동작은 따로 둔다.
- Autocomplete는 `List`, `Group`, `GroupLabel`, `Item`, `Empty`, `Status`, `useFilter`, `useFilteredItems`를 내보낸다. 명령 팔레트에 필요한 묶음과 빈 결과 표시가 있다.
- Toast는 `Provider`, `Viewport`, `Root`, `Title`, `Description`, `Action`, `Close`와 `useToastManager`, `createToastManager`를 내보낸다.

한글 입력기에서 Autocomplete의 Enter 동작은 여전히 실제 입력으로 확인해야 한다.
