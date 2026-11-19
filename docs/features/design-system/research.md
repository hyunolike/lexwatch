# 디자인 시스템 외부 레퍼런스 조사 (2026-11-19)

LexWatch 화면에 빌려 올 규칙을 외부 디자인 시스템에서 찾았다. 확인하지 못한 항목은 끝에 따로 적었다.

## 1. 참고 시스템과 가져올 것

| 출처 | 내용 | LexWatch에 쓰는 곳 |
|---|---|---|
| IBM Carbon Data table | 행 높이 xs 24, sm 32, md 40(기본), lg 48, xl 64px. 행을 고르면 배치 액션 바가 표 위에 뜨고 행별 액션은 잠긴다 ([링크](https://carbondesignsystem.com/components/data-table/usage/)) | 승인 대기함 행 높이, 나중의 일괄 처리 |
| IBM Carbon Status indicator | 빨강 오류, 주황 심각 경고, 노랑 경고, 초록 성공, 파랑 정보와 진행, 회색 초안과 미시작. 색, 모양, 기호, 글자 중 둘 이상을 쓰고 회색조에서도 구분돼야 한다 ([링크](https://carbondesignsystem.com/patterns/status-indicator-pattern/)) | 실행 상태, 그룹 검토 상태 |
| IBM Carbon Tag | 태그 색은 범주를 나누는 데 쓴다 ([링크](https://carbondesignsystem.com/components/tag/usage/)) | 출처 라벨, 실무그룹 |
| GOV.UK Tag | 상태 표시 전용, 링크나 버튼으로 쓰지 않고 종류를 적게 유지, 색만으로 의미를 전하지 않는다 ([링크](https://design-system.service.gov.uk/components/tag/)) | 상태 라벨 문구 |
| GOV.UK Summary list | 키, 값, 선택적 액션 행. 표 형태 데이터에는 쓰지 않는다 ([링크](https://design-system.service.gov.uk/components/summary-list/)) | 검토 상세의 법령 정보 |
| GOV.UK Error summary | `main` 맨 위에 두고 로드되면 포커스를 받으며 항목이 해당 입력으로 링크한다 ([링크](https://design-system.service.gov.uk/components/error-summary/)) | 반려 사유 검증 |
| Atlassian Lozenge | Lozenge는 워크플로 상태, Badge는 개수, Tag는 분류 메타데이터. 의미 색(success 등)과 의미 없는 accent 색을 섞지 않는다 ([링크](https://atlassian.design/components/lozenge/usage)) | 상태와 개수와 분류를 서로 다른 컴포넌트로 나누는 근거 |
| Microsoft Fluent 2 | 4px 단위 간격 토큰. 긴 글은 단일 칼럼과 적정 줄 길이(manuscript grid) ([링크](https://fluent2.microsoft.design/layout)) | 간격 척도, 초안 본문 폭 |
| KRDS 타이포그래피 | Pretendard GOV, 본문 17px 기준(최소 16px), 줄높이 150% 이상, 자간 0, 굵기 400/500/700, 제목은 본문의 1.25~1.5배, 밑줄은 링크에만 ([링크](https://www.krds.go.kr/html/site/style/style_03.html)) | 본문 글꼴과 크기 |
| KRDS 색 | 일반 텍스트 4.5:1, 큰 텍스트 3:1, 색만으로 정보 전달 금지, 적록 조합 회피 ([링크](https://www.krds.go.kr/html/site/style/style_02.html)) | 대비 검사 기준 |
| WCAG 2.2 AA | 텍스트 4.5:1(큰 텍스트 3:1), UI 구성요소 3:1, 색만으로 의미 전달 금지 ([1.4.3](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html), [1.4.11](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html), [1.4.1](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html)) | 토큰 대비 테스트 |

리걸테크 제품(Harvey, CoCounsel, Clio, LexisNexis, 엘박스)은 공개 디자인 가이드를 찾지 못했다. Harvey는 출처 패널 개편과 문서 안 인용을 릴리스 노트에서 언급한다([링크](https://eu.help.harvey.ai/release-notes/improved-sources-experience)). 인용을 본문 옆에서 바로 열어 보는 방식은 이 제품의 검토 흐름과 맞는다.

## 2. 신구 대조(레드라인)

- 삭제는 취소선, 삽입은 밑줄이 관행이다. 이를 정한 공식 표준은 찾지 못했다.
- `<ins>`, `<del>`을 쓰면 JAWS와 NVDA가 삽입과 삭제를 읽어 준다([링크](https://phabricator.wikimedia.org/T324760)).
- 색과 함께 기호나 숨김 텍스트("추가됨", "삭제됨")를 붙인다([링크](https://lists.wordpress.org/pipermail/wp-trac/2018-March/352864.html)).
- 변경 덩어리 사이를 키보드로 이동하게 한다([링크](https://dev.to/babycat/build-an-accessible-code-diff-with-one-reading-order-and-navigable-hunks-36hd)).
- 국가법령정보센터 신구조문 응답은 바뀐 부분을 `<P>…</P>`로 감싸 오지만 백엔드가 근거 저장 때 이 표시를 지운다(#4). 화면에서는 구조문과 신조문을 비교해 다시 계산한다.

## 3. 라이브러리

| 선택지 | 확인한 사실 |
|---|---|
| shadcn/ui | Tailwind v4, React 19 지원. 코드를 프로젝트에 복사해 소유하는 방식. 2026-07부터 Base UI가 기본이고 Radix(`-b radix`)와 React Aria(`--base aria`)를 고를 수 있다. Radix는 지원 중단이 아니다. Toast는 2026-07 추가 ([changelog](https://ui.shadcn.com/docs/changelog), [Base UI 기본](https://ui.shadcn.com/docs/changelog/2026-07-base-ui-default), [React Aria](https://ui.shadcn.com/docs/changelog/2026-07-react-aria)) |
| Base UI | `@base-ui/react` v1.0 안정판 2025-12 ([링크](https://base-ui.com/react/overview/releases/v1-0-0.md)) |
| React Aria Components | 한국어 IME에서 Autocomplete 첫 항목 포커스가 안 되던 문제가 2026-06-09 수정됨(#10156) ([링크](https://github.com/adobe/react-spectrum/issues/10126)) |
| Radix Primitives | WorkOS 관리. 2026년 활동 상태는 확인하지 못함 |

Enter로 보내는 입력은 `KeyboardEvent.isComposing`을 확인해야 한글 조합 중 전송이 일어나지 않는다([링크](https://dev.classmethod.jp/en/articles/react-ime-composition-pitfalls/)).

## 4. 글꼴

- Pretendard: SIL OFL, 가변 폰트, `next/font/local`에서 가변 woff2를 쓸 때 `weight: '45 920'`을 지정해야 WebKit에서 굵기가 맞다([링크](https://github.com/orioncactus/pretendard)). 영문은 Inter 기반이다.
- v1.3.9 가변 woff2 전체 약 2.06MB, 동적 서브셋 92조각(평균 약 32KB) ([링크](https://data.jsdelivr.com/v1/packages/npm/pretendard@1.3.9)).
- 대안: SUIT, Wanted Sans(둘 다 OFL, 가변), Noto Sans KR.

## 확인하지 못한 것

Fluent 2 웹의 밀도 규격, 리걸테크 제품의 레드라인 화면, Radix의 2026년 활동, Ark UI의 React 19와 Tailwind v4 공식 지원, Pretendard의 `tnum` 지원, cmdk의 한국어 IME 동작.
