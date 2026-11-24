# Goldfish 재검토 보고서 (9단계 구현 준비도)

**대상:** `docs/features/design-system-v2/design.md` (v2 개정본), `docs/features/design-system-v2/spec.md` (v2)
**이전 검토:** `reviews/consolidated_review_report.md` (조치 항목 A1~A22)
**범위:** A1~A22의 해소 여부, 그리고 4절 단계 1을 적힌 그대로 구현했을 때 통과 명령이 성공하는지. 남은 Blocker와 Major만 적는다. A12(아래 고정 결정 상자 유지)와 A13(권고대로 범위 축소)은 소유자 결정이므로 다시 열지 않는다.

## 판정

**구현 준비 안 됨.** Blocker 1건과 Major 10건이 남아 있다. Blocker는 단계 1의 통과 명령(`pnpm test`)을 확실히 실패시킨다. Major 중 M1과 M2도 단계 1 빌드나 테스트를 깨뜨릴 수 있다. 수정은 모두 문서 몇 줄 단위라서, 고치고 나면 짧게 재확인하는 것으로 충분하다.

참고: 검토하는 동안 작업 트리에 이미 v2 구현이 들어오고 있었다(`tokens.css`, `globals.css`, `ConfirmDialog.tsx`, `Floating.tsx`, `palette.test.ts`, `contrast.test.ts`, `tokens.ts`, `theme.ts`, `cx.ts`, `labels.ts`). 두 검토자는 커밋된 기준 코드를 놓고 설계 문서를 판단했다. 진행 중인 코드가 설계와 다르게 간 곳은 설계가 틀렸다는 증거로만 인용했다(예: `bg-scrim`, `--lw-header-h: 78px`, `watchSystemTheme()`).

## 검토자 상태

| 검토자 | 상태 |
| --- | --- |
| `egm-implementation-readiness` | completed |
| `egm-codebase-details` | completed |

## A1~A22 해소 현황

| 항목 | 상태 | 남은 문제 |
| --- | --- | --- |
| A1 | 해소 (빌드 검사 한 곳이 모자람) | M1 |
| A2 | 해소. `replay` 서술이 사실과 다름 | M4 |
| A3 | 일부 해소 | M5 |
| A4 | 일부 해소. 검토자 한 명은 해소로 봤지만, 실제 헤더 높이를 코드로 확인해 보니 미해소다 | M6 |
| A5 | 해소 | |
| A6 | 해소. 배경막만 남음 | B1 |
| A7 | 일부 해소 | M3 |
| A8 | 내용은 해소. 판정 알고리즘이 없음 | M2 |
| A9 | 배경막에서 미해소 | B1 |
| A10, A11 | 해소 | |
| A12 | 소유자 결정 반영됨 | |
| A13 | 설계에는 반영됨. spec 문구가 남아 있음 | M10 |
| A14 | 미해소 (포커스로 열기, `onSelect`일 때 클릭 동작) | M7 |
| A15 | 대체로 해소. 팔레트 `open` 누락 | M8 |
| A16 | 미해소 (상태 확인 응답 전 공백) | M9 |
| A17 | 대체로 해소. `lib/aiTrail.test.ts`가 파일 목록에 없음 | M4 |
| A18 | 해소. `theme.test` 행이 2.4와 어긋남 | M3 |
| A19~A22 | 해소 | |

## 단계 1과 통과 명령

단계 1이 가리키는 경로, 스크립트, API는 모두 실제로 있다.

- `package.json`의 `test`는 `vitest run && node --test scripts/verify-design-css.test.mjs`이고, `build`는 `next build && node scripts/verify-design-css.mjs`이다.
- `@fontsource-variable/noto-serif-kr/wght.css`가 있다.
- `parseColorTokens`와 `SOURCE_TOKENS`를 쓰는 곳은 `design-system/page.tsx`와 `contrast.test.ts`뿐이다.
- 2.7의 삭제·변경 목록은 현재 `verify-design-css.mjs`의 줄과 맞는다.

그런데도 적힌 그대로면 통과 명령이 실패한다. B1 때문에 `pnpm test`가 확실히 실패하고, M1과 M2 때문에 빌드나 테스트가 실패할 가능성이 높다.

단계 1 행에 아래 두 줄도 넣어 두면 구현자가 추측하지 않아도 된다.

- `design-system/page.tsx`에서 `parseColorTokens`를 `parseThemes(css).light`로 바꾸고, `GROUPS`의 `/^source-/`를 `/^prov-/`로 바꾼다.
- `provenanceOf`는 단계 2에 들어오므로, 단계 1의 `badges.tsx` `SOURCES`는 임시로 다음처럼 연결한다: nvidia, cloudflare, groq → `prov-ai`, fallback → `prov-rule`, replay → `prov-replay`.

## 우선순위별 발견 사항

### B1. 대화상자 배경막 `bg-black/50`이 단계 1을 깨뜨리고 화면에도 그려지지 않음 - 심각도: Blocker
* **검토 관점:** `egm-implementation-readiness`, `egm-codebase-details` (둘 다 지적)
* **영향 범위:** design.md 75행 (2.2 표의 "대화상자 배경막 `ui/ConfirmDialog.tsx:39`" 행). 단계 1의 "2.2 표의 뒤집힘 조합"에 들어 있다.
* **문제:**
  - 유지하기로 한 원색 클래스 검사 `web/src/design/palette.test.ts:11`의 `BW = /\b(bg|text|border|ring|outline|divide)-(white|black)\b/`가 `bg-black/50`에 걸린다. 그래서 `pnpm test`가 실패한다.
  - 2.6의 새 "정의 안 된 색 이름" 검사에도 걸린다.
  - `--color-*: initial`이 `black`을 지우므로 Tailwind가 CSS를 만들지 않는다. 배경막이 아무 표시 없이 투명해진다.
* **권고:**
  - 2.1 "추가" 행에 `scrim`을 넣는다.
  - 2.2에 `--lw-scrim`을 정의한다. 밝은 테마는 `rgb(17 18 20 / 0.4)`, 어두운 테마는 `rgb(0 0 0 / 0.6)`이다.
  - `@theme inline`에 `--color-scrim: var(--lw-scrim)`를 추가한다.
  - 배경막 행을 `bg-scrim`으로 바꾼다.
  - 16진수가 아닌 값이라 대비 및 키 집합 파서가 건너뛴다는 점을 2.5에 한 줄 적는다. 키 집합 동일 검사가 `--lw-*` 전체를 볼 경우에 대비해서다.
  - 진행 중인 `tokens.css`가 이미 이렇게 하고 있다.

### M1. `--font-serif` 빌드 검사가 단계 1에서 하지 않는 일에 기대고 있음 - 심각도: Major
* **검토 관점:** 두 검토자 모두
* **영향 범위:** design.md 130행 (2.7 "추가"의 `--font-serif`에 `NotoSerifKRVariable`), 146행, 4절 단계 1 ("글꼴").
* **문제:**
  - Tailwind 4는 `@theme static`이 아니면 실제로 쓰는 테마 변수만 내보낸다. 검토자들은 이를 설치된 문서나 빌드로 확인하지는 않았다.
  - `web/src`에는 지금 `font-serif`를 쓰는 곳이 없다.
  - 세리프를 붙이는 `components/ds/layout.tsx`의 `PageHeader` 변경에는 배정된 단계가 없다. 단계 2에는 `EmptyState`만 적혀 있다.
  - 그래서 단계 1의 `pnpm build`가 이 검사에서 실패할 수 있다.
* **권고:** 단계 1 행에 "`components/ds/layout.tsx` `PageHeader`에 `font-serif`"를 넣는다. 아니면 `--font-serif` 검사를 세리프를 실제로 붙이는 단계로 옮긴다.

### M2. 2.6 "정의 안 된 색 이름" 검사에 판정 규칙이 없음 - 심각도: Major
* **검토 관점:** `egm-implementation-readiness`
* **영향 범위:** design.md 122행, 358행 (4.1 `palette.test.ts`).
* **문제:** 지금 소스에는 색이 아닌 `bg-`, `text-`, `border-` 클래스가 많다. `text-ui`, `text-title-lg`, `text-left`, `border-b`, `border-l-4`, `border-dashed` 같은 것들이고, `border-l-info`처럼 방향 접두가 붙은 색도 있다. 단순한 정규식은 이것들을 오탐해서 단계 1에서 실패한다.
* **권고:** 규칙을 문서에 적는다.
  1. 색 이름은 `@theme inline`의 `--color-([a-z0-9-]+):` 선언에서 읽는다.
  2. 테스트 파일이 아닌 `.ts`와 `.tsx`에서 `\b(bg|text|border(?:-[trblxyse])?)-([a-z][a-z0-9-]*)(?:\/\d+)?\b`를 찾는다.
  3. 접미가 색 이름이거나 아래 허용 목록에 있으면 통과하고, 나머지는 실패다.
     - text: 글자 크기 토큰, `left`, `right`, `center`, `justify`, `start`, `end`, `wrap`, `nowrap`, `balance`, `pretty`, `ellipsis`, `clip`
     - border: `0`, `2`, `4`, `8`, `solid`, `dashed`, `dotted`, `double`, `none`, `hidden`, `collapse`, `separate`
     - bg: `none`, `fixed`, `local`, `scroll`, `cover`, `contain`, `auto`, 위치값, `repeat`, `no-repeat`, `clip-*`, `origin-*`, `linear-*`
  4. 예시 문자열로 규칙 자체를 확인하는 테스트를 하나 둔다.

### M3. 테마 스크립트의 저장소 예외 처리가 서로 모순되고, Strict Mode 조건과 시스템 추종 리스너가 빠짐 - 심각도: Major
* **검토 관점:** 두 검토자 모두
* **영향 범위:** design.md 105~110행 (2.4), 356행 (4.1 `theme.test.ts`), 4절 단계 1과 2.
* **문제:**
  - **예외 처리 모순.** 105행과 spec 5절은 저장소 예외를 `system`으로 본다. 그런데 106행은 전체 try/catch로 감싸 `light`가 남고, 356행 테스트는 "속성을 바꾸지 않음"이다.
  - **Strict Mode.** Next 문서 `preventing-flash-before-hydration.md` 486~512행("Re-applying attributes in development")에 따르면, 개발 모드의 재마운트가 `<html>` 속성을 JSX 값(`data-theme="light"`)으로 되돌린다. 이를 `useLayoutEffect`로 다시 적용해야 한다는 조건을 A7이 요구했는데 아직 적혀 있지 않다.
  - **시스템 추종 리스너.** 기본값 `system`으로 처음 열면 `matchMedia` 리스너를 거는 곳이 없다. `applyTheme`은 사용자가 고를 때만 돈다.
  - **마운트 단계.** 단계 1에 `ThemeMenu`가 있지만, 이를 실제로 마운트할 헤더 변경은 단계 2에 있다.
* **권고:**
  - `localStorage.getItem`만 안쪽 try로 감싸고, 실패하면 `system`으로 처리해 `matchMedia`로 판정한다. 바깥 try/catch는 마지막 방어로 남긴다.
  - 테스트 행을 "`localStorage` 예외 → 시스템 따름"으로 고친다.
  - 2.4에 다음을 추가한다: "`ThemeMenu`(또는 `layout.tsx`에 마운트한 `ThemeSync` 클라이언트 컴포넌트)가 마운트 때 `useLayoutEffect`로 판정한 테마를 다시 적용하고, 선택이 `system`이면 `matchMedia` change 리스너를 건다."
  - 이 컴포넌트를 단계 1에 마운트할지, 헤더와 함께 단계 2에 넣을지 명시한다.

### M4. `isAiProvider("replay")` 서술이 사실과 반대 - 심각도: Major
* **검토 관점:** `egm-implementation-readiness`. 직접 확인했다.
* **영향 범위:** design.md 215행. 3.1 파일 목록.
* **문제:** 215행은 `isAiProvider("replay")`가 "지금처럼 거짓"이라고 쓴다. 하지만 `web/src/lib/aiTrail.ts:152-154`는 `!!kind && kind !== "none" && kind !== "fallback"`이라 `replay`에 참을 돌려준다. `web/src/lib/aiTrail.test.ts:86`도 `toBe(true)`를 단언한다. 구현자는 함수를 고쳐야 하는지 그대로 둬야 하는지 알 수 없다.
* **권고:** 둘 중 하나를 고른다.
  - (a) "`replay`에 거짓을 돌려주도록 바꾼다. `aiTrail.test.ts:86`을 고치고, `lib/aiTrail.test.ts`와 호출부를 3.1에 추가한다."
  - (b) "참으로 두고, `provenanceOf`가 `isAiProvider`보다 먼저 `replay`를 판정한다."

  (a)가 "재현은 AI 라벨을 달지 않는다"는 의도에 맞는다.

### M5. 근거 패널이 없는 필드 이름과 API에 없는 값을 씀 - 심각도: Major
* **검토 관점:** 두 검토자 모두
* **영향 범위:** design.md 219~221행 (3.3), 361행 (4.1 `evidenceLink.test.ts`).
* **문제:**
  - **필드 이름.** `EvidenceItem`의 필드는 `url`이 아니라 `sourceUrl`이고(`web/src/lib/api.ts:57`), `excerpt`는 선택 필드다(`api.ts:59`).
  - **없는 값.** "법령 ID가 검토 대상 법령 ID와 다르면 없음" 규칙에 쓸 법령 ID가 `ReviewDetail`/`ReviewSummary`(`api.ts:4-17, 69-76`)에 없다.
  - **백엔드 동작.** 근거 ID는 `law:{LawId}:{no}`와 `oldnew:{LawId}:{no}` 꼴이다(`backend/src/LexWatch.Api/Workflow/Steps/FetchArticlesStep.cs:63,67`). 백엔드 `ToOldNew`는 칸이 3개 미만이면 "없음"이 아니라 `Title`로 대체한다(`ReviewEndpoints.cs:422-423`).
* **권고:**
  - 219행을 `sourceUrl`로 고치고, `excerpt`가 없을 때 무엇을 보일지 적는다.
  - 시그니처를 `oldNewFor(evidenceId, evidence: EvidenceItem[], oldNew: OldNew[])`로 바꾼다. 동작은 다음과 같다.
    - `[prefix, lawId, art]`로 나눈다.
    - 접두가 `law`나 `oldnew`가 아니거나 칸이 모자라면 없음을 돌려준다.
    - `evidence`에 `oldnew:${lawId}:${art}`가 있을 때만 `articleNo === art`인 항목을 돌려준다.
  - 한 검토의 근거는 모두 한 법령에서 오므로, 법령 ID 비교를 아예 빼는 것도 괜찮다.
  - 짧은 ID에 백엔드의 `Title` 대체를 따를지 적는다.

### M6. 분할 화면 높이가 실제와 다른 헤더 높이와 위쪽 요소를 빠뜨린 계산식을 씀 - 심각도: Major
* **검토 관점:** `egm-codebase-details`. 직접 확인했다.
* **영향 범위:** design.md 97행 (`--lw-header-h: 64px`), 233행, 239행 (3.4와 ASCII 도해).
* **문제:**
  - **헤더 높이.** 실제 헤더는 28px 데모 띠(`AppHeader.tsx:46` `h-7`)에 메뉴 줄이 더해진 높이다. 레이아웃이 비워 두는 자리는 `h-[77px]`(`app/layout.tsx:17`)이고, 진행 중인 `tokens.css`는 78px를 쓴다. `sm` 미만에서는 메뉴가 줄바꿈되어(`AppHeader.tsx:55` `order-3 w-full`) 높이가 바뀐다.
  - **계산식.** `calc(100dvh - header - 6rem)`은 `ReviewClient` 안에서 분할 위에 있는 제목 블록, 주의 Callout, `AiTrail`, 그리고 `<main>`의 `py-8`을 빼지 않는다. 그래서 페이지와 칸이 함께 스크롤된다(이중 스크롤).
* **권고:**
  - 값을 78px로 고치고 분할이 있는 `xl`에서만 고정한다고 쓴다. 아니면 데모 띠를 없앤다고 명시한다.
  - 분할 컨테이너를 `xl:sticky xl:top-[var(--lw-header-h)] xl:h-[calc(100dvh-var(--lw-header-h)-6rem)]`로 둔다. 그러면 페이지를 스크롤했을 때 분할이 위에 붙은 뒤 두 칸이 각자 스크롤된다.
  - ASCII 도해의 "헤더 (64px)" 표기를 고친다.

### M7. 인용 미리보기의 포커스 열기와 `onSelect`일 때 클릭 동작이 정해지지 않음 - 심각도: Major
* **검토 관점:** 두 검토자 모두
* **영향 범위:** design.md 24행 (1.1), 173~174행 (3.1). spec DS2-08.
* **문제:**
  - `Popover.Trigger`가 지원하는 것은 `openOnHover`, `delay`, `closeDelay`뿐이다(`node_modules/@base-ui/react/popover/trigger/PopoverTrigger.d.ts:45-64`). 포커스로 열리지 않으므로 DS2-08의 "포커스하면"을 만족하지 못한다.
  - `onSelect`를 넘겼을 때 클릭이 고정 팝오버를 여는지, 패널만 갱신하는지가 없다.
  - 1280px 미만에서 패널이 화면 밖에 있을 때의 처리도 없다.
* **권고:**
  - `Popover.Root`를 제어형(`open`/`onOpenChange`)으로 두고, 트리거의 `onFocus`로 임시 카드를 열고 `onBlur`로 닫는다(고정된 경우는 제외).
  - "`onSelect`가 있으면 클릭과 Enter는 `onSelect(id)`를 부르고 고정하지 않는다. 마우스 올림과 포커스는 임시 카드를 보인다"를 적는다.
  - "`ReviewClient`는 늘 `onSelect`를 넘기고, 1280px 미만에서는 `EvidencePanel`을 화면 안으로 스크롤한다"를 적는다(또는 반대 결정을 적는다).
  - 이렇게 하지 않으려면 DS2-08을 "포커스 후 Enter로 고정해서 열림"으로 좁힌다.

### M8. 명령 팔레트 Autocomplete에 `open`이 빠짐 - 심각도: Major
* **검토 관점:** `egm-codebase-details`
* **영향 범위:** design.md 294행 (3.7).
* **문제:** `AutocompleteRoot.d.ts:77-83`에 따르면 `inline`은 `open`과 함께 써야 한다(`<Autocomplete.Root inline open>`). `open`이 없으면 목록이 보이지 않는 상태로 취급된다.
* **권고:** Dialog 안에서 `<Autocomplete.Root inline open autoHighlight>`로 적는다.

### M9. 스켈레톤과 WakeGate 규칙이 상태 확인 응답 전에 정해지지 않아, 깨어 있지만 느린 서버에서 렉스가 깜빡임 - 심각도: Major
* **검토 관점:** 두 검토자 모두
* **영향 범위:** design.md 321행 (3.9, 단계 5).
* **문제:**
  - `pingHealth`는 400ms 뒤 `wait`가 켜진 다음에야 시작된다(`WakeGate.tsx:46-49, 86`). `apiUp`은 핑이 돌아와야 참이 된다(`WakeGate.tsx:94`).
  - 그래서 "apiUp면 스켈레톤, 아니면 렉스" 규칙대로면, 400ms보다 느린 응답은 모두 렉스를 잠깐 보였다가 스켈레톤으로 바뀐다.
  - 데이터가 그 뒤에 도착하면 `DONE_HOLD_MS` 600ms 유지까지 붙는다(`WakeGate.tsx:68`).
* **권고:** 상태를 `unknown | up | waking` 셋으로 둔다.
  - `unknown`과 `up`이면 `fallback`(스켈레톤)을 보인다.
  - 핑이 `waking`을 돌려주거나 실패했을 때만 `WakeWait`(렉스)로 바꾼다.
  - 스켈레톤을 보이던 중이면 `holding`을 건너뛴다.

### M10. A13 축소 뒤 spec과 design이 서로 다름 - 심각도: Major
* **검토 관점:** 두 검토자 모두. 직접 확인했다.
* **영향 범위:** spec.md 63, 64, 68행. design.md 344행, 349행.
* **문제:** A13을 다시 여는 것이 아니라, 반영하면서 놓친 문구다. 이대로면 수용 기준을 만족할 수 없다.
  - spec 68행은 고정 비율 "60:40", design 349행은 "58:42"이다.
  - spec DS2-16(64행)에 "밀도 비교"가 남아 있다. 밀도 전환은 범위 밖이다(spec 41행).
  - spec DS2-15(63행)에 "표 행"이 남아 있다. design에는 표 행 cva가 없다.
  - spec 4.1은 "DS2-16의 두 테마 견본"을 필수로 두는데, design 4절은 `/design-system` 전체를 선택 단계 5에 넣었다.
* **권고:**
  - 비율을 하나로 정한다.
  - DS2-16에서 "밀도 비교"를, DS2-15에서 "표 행"을 지운다.
  - 두 테마 견본(상자별 `data-theme`와 두 테마 대비 표)을 이미 `design-system/page.tsx`를 고치는 단계 1로 옮기고, 3.10의 나머지는 단계 5에 둔다.

## 요약

| 심각도 | 건수 | 단계 1 통과에 영향 |
| --- | --- | --- |
| Blocker | 1 (B1) | 확실히 실패 |
| Major | 10 (M1~M10) | M1, M2는 실패 가능. M3, M10은 단계 1 범위 정의에 영향. 나머지는 단계 2~5 |
