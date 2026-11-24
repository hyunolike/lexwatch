# 설계: LexWatch 디자인 시스템

- 상태: 검토 반영본 v2 (2026-11-19). EGM 통합 검토 결과는 [reviews/consolidated_review_report.md](reviews/consolidated_review_report.md)
- 작성: hh.jang
- 명세: [spec.md](spec.md) / 조사: [research.md](research.md) / 에셋 기록: [assets.md](assets.md)
- 대체하는 규정: [기존 설계 문서](../../superpowers/specs/2026-11-16-lexwatch-design.md) 5.3절의 정직성 배지 색(초록/주황/회색)은 이 문서 2.1절로 바꾼다

## 0. 읽기 전에

### 0.1 이 문서에 나오는 제품 용어

| 용어 | 뜻 | 화면에 오는 값 |
|---|---|---|
| 크리틱 | 초안 문장을 세 단계로 검사해 문제를 적발하는 단계. 1단 인용 규칙, 2단 조문번호·날짜·숫자 대조, 3단 과잉해석 | 문장마다 `findings[]`(`tier`, `ruleId`, `message`) |
| 출처 | 단계나 답변을 누가 만들었는지. NVIDIA 모델이 실행했으면 `nvidia`, 키가 없거나 실패해 규칙 기반 대체 구현이 만들었으면 `fallback`, 녹화한 데모 데이터를 다시 재생했으면 `replay`, 해당 없음은 `none` | `providerKind` |
| 신뢰도 | 분류 단계가 그룹 관련성을 판단한 확신 정도, 0~1 실수 | `confidence` (없으면 `null`) |
| 검토 보조 | 검토 상세 화면 아래의 채팅. 그 개정안의 근거 안에서만 답한다 | `ReviewChat.tsx` |
| 서버 깨우기 | API가 무료 호스팅(Render)에 있어 잠들었다가 첫 요청에 1분쯤 걸려 깨어난다. 그동안 보여 주는 대기 화면 | `WakeGate.tsx` |
| 역할 | 로그인 없이 "변호사로 보기"(승인 가능), "파트너로 보기"(적발이 남은 승인의 최종 승인, [ai-governance](../ai-governance/spec.md)), "관리자로 보기"(재실행, AI 사용 기록)를 고른다 | `X-Demo-Role` 헤더 |

### 0.2 요구사항 요약 (자세한 수용 기준은 spec.md)

| ID | 요약 |
|---|---|
| DS-01 | 전역 CSS가 Tailwind 클래스를 덮어쓰지 않는다 |
| DS-02 | Pretendard로 모든 환경에서 같은 글꼴. 읽는 글 16px 이상 |
| DS-03 | 출처 색과 상태 색 분리 |
| DS-04 | WCAG 2.2 AA 대비 |
| DS-05 | 영어 상태값을 화면에 내보내지 않음 |
| DS-06 | 공용 부품으로 모양을 한 곳에서 관리 |
| DS-07 | 키보드만으로 모든 동작, 보이는 포커스 링 |
| DS-08 | 신구조문의 바뀐 부분 표시 |
| DS-09 | 인용 근거를 팝오버로 확인 |
| DS-10 | 승인 전에 한 번 더 확인 |
| DS-11 | 승인 대기함을 빠르게 훑기 |
| DS-13 | 앱 안 디자인 시스템 화면 |
| DS-14 | 로고와 파비콘 |
| DS-15 | 3D 마스코트 "렉스"를 정해진 곳에만 |

DS-12(Enter 전송)는 검토 결과 이번 범위에서 뺐다(1.2절).

## 1. 결정

### 1.1 결정과 근거

| 항목 | 결정 | 근거 | 관련 요구 |
|---|---|---|---|
| UI 부품 바탕 | `@base-ui/react` 1.9.0을 직접 쓴다. shadcn/ui는 쓰지 않는다 | 맞춤 부품(출처 라벨, 신구 대조, 인용 칩)은 어차피 직접 만든다. 남는 기본 부품(Button, AlertDialog, Popover, Tooltip, Tabs, Meter)은 Base UI가 접근성 동작(포커스 가두기, Esc, ARIA)을 주고 스타일만 입히면 된다. CLI가 `globals.css`를 바꾸는 위험과 cva, clsx, tailwind-merge 의존성이 사라진다 | DS-06, DS-07 |
| 색 정의 | `web/src/styles/tokens.css`의 `@theme` 블록이 유일한 원본. Tailwind 기본 색 팔레트는 `--color-*: initial`로 비운다 | 값을 한 곳에만 적는다. 기본 팔레트를 비우면 `slate-500` 같은 원색 클래스는 CSS가 생성되지 않아 바로 드러난다. 대비 테스트와 `/design-system` 화면은 이 파일을 읽는다 | DS-03, DS-04, DS-06 |
| 글꼴 | Pretendard Variable 1.3.9. npm `pretendard` 패키지의 동적 서브셋 CSS(`pretendard/dist/web/variable/pretendardvariable-dynamic-subset.css`)를 `layout.tsx`에서 import | 자체 호스팅이고, 화면에 나온 글자가 속한 조각만 받는다. 조각 하나 평균 약 32KB(research.md 4절). 정부 표준(KRDS)과 같은 계열이고 영문이 Inter 기반 | DS-02 |
| 신구 대조 | 공식 변경 표시가 있으면 그것을 그리고, 없으면(옛 녹화 데이터) 화면에서 `diff` 9.0.0 `diffWordsWithSpace`로 계산한다. 화면은 둘을 구분해 알린다 | 법제처가 바뀐 부분을 `<P>…</P>`로 표시해 주므로 그대로 쓰는 것이 가장 정확하다. 대체 계산은 한글에서 조사 단위까지 나뉜다(2026-11-19 확인: "근로자는"→"근로자가"는 `-는 +가`, "검찰청"→"공소청"은 `-검찰 +공소`) | DS-08 |
| 백엔드 범위 | 근거 본문 앞부분과 공식 변경 표시를 API로 내보낸다(3절) | 작성자 결정(2026-11-19, 검토 보고서 A4). 인용 팝오버에 근거 본문을 보여 주려면 API에 본문이 있어야 하고, 공식 표시를 쓰려면 수집 때 보존해야 한다 | DS-08, DS-09 |
| 결과 안내 | 토스트를 쓰지 않는다. 결정 결과와 오류는 화면 위 `aria-live="polite"` 안내 상자로 | 결정 뒤 화면이 다시 그려져 상태 표시가 바뀌므로 결과가 이미 보인다. 토스트는 루트 레이아웃에 제공자를 두어야 해서 비용에 비해 얻는 것이 적다 | DS-10 |
| 다크 모드 | 하지 않는다. `color-scheme: light` | spec 3절 | DS-01 |

### 1.2 검토한 대안

| 항목 | 버린 대안 | 버린 이유 |
|---|---|---|
| UI 부품 바탕 | shadcn/ui + Base UI | 위 1.1. 작성자가 검토 뒤 직접 사용을 골랐다 |
| | React Aria Components | 접근성 처리는 가장 엄격하지만 API가 길고, 이 화면에는 한글을 입력받는 Base UI 부품이 없어 IME 수정 이력의 이점이 작다 |
| | Radix | 2026년 유지보수 상태를 확인하지 못했다(research.md 3절) |
| 글꼴 | SUIT, Wanted Sans | 품질은 비슷하지만 공공 표준 계열이 아니고 영문 숫자 모양이 Inter 기반이 아니다 |
| | Noto Sans KR (`next/font/google`) | preload와 크기 보정을 자동으로 받지만 Pretendard보다 자간이 넓어 표가 길어진다 |
| 글꼴 불러오기 | `next/font/local`로 가변 woff2 하나 | preload와 size-adjust를 받지만 파일이 약 2MB라 첫 화면에서 전체를 받는다. 대신 대체 글꼴에 `size-adjust`를 직접 지정한다(2.2) |
| 신구 대조 | 화면 계산만 | 공식 표시와 다를 수 있다. 대체 경로로만 남긴다 |
| | 근거 본문에 `<P>` 표시를 남김 | 크리틱 2단이 근거 본문의 숫자와 날짜를 대조하므로 표시가 섞이면 안 된다. 별도 필드에 둔다 |
| 색 정의 | `tokens.ts` 원본 + CSS 사본 | 같은 값을 두 번 적고 동기화 테스트가 하나 더 든다 |
| | oklch 표기 | 대비 테스트에서 16진수가 계산이 단순하다. Tailwind는 16진수도 받는다 |
| Enter 전송 (옛 DS-12) | 넣기 | 지금은 버튼으로만 보내 한글 조합 중 전송 문제가 없다. 새 기능이 위험을 만들고 하루 질문 한도를 깎을 수 있다 |

## 2. 토큰

모든 토큰은 `web/src/styles/tokens.css`의 `@theme` 블록에 있다. 화면 코드는 `bg-surface`, `text-ink-muted`, `border-line` 같은 토큰 클래스만 쓴다.

### 2.1 색

| 토큰 | 값 | 쓰는 곳 |
|---|---|---|
| `--color-canvas` | `#F6F7F9` | 페이지 배경 |
| `--color-surface` | `#FFFFFF` | 카드, 패널, 입력 |
| `--color-surface-sunken` | `#EEF1F5` | 표 머리, 구조문 칸, 중립 배지 바탕 |
| `--color-ink` | `#1A2233` | 본문 |
| `--color-ink-muted` | `#4A5568` | 보조 정보 |
| `--color-ink-subtle` | `#5F6779` | 캡션, 시간 |
| `--color-line` | `#D9DEE7` | 카드 테두리, 구분선(장식이라 대비 기준 없음) |
| `--color-line-strong` | `#737D93` | 입력 테두리, 재현 라벨 점선 |
| `--color-brand` | `#1F2A44` | 주 버튼, 로고 글자. 마스코트 재킷과 같은 남색 |
| `--color-brand-hover` | `#2C3B5E` | 주 버튼에 마우스를 올렸을 때 |
| `--color-on-brand` | `#FFFFFF` | 주 버튼과 위험 버튼의 글자 |
| `--color-focus` | `#1F6FEB` | 포커스 링 |
| `--color-success`, `--color-success-bg` | `#1E6B3A`, `#E7F4EC` | 단계 성공, 승인, 알림 발송 |
| `--color-danger`, `--color-danger-bg` | `#B3261E`, `#FCEBEA` | 실패, 반려, 크리틱 적발 |
| `--color-warning`, `--color-warning-bg` | `#8A5A00`, `#FFF4D6` | 검토 필요, 재시도 예정 |
| `--color-info`, `--color-info-bg` | `#1D4ED8`, `#E8EFFD` | 진행 중, 승인 대기, 일반 안내 |
| `--color-neutral`, `--color-neutral-bg` | `#4A5568`, `#EEF1F5` | 대기, 관련 없음, 모르는 값 |
| `--color-source-nvidia`, `--color-source-nvidia-bg` | `#5B3FC4`, `#EFEBFC` | 출처 NVIDIA |
| `--color-source-cloudflare`, `--color-source-cloudflare-bg` | `#8E2C7A`, `#F8E9F4` | 출처 Cloudflare Workers AI (2026-11-19 추가, [명세](../cloudflare-ai/spec.md)) |
| `--color-source-groq`, `--color-source-groq-bg` | `#0B6670`, `#E2F3F5` | 출처 Groq (2026-11-20 추가, [명세](../llm-failover/spec.md)) |
| `--color-source-fallback`, `--color-source-fallback-bg` | `#3D4A63`, `#E9ECF2` | 출처 대체 구현 |
| `--color-source-replay`, `--color-source-replay-bg` | `#5A6172`, `#FFFFFF` | 출처 재현(테두리는 `line-strong` 점선) |
| `--color-diff-del`, `--color-diff-del-bg` | `#9B1C1C`, `#FDECEC` | 신구 대조 삭제 |
| `--color-diff-ins`, `--color-diff-ins-bg` | `#14532D`, `#E6F4EA` | 신구 대조 추가 |

출처 색은 상태 색 계열을 쓰지 않는다. 출처 라벨은 품질 판정이 아니라 "누가 만들었나"를 알려 주는 분류라서다. IBM Carbon은 태그 색을 범주 구분에 쓰고 상태는 별도 패턴으로 다루며, Atlassian은 의미 색과 장식 색을 섞지 말라고 한다(research.md 1절). 판정 규칙: 채도(HSV S)가 0.3 이상인 출처 색은 색상각이 0~70°, 90~170°, 330~360° 범위(빨강, 주황, 노랑, 초록)에 들면 안 된다.

신구 대조 색은 빨강과 초록 관행을 따르되 신구 대조 영역 안에서만 쓴다. 색만으로 구분하지 않도록 삭제에는 취소선, 추가에는 두께 2px 밑줄을 함께 쓴다. KRDS는 밑줄을 링크에만 쓰라고 하지만, 추가 표시는 배경색과 두꺼운 밑줄을 함께 쓰고 링크가 될 수 없는 본문 안에만 나와 링크와 헷갈리지 않는다.

### 2.2 대비 검사 쌍

`web/src/design/contrast.test.ts`가 `tokens.css`를 읽어 아래 쌍을 모두 검사한다. 값은 2026-11-19 계산값이다.

| 앞 | 뒤 | 기준 | 계산값 |
|---|---|---|---|
| `ink`, `ink-muted`, `ink-subtle` | `canvas`, `surface`, `surface-sunken` | 4.5 | 최저 5.01 (`ink-subtle`/`surface-sunken`) |
| `brand` | `canvas`, `surface` | 4.5 | 13.30 |
| `on-brand` | `brand`, `danger` | 4.5 | 14.26, 6.54 |
| 상태 다섯 색 | 각자의 `-bg` | 4.5 | 최저 5.41 (`warning`) |
| 출처 세 색 | 각자의 `-bg` | 4.5 | 최저 6.08 (`source-nvidia`) |
| `diff-del`, `diff-ins` | 각자의 `-bg` | 4.5 | 7.14, 8.02 |
| `line-strong` | `canvas`, `surface`, `surface-sunken` | 3.0 | 최저 3.65 |
| `focus` | `canvas`, `surface`, `surface-sunken`, `brand` | 3.0 | 최저 3.08 (`brand`) |

### 2.3 글자

| 토큰(클래스) | 크기/줄높이 | 굵기 | 쓰는 곳 |
|---|---|---|---|
| `text-title-lg` | 24/32px | 700 | 페이지 제목 |
| `text-title-md` | 20/28px | 700 | 상세 화면 법령명 |
| `text-title-sm` | 16/24px | 600 | 구역 제목 |
| `text-reading` | 16/28px | 400 | 초안 문장, 조문 본문, 알림 본문. 한 줄 최대 `40em`(한글 약 40자) |
| `text-ui` | 15/24px | 400 | 목록, 표, 일반 UI |
| `text-label` | 14/20px | 500 | 버튼, 라벨 |
| `text-caption` | 13/18px | 400 | 시간, 보조 설명 |
| `text-micro` | 12/16px | 600 | 배지 |

자간은 모두 0. 숫자 열(신뢰도, 건수, 시도 횟수, 시간)은 `tabular-nums`. KRDS의 본문 16px, 줄높이 150% 이상 기준은 읽는 글(`text-reading`)에 적용한다. 목록과 버튼은 한 화면에 더 많은 행을 보이도록 15px 이하를 쓴다. spec DS-02를 같은 내용으로 고쳤다.

글꼴 묶음: `--font-sans: "Pretendard Variable", "Pretendard Fallback", system-ui, -apple-system, "Apple SD Gothic Neo", "Malgun Gothic", sans-serif`. `Pretendard Fallback`은 `local("Apple SD Gothic Neo"), local("Malgun Gothic")`에 `size-adjust: 96%`를 준 `@font-face`로, 글꼴을 받기 전후의 줄바꿈 차이를 줄인다. 고정폭이 필요한 실행 ID는 `ui-monospace, SFMono-Regular, Menlo, monospace`.

### 2.4 간격, 모서리, 그림자, 움직임, 포커스

| 토큰 | 값 | 쓰는 곳 |
|---|---|---|
| 간격 | Tailwind 기본 4px 단위 | 카드 안쪽 `p-4`, 목록 행 위아래 각각 `py-3` |
| `--radius-sm` | 4px | 배지 |
| `--radius-md` | 8px | 버튼, 입력 |
| `--radius-lg` | 12px | 카드, 대화상자, 팝오버 |
| `--shadow-card` | `0 1px 2px rgb(26 34 51 / 0.06)` | 카드 |
| `--shadow-pop` | `0 8px 24px rgb(26 34 51 / 0.14)` | 팝오버, 툴팁 |
| `--shadow-dialog` | `0 24px 48px rgb(26 34 51 / 0.24)` | 대화상자 |
| `--ease-out` | `cubic-bezier(0.2, 0, 0, 1)` | 모든 움직임 |
| 시간 | 120ms(마우스 올림), 200ms(팝오버, 탭), 240ms(대화상자) | `prefers-reduced-motion: reduce`이면 0ms |

`--radius-*`는 Tailwind 기본 척도를 덮어쓴다. 포커스 링은 모든 대화형 요소에 같은 규칙을 쓴다: `focus-visible:outline-2 outline-offset-2 outline-focus`(불투명 2px, 2px 띄움). 띄운 틈은 뒤 배경색이라 남색 버튼에서도 링이 버튼과 붙어 보이지 않는다.

## 3. 백엔드 변경

### 3.1 공식 변경 표시 보존

- `LawApiClient.ArticleTexts`가 `<P>`, `</P>`를 지울 때, 지운 뒤 글 기준의 구간 `[시작, 끝)` 목록을 함께 만든다. 한 조문의 항목을 줄바꿈으로 이어 붙이므로 구간도 그 위치 기준이다.
- 짝이 맞지 않는 표시: 여는 `<P>`만 있으면 그 항목 끝에서 닫는다. 길이가 0인 구간은 버린다. 항목 앞뒤 공백을 잘라 낸 뒤의 위치를 쓴다.
- `OldNewArticle`에 `OldMarks`, `NewMarks`(`IReadOnlyList<(int Start, int End)>`)를 더한다.
- `Evidence`에 `ChangeMarksJson`(nullable 문자열) 열을 더하는 하위 호환 마이그레이션 `EvidenceChangeMarks`. 값은 `{"old":[[s,e],...],"new":[[s,e],...]}`. `oldnew` 근거에만 채운다. 근거 `Body` 형식은 바꾸지 않는다(크리틱 2단과 에이전트가 읽음).
- 녹화 재현 데이터와 기존 실행에는 값이 없다(`null`). 화면은 대체 계산으로 그린다.

### 3.2 검토 API 응답

`GET /api/reviews/{id}`:

- `oldNew[]`: `{ articleNo, before, after, beforeMissing, afterMissing, oldMarks, newMarks }`. `beforeMissing`과 `afterMissing`은 해당 칸이 `"(없음)"`이었는지. `oldMarks`, `newMarks`는 `[시작, 끝][]` 또는 `null`.
- `evidence[]`: 기존 필드에 `excerpt`(근거 본문 앞 240자, 넘으면 끝에 `…`)를 더한다. `oldnew` 근거는 신조문 부분에서 자르고, 신조문이 `"(없음)"`(삭제 조문)이면 구조문 부분에서 자른다.

### 3.3 백엔드 테스트

- `LawApiRecordedTests`: 녹화본 `oldnew_005698.json` 제7조. `NewMarks`는 2개이고 각각 `"공소청(광역공소청, 지방공소청 및 지방공소청 지청을 포함한다. 이하 같다)"`와 `"공소청"`을 가리킨다. `OldMarks`는 2개이고 둘 다 `"검찰청"`을 가리킨다.
- `FetchArticlesStep` 테스트(기존 `CollectStepTests`에 추가): `oldnew` 근거의 `ChangeMarksJson`이 저장된다.
- 검토 API 테스트(`ReviewEndpointTests`): `excerpt`와 `beforeMissing`, `oldMarks`가 응답에 있다.

### 3.4 백엔드 파일

| 경로 (`backend/` 아래) | 작업 |
|---|---|
| `src/LexWatch.Api/LawApi/LawApiClient.cs` | 고침: `ArticleTexts`가 글과 구간을 함께 돌려줌, `OldNewArticle`에 `OldMarks`, `NewMarks` |
| `src/LexWatch.Api/Data/Entities/Entities.cs` | 고침: `Evidence.ChangeMarksJson` (`string?`) |
| `src/LexWatch.Api/Data/Migrations/*_EvidenceChangeMarks.cs`, `.Designer.cs`, `LexWatchDbModelSnapshot.cs` | 만듦/고침: `dotnet ef migrations add EvidenceChangeMarks --project src/LexWatch.Api` |
| `src/LexWatch.Api/Workflow/Steps/FetchArticlesStep.cs` | 고침: `Add` 헬퍼가 `changeMarksJson`을 받고 `oldnew` 근거에 `{"old":[[s,e]],"new":[[s,e]]}`를 넣음 |
| `src/LexWatch.Api/Reviews/ReviewEndpoints.cs` | 고침: `ToOldNew`에 3.2 필드, `evidence` 투영에 `excerpt` |
| `tests/LexWatch.Tests/LawApiRecordedTests.cs`, `CollectStepTests.cs`, `ReviewEndpointTests.cs` | 고침: 3.3 |

## 4. 부품

### 4.1 파일

| 경로 (`web/src/` 아래) | 작업 | 내용 |
|---|---|---|
| `styles/tokens.css` | 만듦 | 2절 토큰 |
| `app/globals.css` | 고침 | `@import "tailwindcss"`, `@import "../styles/tokens.css"`, `@layer base`에 `html { color-scheme: light }`, `body { bg-canvas text-ink font-sans }`. 레이어 밖 규칙과 다크 모드 블록, Geist 변수를 지움 |
| `lib/cx.ts` | 만듦 | 클래스 이어 붙이기 함수(의존성 없이 5줄) |
| `lib/labels.ts` | 만듦 | 5.2절. `api.ts`의 `STEP_LABELS`, `stepLabel`과 `ReplayNote.tsx`의 `REPLAY_NOTE` 문구를 옮겨 옴 |
| `design/tokens.ts` | 만듦 | `parseColorTokens(css: string): Record<string, string>`, `contrastRatio(a: string, b: string): number`, `hsv(hex: string): {h, s, v}`, `CONTRAST_PAIRS`(2.2절 표). 테스트와 `/design-system` 화면이 함께 씀 |
| `lib/redline.ts` | 만듦 | 5.1절 |
| `lib/api.ts` | 고침(단계 3) | `OldNew`, `EvidenceItem` 타입에 3.2 필드. `stepLabel`을 `labels.ts`에서 다시 내보냄 |
| `components/ui/Button.tsx` | 만듦 | Base UI `button` |
| `components/ui/ConfirmDialog.tsx` | 만듦 | Base UI `alert-dialog` |
| `components/ui/Floating.tsx`(Popover, Tooltip), `Tabs.tsx` | 만듦 | Base UI 해당 부품에 토큰 스타일 |
| `components/ds/badges.tsx`(StatusLozenge, SourceLabel, CountBadge, ConfidenceMeter), `Callout.tsx`, `layout.tsx`(Mascot, EmptyState, SummaryList, PageHeader, CARD) | 만듦 | 4.2 |
| `components/ds/CitationChip.tsx` | 만듦 | `DraftEditor.tsx` 안의 `Chip`을 대체 |
| `components/ds/RedlineDiff.tsx` | 만듦 | `OldNewDiff.tsx`를 대체 |
| `components/ds/AppHeader.tsx` | 만듦 | `Nav.tsx`, `DemoBanner.tsx`, 각 화면의 `RoleSwitch` 배치를 합침. 경로를 읽는 클라이언트 부분은 지금처럼 `layout.tsx`에서 `<Suspense>`로 감쌈(`cacheComponents`) |
| `components/ProviderBadge.tsx`, `OldNewDiff.tsx`, `Nav.tsx`, `DemoBanner.tsx`, `ReplayNote.tsx` | 지움 | 위 부품이 대체 |
| `components/RoleSwitch.tsx`, `Timeline.tsx`, `DraftEditor.tsx`, `ReviewChat.tsx`, `WakeGate.tsx` | 고침 | 토큰 클래스와 ds 부품으로 |
| `app/layout.tsx`, `page.tsx`, `inbox/page.tsx`, `runs/page.tsx`, `runs/[id]/RunClient.tsx`, `reviews/[id]/ReviewClient.tsx` | 고침 | 4.3 |
| `app/design-system/page.tsx` | 만듦 | DS-13 |
| `app/icon.png` | 만듦 | 64x64. `uv run --with pillow python -I -c "from PIL import Image; Image.open('web/public/mascot/lex-avatar.webp').resize((64,64), Image.LANCZOS).save('web/src/app/icon.png')"` |
| `app/favicon.ico` | 지움 | 기본 Next 아이콘 |
| `design/contrast.test.ts`, `design/palette.test.ts` | 만듦 | 6절 |
| `lib/labels.test.ts`, `lib/redline.test.ts` | 만듦 | 6절 |
| `web/public/mascot/*.webp` (6장), `scripts/process_mascot.py` | 이미 만듦 | 그대로 커밋 |
| `web/public/{file,globe,next,vercel,window}.svg` | 지움 | 쓰지 않는 기본 파일 |

의존성(정확한 버전 고정): `@base-ui/react@1.9.0`, `diff@9.0.0`, `pretendard@1.3.9`, `lucide-react@1.75.0`(아이콘). 개발 의존성 추가 없음.

### 4.2 부품 규칙

| 부품 | props | 규칙 |
|---|---|---|
| `Button` | `variant: "primary" \| "secondary" \| "danger" \| "danger-ghost" \| "ghost"`, `size: "sm" \| "md"`, `loading?: boolean` | 높이 32px/36px. `loading`이면 앞에 회전 아이콘, `aria-busy`, 클릭 막음. 비활성은 `opacity-50` 하나 |
| `SourceLabel` | `kind: ProviderKind \| string`, `tooltip?: boolean` | `none`이나 빈 값은 그리지 않는다. 링크 안(목록 행)처럼 포커스를 받으면 안 되는 곳에서는 `tooltip={false}`로 `title`과 숨김 글로만 설명한다. 아이콘+글자+툴팁. NVIDIA(`Cpu` 아이콘) "NVIDIA Nemotron이 실제로 처리했어요", 대체 구현(`Wrench`) "모델 대신 규칙 기반 대체 구현이 만들었어요. 품질 판정이 아니에요", 재현(`History`, 점선 테두리) "녹화한 예시를 다시 보여 주는 데모 데이터예요". 모르는 값은 원문을 `neutral`로 |
| `StatusLozenge` | `domain`, `value` | `statusLabel`(5.2)로 라벨과 의미를 얻어 색 점+라벨 |
| `CountBadge` | `count`, `label`, `tone?: "neutral" \| "danger"` | 부르는 쪽이 `tone`을 정한다. 크리틱 적발 수만 `danger`로 부르고, 0이면 그려도 `neutral` |
| `ConfidenceMeter` | `value: number \| null` | 숫자는 `Math.round(v*100)%`. 막대 5칸, 채울 칸은 `Math.round(v*5)`, 0~1 밖은 잘라 낸다. `null`이면 "—"만. Base UI `meter`로 `aria-valuenow` |
| `Callout` | `tone: "info" \| "success" \| "warning" \| "danger"`, `title?`, `live?: boolean` | 아이콘+제목+본문. `live`이면 `role="status"` `aria-live="polite"`. 정적인 크리틱 표시는 `live` 없이 |
| `CitationChip` | `id`, `evidence?: EvidenceItem`, `onRemove?` | 칩을 누르거나 Enter면 팝오버: 제목, `excerpt`, 원문 링크(있을 때). 근거가 없으면 경고 아이콘과 "근거를 찾을 수 없어요". 편집 중에는 제거 버튼이 칩 오른쪽에 따로 있다 |
| `RedlineDiff` | `items: OldNew[]` | 조문마다 카드 하나. 768px 이상은 두 칸, 미만은 조문마다 탭(구조문/신조문). CSS(`md:`)로 전환해 서버와 브라우저 결과가 같다. 카드 머리에 "변경 n곳"과 출처 표시("법제처 표시" 또는 "화면에서 비교") |
| `SummaryList` | `rows: {term, value}[]` | `<dl>` 두 열. 검토 상세의 법령 정보(GOV.UK Summary list 패턴, research.md 1절) |
| `Mascot` | `pose: "avatar" \| "empty-queue" \| "empty-inbox" \| "waking" \| "error"`, `size: number` | `<img>`로 `/mascot/lex-{pose}.webp`. `alt=""`, `aria-hidden`. 표시 크기: 헤더 32px, 빈 화면과 대기, 오류 화면 180px. 파일은 표시 크기의 2배 이상(avatar 128px, 나머지 360px). `lex-master`는 `/design-system`에서만 |
| `EmptyState` | `pose`, `title`, `description`, `action?` | 마스코트 위, 제목과 설명 아래 |
| Card | (부품 없음) | `rounded-lg border border-line bg-surface shadow-card` 클래스 조합. 부품으로 만들 만큼 변형이 없다 |

마스코트 금지 위치: 승인과 반려 버튼 주변, 확인 대화상자, 초안 본문, 신구조문, 실행 타임라인.

### 4.3 화면 적용

| 화면 | 바뀌는 것 |
|---|---|
| 공통 (`layout.tsx`) | `AppHeader`: 데모 안내 띠(높이 28px, `sticky top-0`) 아래에 렉스 아바타, "LexWatch", 내비게이션, 역할 전환을 한 줄로. 맨 앞에 "본문으로 건너뛰기" 링크(`#main`). `main`에 `id="main"`. 각 화면의 `<RoleSwitch />`를 지움 |
| 승인 대기함 (`page.tsx`) | 실무그룹마다 Card. 행 순서: 법령명(굵게) → 크리틱 적발 `CountBadge`(1 이상일 때만) → `ConfidenceMeter` → 근거 n건 → 공포일(재현이면 "재현 예시") → `SourceLabel`. 비어 있으면 `EmptyState`(empty-queue, "승인을 기다리는 검토가 없어요") |
| 검토 상세 (`ReviewClient.tsx`) | 머리: 법령명, `StatusLozenge`, `SourceLabel`. `SummaryList`(실무그룹, 공포일, 상태, 초안 버전과 작성 주체). `RedlineDiff`. 초안 문장 아래 크리틱은 `Callout danger`(정적). 인용은 `CitationChip`. 승인과 수정 후 승인은 `ConfirmDialog`를 거친다. 반려는 지금처럼 사유 입력 뒤 "반려 확정"(두 단계). 충돌과 오류 안내는 `Callout live` |
| 재현 안내 (검토 상세, 실행 타임라인, 알림함) | `isReplay`이면 머리 아래에 `Callout tone="info"`로 `REPLAY_NOTE`("재현 예시예요. 실제 공포 법령이 아니에요.")를 보여 준다. 정직성 표시라 빼지 않는다 |
| 실행 목록 (`runs/page.tsx`) | 실행 상태와 그룹 상태를 `StatusLozenge`로, 그룹 코드를 한국어로 |
| 실행 타임라인 (`RunClient.tsx`, `Timeline.tsx`) | 단계 상태 `StatusLozenge`, 출처 `SourceLabel`, 시도 횟수와 시간 `tabular-nums`. 승인 기록의 `decision`과 `actorRole`을 한국어로, 그룹 코드 배지는 `groupLabel`로. (타임라인 알림 항목에는 `channel` 필드가 없어 해당 없음) 재실행 안내는 `Callout live` |
| 알림함 (`inbox/page.tsx`) | 알림 상태 `StatusLozenge`, 본문 `text-reading`. 비어 있으면 `EmptyState`(empty-inbox) |
| 서버 깨우기 (`WakeGate.tsx`, `WakeWait.tsx`) | 대기 중에는 경과 시간, 시간 막대, 실제 단계, 누를 수 있는 `Mascot waking`, 읽을거리 카드([wake-wait](../wake-wait/design.md)). 실패와 포기는 `Mascot error` + 다시 시도 버튼 |
| 디자인 시스템 (`design-system/page.tsx`) | 정적으로 미리 그리는 서버 컴포넌트. `fs.readFileSync(path.join(process.cwd(), "src/styles/tokens.css"))`를 `design/tokens.ts`로 읽어 색 견본과 대비 값을 보여 줌. 글자 척도, 부품별 상태, 마스코트 사용 규칙과 금지 위치 |

`ConfirmDialog` 문구:
- 승인: 제목 "승인할까요?", 본문 "{그룹명} 그룹 알림함으로 안내문이 발송돼요. 발송 뒤에는 되돌릴 수 없어요.", 버튼 "취소" / "승인하고 알림 보내기".
- 수정 후 승인: 제목 "수정한 내용으로 승인할까요?", 본문 "수정 전과 후가 함께 기록되고, {그룹명} 그룹 알림함으로 안내문이 발송돼요.", 버튼 "취소" / "수정 내용으로 승인".
- 대화상자를 열 때 포커스는 "취소"에. 확정하면 버튼이 `loading`이 되고, 끝나면 대화상자를 닫고 화면을 다시 불러온다.

## 5. 계산 규칙

### 5.1 신구 대조 (`lib/redline.ts`)

```ts
type Part = { kind: "same" | "del" | "ins"; text: string };
type Redline =
  | { status: "diff"; source: "official" | "computed"; old: Part[]; new: Part[]; changes: number }
  | { status: "same" }
  | { status: "added" }      // 구조문이 없음(신설)
  | { status: "removed" }    // 신조문이 없음(삭제)
  | { status: "empty" }      // 둘 다 비었음
  | { status: "tooLong" };   // 계산 상한 초과
function redline(item: OldNew): Redline;
```

1. `beforeMissing` 또는 `before`가 공백뿐이거나 `"(없음)"`이면 구조문 없음. 신조문도 같은 규칙.
2. 둘 다 없으면 `empty`, 구조문만 없으면 `added`, 신조문만 없으면 `removed`.
3. `oldMarks`나 `newMarks`가 배열이고 둘 중 하나라도 구간이 있으면 `source: "official"`. 둘 다 빈 배열인데 `before !== after`면 4번으로 간다. 구조문은 `oldMarks` 구간을 `del`, 신조문은 `newMarks` 구간을 `ins`로 나눈다. 구간이 글 길이를 넘으면 잘라 낸다.
4. 아니면 `source: "computed"`. `diffWordsWithSpace(before, after, { maxEditLength: 400 })`. 결과가 `undefined`면 `tooLong`.
5. 바뀐 곳이 없으면 `same`.
6. `changes`는 구조문의 `del` 덩어리 수와 신조문의 `ins` 덩어리 수 중 큰 값.

화면 표시: `added`는 신조문 칸에 "신설" 배지와 전체 글, `removed`는 구조문 칸에 "삭제" 배지, `empty`는 "비교할 내용이 없어요", `tooLong`은 하이라이트 없이 두 글과 "변경이 많아 강조하지 않았어요", `same`은 "변경 없음". `del`은 `<del>`, `ins`는 `<ins>`로 감싸고 앞에 화면 읽기 프로그램용 숨김 글 "삭제:", "추가:"를 넣는다.

### 5.2 라벨 (`lib/labels.ts`)

```ts
type Tone = "success" | "danger" | "warning" | "info" | "neutral";
type Domain = "run" | "review" | "step" | "notification" | "decision" | "role" | "channel";
function statusLabel(domain: Domain, value: string): { label: string; tone: Tone; known: boolean };
function groupLabel(code: string): string;   // 모르면 원문
function stepLabel(step: string): string;    // api.ts에서 옮김
```

모르는 값은 `{ label: value, tone: "neutral", known: false }`. 개발 모드에서는 `console.warn`도 남긴다.

| 영역 | 원래 값 → 라벨 (의미) |
|---|---|
| run | Queued 대기(neutral), Running 진행 중(info), InReview 검토 중(info), Completed 완료(success), Failed 실패(danger), ClosedIrrelevant 관련 없음(neutral) |
| review | Analyzing 분석 중(info), NeedsReview 검토 필요(warning), WaitingApproval 승인 대기(info), Approved 승인(success), Rejected 반려(danger), Notified 알림 발송(success), ClosedIrrelevant 관련 없음(neutral), AwaitingPartner 파트너 승인 대기(warning) |
| step | Pending 대기(neutral), Running 진행 중(info), Succeeded 성공(success), RetryScheduled 재시도 예정(warning), Failed 실패(danger), NeedsReview 검토 필요(warning) |
| notification | pending 발송 대기(neutral), sent 발송 완료(success) |
| decision | approve 승인(success), reject 반려(danger), confirm 파트너 최종 승인(success), return 파트너가 돌려보냄(warning) |
| role | lawyer 변호사(neutral), partner 파트너(neutral), admin 관리자(neutral) |
| usage | answered 답변(success), refused 거절(warning), unavailable 모델 없음(neutral), failed 실패(danger) |
| channel | demo_inbox 그룹 알림함(neutral), admin 관리자 알림(neutral) |
| 실무그룹 | labor 노동, privacy 개인정보, finance 금융 |

값 출처: `backend/src/LexWatch.Api/Data/Entities/Enums.cs`, 알림 상태(`CollectService.cs`, `NotifyStep.cs`), 결정(`ReviewEndpoints.cs`), 실무그룹(`LexWatchDb.cs`).

## 6. 단계와 검증

각 단계는 아래 명령이 모두 통과한 상태로 끝내고 커밋한다.
- 웹: `cd web && pnpm test && pnpm exec tsc --noEmit && pnpm lint && pnpm build`
- 백엔드: `make test-backend`

| 단계 | 할 일 | 끝났다고 보는 기준 |
|---|---|---|
| 1. 기반 | 의존성, `tokens.css`, `globals.css`, 글꼴, `labels.ts`, 대비·팔레트·라벨 테스트 | 테스트 통과. 다크 모드 에뮬레이션에서도 밝은 화면. 원색 클래스를 쓴 화면은 색이 빠져 보임(다음 단계에서 고침) |
| 2. 백엔드 | 3절 전부 | `make test-backend` 통과 |
| 3. 부품 | `api.ts` 타입, `redline.ts`와 테스트, `ui/*`, `ds/*` | 테스트 통과 |
| 4. 화면 | 4.3 전부, 옛 부품 지움 | `src/**`에 원색 클래스 0건. 수동 확인 절차 통과 |
| 5. 에셋과 문서 화면 | `icon.png`, `/design-system` | 화면 확인 |

마감이 밀리면 이 순서로 뺀다: `/design-system` 화면 → 모바일 탭(두 칸 유지) → `ConfidenceMeter` 막대(숫자만) → 상황별 마스코트(아바타만). 1~4단계의 토큰, 라벨, 출처 분리, 포커스 링, 확인 대화상자는 빼지 않는다.

### 6.1 자동 테스트

| 파일 | 확인하는 것 | 요구 |
|---|---|---|
| `design/contrast.test.ts` | 2.2절 쌍 전부. 출처 색상각 규칙 | DS-03, DS-04 |
| `design/palette.test.ts` | `src/**/*.{ts,tsx}`(테스트 제외)에 `\b(bg\|text\|border\|ring\|outline\|fill\|stroke\|from\|to\|via)-(slate\|gray\|zinc\|neutral\|stone\|red\|orange\|amber\|yellow\|lime\|green\|emerald\|teal\|cyan\|sky\|blue\|indigo\|violet\|purple\|fuchsia\|pink\|rose)-\d`와 `\b(bg\|text\|border\|ring\|outline)-(white\|black)\b` 0건(기본 팔레트를 비우면 흰색과 검정도 사라진다. `bg-white`→`bg-surface`, `text-white`→`text-on-brand`). `tokens.css`에 `--color-*: initial` 있음 | DS-06 |
| `lib/labels.test.ts` | `Enums.cs`를 읽어 세 열거형의 모든 멤버에 `known: true` 라벨. 모르는 값은 원문과 neutral | DS-05 |
| `lib/redline.test.ts` | 공식 표시로 나누기, 대체 계산(조사 변경 "근로자는"→"근로자가", 숫자 "40"→"52"), 신설, 삭제, 둘 다 없음, `"(없음)"`, `maxEditLength` 초과, 변경 없음, 구간이 글을 넘을 때 | DS-08 |
| 백엔드 | 3.3절 | DS-08, DS-09 |
| 에셋 | `ls -l web/public/mascot`으로 각 파일 120KB 이하 확인(2026-11-19 처리 결과 6~30KB) | DS-15 |

웹 테스트 환경은 Node라 부품 렌더링은 자동으로 검사하지 않는다.

### 6.2 수동 확인 절차

로컬 `docker compose up`과 `pnpm dev`로 띄워 브라우저에서 확인한다.

1. 키보드만으로: Tab으로 건너뛰기 링크 → 내비게이션 → 대기함 첫 행 → Enter로 검토 상세 → 인용 칩에서 Enter로 팝오버, Esc로 닫히고 포커스가 칩으로 돌아옴 → 승인 → 대화상자 포커스가 "취소" → Tab으로 확정 → 결과 안내가 읽힘. 모든 단계에서 포커스 링이 보임 (DS-07, 09, 10)
2. 대기함에서 크리틱 적발이 있는 행이 빨간 배지로 먼저 눈에 띄고 신뢰도가 숫자와 막대로 보임 (DS-11)
3. 다크 모드 에뮬레이션에서 밝은 화면 그대로, 카드 글자가 읽힘 (DS-01)
4. 맥과 윈도우 화면 캡처에서 같은 글꼴 (DS-02)
5. 화면 폭 375px에서 신구조문이 탭으로 바뀜 (DS-08)
6. 마스코트가 4.2절 금지 위치에 없음 (DS-15)

## 7. 위험

### 2026-11-19 화면 진단 후 결정

- 배포 HTML은 새 클래스인데 CSS에는 이전 body와 다크 규칙이 남아 있다. Vercel 로그는 이전 빌드 캐시 복원을 보여 준다. 캐시가 없는 배포로 비교하고, 검증 전에는 캐시의 세부 무효화 결함을 확정하지 않는다.
- `pnpm build`에 최종 CSS 검사를 연결한다. `web/scripts/verify-design-css.mjs`는 로컬 `.next` 또는 명시한 공개 URL의 HTML에 연결된 CSS를 읽고 동일한 검사를 수행한다. HTTP 실패나 스타일 누락은 성공으로 처리하지 않는다. Vercel의 immutable 경로와 CSS 분할을 지원한다.
- CSS 검사 계약: `canvas`, `surface`, `ink`, `brand`, `danger`, `warning`, `focus`의 `--color-*` 선언은 `tokens.css`의 값과 같아야 한다(축약 hex 허용). `.bg-surface`/`.bg-brand`는 대응 `background-color:var(--color-*)`, `.text-ink`/`.text-danger`/`.text-warning`는 대응 `color:var(--color-*)`, `.text-title-lg`는 `font-size:var(--text-title-lg)`, `:focus-visible` 규칙은 `outline-color:var(--color-focus)`가 있어야 한다. body는 `background-color:var(--color-canvas)`, `font-family:var(--font-sans)`, `color:var(--color-ink)`, html은 `color-scheme:light`를 포함한다. `--font-sans`에 Pretendard Variable, `--text-title-lg:1.5rem`도 필요하다. 이전 `--background`/`--foreground` 선언, `prefers-color-scheme:dark`와 Arial body는 정상 스타일과 혼재해도 실패다. 정상/분할 fixture는 통과, 누락/값 불일치/이전 규칙 혼재/HTTP 오류는 실패한다. 이는 배포 smoke 검사이며 전체 CSS 캐스케이드/접근성 인증이 아니다.
- 제품 값 변경이나 새 의존성 없이 기존 의미 토큰을 사용한다. `tokens.css`에 작업 폭(70rem), 읽기 폭(48rem), 팝오버/모달 레이어 규칙을 정의하고 문서 화면에 설명한다. 기존 4px 간격 척도를 사용한다.
- 목록은 데스크톱에서 법령명/확인 필요/그룹 관련성/근거/출처 열을 고정해 행마다 정렬한다. 문제 0건도 중립 표시해 빈 열이 다른 정보를 밀지 않게 한다. 작은 화면은 법령명과 보조 정보를 줄바꿈한다. 재현 여부 안내는 유지한다.
- 상세는 상태와 지적 요약을 먼저 놓고 초안은 가운데 읽기 폭으로, 조문 비교는 그 아래 전체 작업 폭으로 놓는다. 상세 상단 ‘확인할 문장 n개’는 첫 지적 문장 앵커로 연결한다. 기본 정보는 접힌 details로 제공한다. 결정 영역은 기존 sticky 위치와 권한/확인 동작을 유지한다.
- 모바일 헤더는 로고와 짧은 역할 이름을 첫 줄에, 내비게이션을 둘째 줄에 놓는다. 역할의 접근 가능한 이름은 ‘변호사로 보기’/‘관리자로 보기’를 유지한다. 문장 바로가기에서 문장 번호가 헤더 뒤로 가려지지 않아야 한다.
- `reviewPresentation.ts`는 지적 문장 수와 첫 지적 위치를 계산한다. 초안 없음, 지적 없음, 여러 지적이 한 문장에 있는 경우를 구분한다. 검사 메시지는 변경하지 않고 문장 아래 먼저 표시하며 `details` 안에 단계와 규칙 ID를 둔다.
- 대안: 색과 부품을 전면 교체하는 재디자인은 이미 있는 접근성과 업무 패턴을 다시 검증해야 하므로 제외한다. 좌우 초안/조문 배치는 조문을 다시 두 칸으로 쪼개는 문제 때문에 순차 배치를 택한다. 선택한 근거 팝오버는 그대로 쓴다.


- Base UI 1.9.0의 `alert-dialog`, `popover` 동작을 처음 쓴다. 단계 3에서 부품 하나씩 수동 확인 1번 절차로 확인하고 문제가 있으면 그 부품만 직접 구현으로 바꾼다.
- `next.config.ts`의 `"*.css"` Turbopack 규칙이 `pretendard` CSS에도 Tailwind 로더를 적용한다. 단계 1의 `pnpm build`와 브라우저 네트워크 탭에서 글꼴 조각이 받아지는지 확인한다. 실패하면 글꼴 CSS를 `public/fonts`로 복사해 `<link>`로 불러온다.
- Pretendard의 `tnum` 지원은 확인하지 못했다. 단계 1에서 신뢰도 열이 정렬되는지 보고, 안 되면 숫자 열만 `ui-monospace`로 바꾼다.
- 대체 글꼴의 `size-adjust: 96%`는 추정값이다. 단계 1에서 글꼴 적용 전후 화면 캡처로 줄바꿈 차이를 보고 조정한다.
- 공식 변경 표시는 이번 배포 이후 수집한 실행에만 있다. 기존 실행과 재현 데이터는 "화면에서 비교"로 표시되어 사용자가 차이를 알 수 있다.
- 마스코트가 업무 도구의 신뢰감을 해칠 수 있다. 등장 위치를 4.2절로 제한하고 크기를 고정한다.
- 마스코트 원본 PNG(장당 약 1.1MB)는 저장소에 두지 않는다. 처리한 WebP가 최종 에셋이고, 교체할 때는 assets.md의 프롬프트로 모든 포즈를 같은 기본형에서 다시 만든다.
