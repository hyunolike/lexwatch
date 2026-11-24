# 설계: LexWatch 디자인 시스템 v2

- 상태: 검토 반영본 v3 (2026-11-25). [EGM 통합 검토](reviews/consolidated_review_report.md)의 A1~A22와 [재검토](reviews/re_review_report.md)의 B1, M1~M10 반영. 아래 7절에 재검토 조치를 모았다
- 작성: hh.jang
- 명세: [spec.md](spec.md) v2 / 조사: [research.md](research.md) / v1 설계: [../design-system/design.md](../design-system/design.md)
- 경로 기준: 화면 코드는 `web/src/`, 스크립트는 `web/`. 명령은 `web/`에서 실행한다.

용어: 본문에서 "밝은 테마", "어두운 테마"라고 쓰고, 사용자가 고르는 설정 값만 "시스템, 밝게, 어둡게"라고 쓴다. 렉스(마스코트)는 [v1 DS-15](../design-system/spec.md), 역할 전환은 `components/RoleSwitch.tsx`, 서버 깨우기 대기는 [wake-wait](../wake-wait/spec.md), AI 흔적은 [ai-footprints](../ai-footprints/spec.md)를 따른다.

## 1. 결정

### 1.1 결정과 근거

| 결정 | 근거 | 관련 요구 |
|---|---|---|
| 의미 토큰 이름은 v1 것을 유지하고 값만 두 테마로 나눈다 | Tailwind는 색 이름 앞에 속성을 붙여 research의 `text-primary`는 `text-text-primary`가 된다. 이름을 바꾸면 화면 파일 35개를 고쳐야 한다 | DS2-01, DS2-03 |
| 테마는 `[data-theme]` 속성 하나로 바꾼다. 인라인 스크립트가 늘 실제 테마(`light` 또는 `dark`)를 적는다 | CSS가 두 블록으로 끝나고 어두운 값이 한 번만 적힌다. 미디어 쿼리 블록과 속성 블록을 둘 다 두면 같은 값을 두 번 써야 한다 | DS2-01 |
| 클래스는 `@theme inline { --color-x: var(--lw-x) }`로 만든다 | `inline`이면 클래스가 `var(--lw-x)`를 직접 쓰고, 화면 코드가 테마를 모른다 | DS2-03 |
| 테마 스크립트는 Next.js 문서의 루트 레이아웃 `<head>` 인라인 스크립트 방식 하나를 쓴다(next-themes를 쓰지 않음) | 필요한 값이 셋뿐이다. next-themes는 2025-03 이후 배포가 없고 React 19 스크립트 경고 이슈(#385)가 열려 있다 | DS2-01 |
| 출처는 "누가 만들었나" 네 종류(AI, 규칙, 사람, 재현)로 한 부품에서 그린다. `source-*` 다섯 쌍(NVIDIA, Cloudflare, Groq, 대체 구현, 재현)은 지운다 | 공급자가 늘 때마다 상태 색과 겹치지 않는 색상각이 바닥난다. 지금 출처 색이 네 곳(`badges.tsx`, `AiLabel.tsx`, `AiTrail.tsx`, `RunTimeline.tsx`)에 흩어져 AI가 `info`, 사람이 `success`를 쓰는 곳이 있다 | DS2-05, DS2-06 |
| 제목 세리프는 `@fontsource-variable/noto-serif-kr@5.3.1` | 유지보수되는 npm 패키지, 조각 로딩, 한자 포함. 마루 부리는 굵기 하나 800KB, 리디바탕 패키지는 글꼴 파일이 없다 | DS2-04 |
| 변형은 `class-variance-authority@0.7.1`, 클래스 합치기는 `tailwind-merge@3.7.0`을 `extendTailwindMerge`로 설정해 쓴다 | 변형 수가 늘었다. 설정 없이 쓰면 `twMerge("text-ui text-ink")`가 `text-ui`를 지운다(research 3.1에서 재현) | DS2-15 |
| 토스트와 명령 팔레트는 이미 쓰는 `@base-ui/react@1.9.0`의 Toast, Dialog, Autocomplete | sonner는 +9.4KB, Base UI Toast는 +6.5KB. cmdk는 Radix Dialog를 함께 끌고 와 포커스 규칙이 두 벌이 된다 | DS2-10, DS2-11 |
| 인용 미리보기는 지금 `ui/Floating.tsx`의 Popover 하나에 `openOnHover`를 더한다 | PreviewCard를 따로 쓰면 마우스 올림 카드와 클릭 팝오버가 두 레이어가 되어 같은 칩에 열리는 것이 둘이 된다. Popover 하나로 올림은 임시, 클릭은 고정이 된다 | DS2-08 |
| 분할 화면 크기 조절은 직접 만든다 | 패널 둘, 분리선 하나다. 키보드 조작과 `aria-value*`를 직접 보장한다 | DS2-07 |
| 결정 버튼은 지금처럼 화면 아래 고정 상자에 둔다 | 작성자 결정(A12). 다 읽은 뒤 결정하는 흐름에 맞고 사유 입력, 재검사 적발 안내, 파트너 상자의 자리를 바꾸지 않는다 | DS2-07 |
| 렉스는 헤더에서 내리고 평면 SVG 마크(`currentColor`)를 쓴다 | 작성자 결정(줄여서 유지). 헤더는 매 화면에 나와 인상을 가장 크게 좌우한다 | DS2-14 |
| 운영 현황의 공급자 쌓은 막대는 공급자별 가로 막대 목록으로 바꾸고 모두 `prov-ai` 색을 쓴다 | 공급자는 모두 AI라 색으로 나눌 이유가 없다. 이름과 건수, 비율 글자로 구분한다. 새 토큰이 필요 없다 | DS2-05 |
| `cx`는 이름을 유지하고 안쪽을 `tailwind-merge`로 바꾼다 | 이름을 둘로 두면 어느 쪽을 쓸지 헷갈린다. 동작이 바뀌므로 회귀 테스트를 단계 1에 둔다(4절) | DS2-15 |

### 1.2 검토한 대안

| 대안 | 버린 이유 |
|---|---|
| research 이름(`text-primary` 등) 채택 | 1.1 첫 줄 |
| CSS `light-dark()` 함수로 한 줄에 두 값 | Tailwind 4의 Lightning CSS가 이 함수를 어떻게 바꿔 내는지 확인하지 않았다. 대비 테스트가 값을 읽기도 어려워진다 |
| `prefers-color-scheme` 미디어 블록 + 속성 블록 | 어두운 값이 두 번 적힌다. 스크립트가 늘 속성을 적으면 필요 없다 |
| 테마를 쿠키에 저장하고 서버에서 읽기 | 루트 레이아웃에서 쿠키를 읽으면 앱 전체가 정적 생성에서 빠진다(Next.js 문서) |
| Tailwind `dark:` 변형 | 화면 코드가 테마를 알아야 하고, 어두운 값이 토큰 밖으로 흩어진다 |
| shadcn/ui로 갈아타기 | Base UI 부품이 화면 전체에 있다. shadcn의 Base UI 판도 같은 부품에 스타일을 입힌 복사 코드다 |
| next-themes, sonner, cmdk | 1.1 |
| 분할 화면 라이브러리(react-resizable-panels) | 패널 둘에 비해 크고, 폭 저장을 하지 않으므로 얻는 기능이 없다 |
| 본문까지 세리프 | 표와 버튼이 산세리프라 한 화면에 두 서체가 섞인다. `/design-system`에서 비교만 한다 |
| PreviewCard 따로 쓰기 | 1.1 인용 미리보기 줄 |

## 2. 토큰

### 2.1 유지, 추가, 삭제

| 구분 | 토큰 | 관련 요구 |
|---|---|---|
| 유지(값만 두 테마) | `canvas`, `surface`, `surface-sunken`, `ink`, `ink-muted`, `ink-subtle`, `line`, `line-strong`, `brand`, `brand-hover`, `on-brand`, `focus`, `success`, `success-bg`, `danger`, `danger-bg`, `warning`, `warning-bg`, `info`, `info-bg`, `diff-del`, `diff-del-bg`, `diff-ins`, `diff-ins-bg` | DS2-01 |
| 유지(별칭) | `neutral` = `ink-muted`, `neutral-bg` = `surface-sunken`. `@theme inline`에서 `--color-neutral: var(--lw-ink-muted)`로 적고 `--lw-neutral`은 두지 않는다 | DS2-01 |
| 추가 | `surface-raised`, `brand-subtle`, `brand-fg`(글자로 쓰는 브랜드색. `text-brand-fg`), `on-danger`, `scrim`(배경막), `prov-ai`, `prov-ai-bg`, `prov-rule`, `prov-rule-bg`, `prov-human`, `prov-human-bg`, `prov-replay`, `prov-replay-bg`, 그림자 `--lw-shadow-card`, `--lw-shadow-pop`, `--lw-shadow-dialog`, `--lw-header-h` | DS2-01, DS2-05, DS2-07 |
| 삭제 | `source-nvidia`, `source-cloudflare`, `source-groq`, `source-fallback`, `source-replay`와 각 `-bg` | DS2-05 |

research의 `accent-text`는 `brand-text`로 옮기면 클래스가 `text-brand-text`가 되어 `brand-fg`로 이름을 바꿨다.

### 2.2 값

research 4.3 표의 값을 쓴다. 대응: `border`→`line`, `border-strong`→`line-strong`, `text-primary`→`ink`, `text-secondary`→`ink-muted`, `text-tertiary`→`ink-subtle`, `accent`→`brand`, `accent-hover`→`brand-hover`, `on-accent`→`on-brand`, `accent-subtle`→`brand-subtle`, `accent-text`→`brand-fg`. research에 없는 값:

| 토큰 | 밝은 테마 | 어두운 테마 | 이유 |
|---|---|---|---|
| `on-danger` | `#ffffff` | `#1b0a09` | 어두운 테마 `danger`(`#f08a80`) 위 흰 글자는 약 2.4:1로 미달 |
| `--lw-shadow-card` | `0 1px 2px rgb(27 29 34 / 0.06)` | `0 1px 2px rgb(0 0 0 / 0.4)` | 고정 rgb 그림자는 어두운 바탕에서 안 보인다 |
| `--lw-shadow-pop` | `0 8px 24px rgb(27 29 34 / 0.14)` | `0 8px 24px rgb(0 0 0 / 0.5)` | 같음 |
| `--lw-shadow-dialog` | `0 24px 48px rgb(27 29 34 / 0.24)` | `0 24px 48px rgb(0 0 0 / 0.6)` | 같음 |

테마에 따라 뒤집히는 조합(지금 코드가 고정 가정을 한 곳):

| 위치 | 지금 | 바꿈 |
|---|---|---|
| 툴팁 `ui/Floating.tsx:35` | `bg-ink text-on-brand` | `bg-ink text-canvas`. 어두운 테마에서 `ink`가 밝은 회색이 되므로 글자도 `canvas`로 뒤집힌다. 대비 쌍 `canvas/ink` 추가 |
| 대화상자 배경막 `ui/ConfirmDialog.tsx:39` | `bg-ink/40` | `bg-scrim`. `--lw-scrim` 밝은 테마 `rgb(17 18 20 / 0.4)`, 어두운 테마 `rgb(0 0 0 / 0.6)`. 원색 클래스(`bg-black`)는 팔레트를 비워 CSS가 생기지 않는다 |
| `.lw-stripes` `app/globals.css:44-48` | 흰색 rgba 줄무늬 | `color-mix(in srgb, var(--lw-surface) 40%, transparent)` |
| 위험 버튼 | `bg-danger text-on-brand` | `bg-danger text-on-danger hover:bg-danger/90` |
| 글자로 쓰는 `text-brand` (`AppHeader.tsx:53`, `FirstVisitGuide.tsx:16`, `WakeWait.tsx:217`) | `text-brand` | `text-brand-fg` |
| `RunTimeline.tsx:20, 128`의 `var(--color-danger)`, `var(--color-danger-bg)`, `var(--color-line)` | `--color-*` | `var(--lw-danger)` 등 |

`surface-raised`로 옮길 부품: 팝오버, 툴팁 아닌 떠 있는 카드(`Floating.tsx` Popover), 대화상자, 토스트, 명령 팔레트, 아래 고정 결정 상자.

### 2.3 파일 구조 (`web/src/styles/tokens.css`)

```css
@theme inline {
  --color-*: initial;
  --color-canvas: var(--lw-canvas);
  /* ... 2.1의 모든 색 토큰이 같은 꼴 ... */
  --color-neutral: var(--lw-ink-muted);
  --color-neutral-bg: var(--lw-surface-sunken);
  --shadow-card: var(--lw-shadow-card);
  --shadow-pop: var(--lw-shadow-pop);
  --shadow-dialog: var(--lw-shadow-dialog);
}
@theme { /* 글자 척도, 글꼴, 모서리, 레이어, 폭: 테마와 무관 */ }
:root { color-scheme: light; --lw-canvas: #f7f6f3; /* ... 밝은 테마 ... */ --lw-header-h: 64px; }
[data-theme="dark"] { color-scheme: dark; --lw-canvas: #111214; /* ... 어두운 테마 ... */ }
```

어두운 블록은 `:root`로 한정하지 않는다. `/design-system`이 견본 상자 하나에 `data-theme="dark"`를 달아 두 테마를 나란히 그린다.

### 2.4 테마 스크립트 (`lib/theme.ts`, `app/layout.tsx`)

- 저장 키 `lw_theme`, 값 `system | light | dark`. 그 밖의 값이나 저장소 예외는 `system`.
- `THEME_SCRIPT`(문자열 하나): 저장 값을 읽어 `system`이면 `matchMedia('(prefers-color-scheme: dark)')`로 정하고 `document.documentElement.dataset.theme`에 `light` 또는 `dark`를 적는다. `try/catch`로 감싸 실패하면 아무것도 하지 않는다(서버가 그린 `data-theme="light"`가 남는다).
- 삽입: Next.js 16 문서(`node_modules/next/dist/docs/01-app/02-guides/preventing-flash-before-hydration.md`, Themes 절)의 방식 그대로 서버 컴포넌트인 루트 레이아웃이 `<html lang="ko" data-theme="light" suppressHydrationWarning>`의 `<head>`에 `<script dangerouslySetInnerHTML={{ __html: THEME_SCRIPT }} />`를 둔다. 문서의 조건: 스크립트는 첫 문서에서만 돈다. 속성이 `<html>`에 남으므로 클라이언트 이동에는 다시 돌 필요가 없다.
- `applyTheme(choice)`: 저장하고, 같은 판정으로 `data-theme`을 적는다. `system`이면 `matchMedia` change 리스너를 걸어 시스템 설정이 바뀌면 따라간다. 다른 탭과는 동기화하지 않는다.
- 테마 메뉴: 헤더의 `ThemeMenu`(Base UI Menu, 라디오 항목 셋)와 명령 팔레트가 `applyTheme`을 부른다. 현재 값은 `useSyncExternalStore`로 `lw_theme`을 구독한다(RoleSwitch와 같은 패턴).
- 테스트: `theme.test.ts`가 `THEME_SCRIPT`를 가짜 `localStorage`, `matchMedia`, `document` 위에서 `new Function`으로 실행해 세 값, 모르는 값, 저장소 예외를 확인한다.

### 2.5 대비 검사 (`design/tokens.ts`, `design/contrast.test.ts`)

- `parseThemes(css) → { light: Record, dark: Record }`: `:root { }`와 `[data-theme="dark"] { }` 블록에서 `--lw-*: #hex`를 읽는다. 별칭(`neutral`)은 `@theme inline`의 `var(--lw-x)`를 따라 풀어 넣는다.
- `CONTRAST_PAIRS`: research 4.4의 47쌍(이름은 2.2 대응대로) + `on-danger/danger` + 실제 쓰는 조합(`ink`, `ink-muted` / `success-bg`, `danger-bg`, `warning-bg`, `info-bg` — Callout 본문, `canvas/ink` — 툴팁, `neutral/neutral-bg`, `brand-fg/brand-subtle`). v1의 `focus/brand` 쌍은 뺀다. 포커스 링은 늘 2px 띄워 링 옆이 바탕이기 때문이다.
- 테스트는 두 테마 모두 돈다. 두 블록의 키 집합이 같아야 한다.
- 출처 색상각 검사: `PROV_TOKENS`(`prov-ai`, `prov-rule`, `prov-human`, `prov-replay`) × 두 테마. v1 금지 범위(채도 0.3 이상이면 0~70°, 90~170°, 330~360° 금지)를 그대로 쓰고, `prov-replay` 밖은 밝은 테마에서 채도 0.3 이상임을 단언한다.
- 포커스: `globals.css`에 `:focus-visible { outline-offset: 2px }`. `palette.test.ts`가 소스에서 `outline-offset-0`, `ring-inset`을 금지한다.

### 2.6 소스 검사 (`design/palette.test.ts`)

`web/src/**/*.tsx`와 `*.ts`(테스트 제외)에서 다음이 나오면 실패: `dark:`, `-[#`(임의 16진수), `tokens.css`에 정의되지 않은 색 이름의 `bg-`, `text-`, `border-` 클래스(지운 `source-*` 포함). 기존 "원색 클래스 금지" 검사는 유지한다.

### 2.7 빌드 검사 (`web/scripts/verify-design-css.mjs`와 `.test.mjs`)

| 구분 | 항목 |
|---|---|
| 삭제 | 14–21행 COLORS 루프(소스에 `--color-x: #hex`가 없어 TypeError). 32행 `html{color-scheme:light}` 요구. 42행 `prefers-color-scheme:dark` 금지 |
| 변경 | 24–31행 클래스 기대값을 `var(--lw-*)`로(`.bg-surface{background-color:var(--lw-surface)}` 등). 41행 `--text-title-lg:1.625rem`. `body` 규칙 기대값 `background-color:var(--lw-canvas)`, `color:var(--lw-ink)` |
| 추가 | `:root` 블록에 `--lw-canvas:#f7f6f3`와 `color-scheme:light`, `[data-theme=dark]` 블록에 `--lw-canvas:#111214`와 `color-scheme:dark`. `:focus-visible`에 `outline-offset:2px`. `--font-serif`에 `NotoSerifKRVariable` |
| 유지 | `--background`/`--foreground`, `body{font-family:Arial}` 잔재 감지. Pretendard 정의 |

`verify-design-css.test.mjs`의 통과 예시 CSS와 실패 예시 CSS를 새 계약으로 바꾼다.

### 2.8 글자 (`DS2-04`)

| 토큰 | 글꼴 | 크기/줄높이 | 굵기 | 쓰는 곳 |
|---|---|---|---|---|
| `text-display`(추가) | 세리프 | 2rem/2.5rem | 600 | 빈 화면 제목, `/design-system` 표지 |
| `text-title-lg` | 세리프 | 1.625rem/2.25rem | 600 | `PageHeader` |
| `text-title-md` | 세리프 | 1.3125rem/1.875rem | 600 | 검토 상세 법령명 |
| `text-title-sm` | 산세리프 | 1rem/1.5rem | 600 | 구역 제목 |
| `text-reading` | 산세리프 | 1.0625rem/1.875rem | 400 | 초안 문장, 조문(`RedlineDiff`), 알림 본문(`app/inbox`), 채팅 답(`ReviewChat`) |
| `text-ui`, `text-label`, `text-caption`, `text-micro` | 산세리프 | v1과 같음 | v1과 같음 | |

- 세리프는 Tailwind 글자 크기 토큰에 글꼴을 넣을 수 없으므로 `PageHeader`, 검토 상세 법령명, `EmptyState` 제목에 `font-serif`를 붙인다. 묶음 유틸리티는 만들지 않는다.
- `--font-serif: "Noto Serif KR Variable", "Noto Serif KR Fallback", AppleMyungjo, Batang, serif`. 불러오기는 `app/layout.tsx`의 `import "@fontsource-variable/noto-serif-kr/wght.css"`.
- 대체 글꼴: `@font-face { font-family: "Noto Serif KR Fallback"; src: local("AppleMyungjo"), local("Batang"); size-adjust: 100%; }`. 100%는 추정치이고 6단계 화면 확인에서 줄바꿈 차이를 보고 고친다.
- 읽기 폭: `--container-reading`을 48rem에서 `40em`으로(17px 기준 680px). 초안 칸과 알림 본문이 쓴다.

## 3. 부품과 화면

### 3.1 파일 (쓰기 허용 목록)

| 상태 | 파일 | 내용 | 요구 |
|---|---|---|---|
| 고침 | `web/package.json`, `web/pnpm-lock.yaml` | `class-variance-authority@0.7.1`, `tailwind-merge@3.7.0`, `@fontsource-variable/noto-serif-kr@5.3.1` 고정 | DS2-04, DS2-15 |
| 고침 | `lib/cx.ts` | 안쪽을 `extendTailwindMerge`(글자 크기 `display, title-lg, title-md, title-sm, reading, ui, label, caption, micro`, 그림자 `card, pop, dialog`, 모서리 `sm, md, lg`)로 | DS2-15 |
| 새로 | `lib/cx.test.ts` | `tokens.css`에서 글자 크기, 그림자, 모서리 이름을 읽어 각 이름 + 색 클래스가 둘 다 남는지. 같은 종류끼리는 뒤의 것만 남는지 | DS2-15 |
| 고침 | `styles/tokens.css`, `app/globals.css` | 2절 | DS2-01~04 |
| 고침 | `design/tokens.ts`, `design/contrast.test.ts`, `design/palette.test.ts` | 2.5, 2.6 | DS2-02, DS2-03 |
| 고침 | `scripts/verify-design-css.mjs`, `scripts/verify-design-css.test.mjs` | 2.7 | DS2-03 |
| 새로 | `lib/theme.ts`, `lib/theme.test.ts` | 2.4 | DS2-01 |
| 새로 | `components/ThemeMenu.tsx` | 2.4 | DS2-01 |
| 고침 | `app/layout.tsx` | 글꼴, 테마 스크립트, `<html data-theme>`, 토스트 제공자, 명령 팔레트, 검토 상세 폭(3.4) | DS2-01, 04, 07, 10, 11 |
| 고침 | `components/ui/Button.tsx` | cva 변형 `variant: primary, secondary, ghost, danger` × `size: sm, md`. props 유지 | DS2-15 |
| 고침 | `components/ds/Callout.tsx` | cva `tone`. props 유지 | DS2-15 |
| 고침 | `components/ds/badges.tsx` | `SourceLabel`을 3.2 함수와 cva 한 벌로. `StatusLozenge`, `CountBadge` cva | DS2-05, DS2-15 |
| 고침 | `lib/api.ts`, `lib/aiTrail.ts`, `lib/labels.ts`, `lib/labels.test.ts` | `TrailActor`에 `"replay"`. 3.2 `provenanceOf` | DS2-05 |
| 고침 | `components/ds/AiLabel.tsx`, `components/AiTrail.tsx`, `components/RunTimeline.tsx` | 출처 색을 `SourceLabel` 변형으로. `RunTimeline`의 `ACTOR` 표와 막대도 | DS2-05 |
| 새로 | `components/ds/CheckMark.tsx`, `lib/checkState.ts`(+test) | 3.5 | DS2-06 |
| 고침 | `components/DraftEditor.tsx` | 문장 머리의 "확인 필요"를 `CheckMark`로, 문장 선택(`onSelect`), 선택 강조 | DS2-06, DS2-07 |
| 고침 | `components/ui/Floating.tsx` | Popover에 `openOnHover`(지연 300ms, 닫힘 150ms)와 `surface-raised` | DS2-08 |
| 고침 | `components/ds/CitationChip.tsx` | `onSelect?(id)`. 없으면 지금처럼 팝오버 고정 | DS2-08 |
| 새로 | `lib/evidenceLink.ts`(+test) | 3.3 근거와 신구조문 연결 | DS2-07 |
| 새로 | `components/ds/SplitPane.tsx`, `lib/split.ts`(+test) | 3.4 | DS2-07 |
| 새로 | `app/reviews/[id]/EvidencePanel.tsx` | 3.3 | DS2-07 |
| 고침 | `app/reviews/[id]/ReviewClient.tsx` | 분할 배치, `selected` 상태, 단축키, 기존 신구조문 영역을 `EvidencePanel`로 옮김 | DS2-07, DS2-09 |
| 고침 | `components/ds/RedlineDiff.tsx` | 한 조만 그리는 `RedlineArticle` 내보내기(근거 패널이 씀) | DS2-07 |
| 고침 | `components/ui/ConfirmDialog.tsx` | `open`, `onOpenChange`를 받는 제어형 추가(단축키가 연다). 배경막 2.2 | DS2-09 |
| 새로 | `lib/shortcuts.ts`(+test), `components/Shortcuts.tsx` | 3.6 | DS2-09 |
| 새로 | `lib/inboxCache.ts`(+test) | 대기함 응답을 담는 저장소(`useSyncExternalStore`). 대기함 화면이 채우고 명령 팔레트가 읽는다 | DS2-10 |
| 새로 | `components/CommandPalette.tsx` | 3.7. `"use client"` 경계, `layout.tsx`가 그린다 | DS2-10 |
| 새로 | `components/ui/Toast.tsx` | 3.8 | DS2-11 |
| 고침 | `app/runs/[id]/RunClient.tsx` | 재실행 결과 토스트 | DS2-11 |
| 새로 | `components/ds/Skeleton.tsx` | `Skeleton`, `SkeletonRows`, `motion-reduce:animate-none` | DS2-13 |
| 고침 | `components/WakeGate.tsx` | `fallback?: ReactNode`(3.9). 실패 화면 렉스 120px | DS2-13, DS2-14 |
| 새로 | `components/ds/LogoMark.tsx` | 평면 SVG(문서와 저울), `currentColor`, 24px | DS2-14 |
| 고침 | `components/ds/AppHeader.tsx` | 로고 마크, `ThemeMenu`, `⌘K` 버튼, `text-brand-fg` | DS2-01, 10, 14 |
| 고침 | `components/ds/layout.tsx` | `PageHeader` 세리프, `EmptyState` 렉스 최대 120px, 제목 세리프 | DS2-04, DS2-14 |
| 고침 | `components/FirstVisitGuide.tsx`, `components/WakeWait.tsx` | `text-brand-fg` | DS2-02 |
| 고침 | `components/ReviewChat.tsx`, `components/Timeline.tsx` | `text-reading`, 출처 라벨 | DS2-04, DS2-05 |
| 고침 | 대기함 `app/page.tsx` | `j/k` 단축키, 스켈레톤, `inboxCache` 채우기 | DS2-09, 10, 13 |
| 고침 | 운영 현황 `app/ops/page.tsx` | 공급자 가로 막대(1.1), 스켈레톤, `source-*` 제거 | DS2-05, DS2-13 |
| 고침 | 법안 레이더 `app/radar/page.tsx` | 스켈레톤 | DS2-13 |
| 고침 | 알림함 `app/inbox/page.tsx` | `text-reading` | DS2-04 |
| 고침 | 실행 이력 `app/runs/page.tsx`, AI 사용 기록 `app/governance/page.tsx` | 토큰 확인(코드 변경은 출처 라벨뿐) | DS2-05 |
| 고침 | `app/design-system/page.tsx`, `app/design-system/Showcase.tsx` | 3.10 | DS2-16 |
| 고침 | `README.md`, `docs/screenshots/*` | 6단계 | DS2-16 |

### 3.2 출처 함수 (`lib/labels.ts`의 `provenanceOf`)

`provenanceOf(input) → { kind: "ai" | "rule" | "human" | "replay" | "neutral" | null; label: string; srLabel: string }`. `SourceLabel`, `AiLabel`, `AiTrail`, `RunTimeline`이 모두 이 함수와 `SourceLabel`의 cva 변형(`kind`)을 쓴다.

| 입력 | 종류 | 라벨 | 화면 읽기 |
|---|---|---|---|
| `providerKind` `nvidia` / `cloudflare` / `groq` | ai | `AI NVIDIA` / `AI Cloudflare` / `AI Groq` | "AI가 만들었어요, NVIDIA" 등 |
| `providerKind` `fallback` (가드레일 거절 `providerName === "guardrail"` 포함) | rule | `규칙` | "규칙이 만들었어요" |
| `providerKind` `replay` 또는 `isReplay` | replay | `재현` | "녹화한 예시예요" |
| `providerKind` `none` 또는 `null` | null(그리지 않음) | — | — |
| 사람: 승인 기록의 `actorRole` `lawyer` / `partner`, 흔적 단계 `actor: "human"`의 `detail.role` | human | `변호사` / `파트너`(역할을 모르면 `사람`) | "변호사가 결정했어요" |
| 흔적 단계 `actor: "ai"`, `"rule"` | ai / rule | `AI` / `규칙` | |
| 그 밖의 값 | neutral | 원문 그대로 | 원문 |

`isAiProvider("replay")`는 지금처럼 거짓이다. 재현은 AI 라벨을 달지 않는다.

### 3.3 근거 패널 (`EvidencePanel`, `lib/evidenceLink.ts`)

지금 API(`ReviewDetail.evidence[]`의 `id`, `title`, `url`, `excerpt` 최대 240자, `oldNew[]`의 `articleNo`, `before`, `after`, 변경 표시)만 쓴다.

- 연결 규칙 `oldNewFor(evidenceId, oldNew)`: `evidenceId.split(":", 3)`의 셋째 칸이 `articleNo`와 같은 항목. 백엔드 `ToOldNew`와 같은 규칙이다. `law:` 또는 `oldnew:`로 시작하지 않거나 칸이 모자라면 없음. 다른 법령의 같은 조 번호가 섞이지 않도록 둘째 칸(법령 ID)이 검토 대상 법령 ID와 다르면 없음.
- 패널 내용:
  - 위: "이 문장의 인용" 목록(선택 문장의 인용 칩, 첫 것이 선택됨). `selected === null`이면 "초안의 모든 인용" 목록.
  - 가운데: 선택한 근거의 제목, 출처 라벨 자리 없음(근거는 법제처 원문), 앞부분(`excerpt`, 끝에 "…"), 원문 링크(`safeHttpUrl`).
  - 아래: 연결된 신구조문이 있으면 `RedlineArticle` 한 조. 없으면 "이 근거에는 신구조문이 없어요".
  - 맨 아래: "인용하지 않은 변경 조문" 접힘 구역(지금 화면 아래 신구조문 영역의 나머지 조).
- 강조: 선택된 문장은 왼쪽 테두리 3px `brand`와 `brand-subtle` 바탕, 선택된 인용 칩은 `brand` 테두리. 패널은 바뀔 때 맨 위로 스크롤한다.
- 1280px 미만: 같은 `EvidencePanel`이 초안 아래에 온다(CSS 그리드 한 줄). 기존 "신구조문 비교" 구역은 이 패널로 옮기고 지운다.

### 3.4 검토 상세 배치와 SplitPane

- 폭: `app/layout.tsx`의 `<main>`은 `max-w-workspace`를 유지한다. 검토 상세 화면만 `ReviewClient` 바깥 래퍼가 `xl:w-[calc(100vw-2rem)] xl:max-w-[96rem] xl:relative xl:left-1/2 xl:-translate-x-1/2`로 벗어난다. 푸터는 그대로 둔다.
- 높이: 헤더는 `h-[var(--lw-header-h)]`(64px)로 고정한다. 분할 영역은 `xl:h-[calc(100dvh-var(--lw-header-h)-6rem)]`(6rem은 아래 고정 결정 상자 자리), 두 칸이 각자 `overflow-y-auto`.
- SplitPane: 기본 비율 초안 58%, 최소 초안 40%, 최대 70%. 분리선 `role="separator" aria-orientation="vertical" aria-valuenow aria-valuemin=40 aria-valuemax=70 aria-label="초안과 근거 폭"`, `tabIndex=0`. 화살표 왼쪽/오른쪽 2%, Shift와 함께 10%, Home 40%, End 70%. 포인터 끌기는 `setPointerCapture`. 비율 계산 `clampRatio(px, total)`은 `lib/split.ts` 순수 함수. 저장하지 않는다.
- 결정 상자: 지금처럼 화면 아래 고정(`sticky bottom-4`). 사유 입력, 재검사 적발 안내, 파트너 상자 위치는 바꾸지 않는다.
- AI 흔적(`AiTrail`)은 지금 위치(제목 아래, 접힌 단계 목록) 그대로다.

```
┌ 헤더 (64px) ───────────────────────────────────────────────────────┐
│ 법령명(세리프) · 상태 · 출처      AI 흔적 요약                     │
├────────────────────────────────┬┬─────────────────────────────────┤
│ 검사 요약: 확인 필요 2 · 다음   ││ 이 문장의 인용  [제13조] [제14조] │
│ ✓ 문장 1 …  [제13조]           ││ 제13조(경영의 건전성 기준)        │
│▌! 문장 2 …  [제14조][제20조]    ││ 앞부분 240자… 원문 ↗             │
│   └ AI 판정: …                 ││ 신구조문: 삭제 ▒ / 추가 ▓        │
│ 열린 질문                      ││ ▸ 인용하지 않은 변경 조문         │
│ 검토 보조(변호사)              ││                                 │
├────────────────────────────────┴┴─────────────────────────────────┤
│ [반려] [수정] [승인]   (화면 아래 고정 결정 상자)                  │
└────────────────────────────────────────────────────────────────────┘
범례: ✓ 검사 통과, ! 확인 필요, ▌ 선택한 문장, ││ 분리선
```

### 3.5 문장별 검증 상태 (`lib/checkState.ts`, `CheckMark`)

`checkState({ findings, critic, editing, edited }) → "passed" | "flagged" | "unchecked"`.

| 조건 (위에서부터 먼저 맞는 것) | 상태 | 기호와 색 | 글자 |
|---|---|---|---|
| 수정 중이고 이 문장을 고쳤다 | unchecked | 빈 원, `ink-subtle` | 검사 전 |
| 적발(`findings.length > 0`) | flagged | 느낌표 삼각형, `danger` | 확인 필요 |
| 크리틱 단계(`critic_citation`, `critic_source`, `critic_judge`) 중 하나라도 `done`이 아님(`running`, `waiting`, `not_run`, `failed`, `skipped`) | unchecked | 빈 원, `ink-subtle` | 검사 전 |
| 그 밖 | passed | 체크 원, `success` | 검사 통과 |

- props: `<CheckMark state />`. 기호는 `aria-hidden`, 글자는 보이는 글로 둔다.
- 상단 요약(초안 머리, 지금 `reviewAttention` 자리): "검사 통과 3 · 확인 필요 2 · 검사 전 0", 아래 줄 "자동 검사 통과는 내용이 맞다는 뜻이 아니에요. 근거와 대조해 주세요." 요약의 "다음" 버튼은 `]`와 같다.
- `critic` 입력은 `aiTrail.stages`에서 `findStage`로 읽는다. 흔적이 없는 옛 응답이면 크리틱 조건을 건너뛴다(지금 동작과 같음).

### 3.6 단축키 (`lib/shortcuts.ts`)

판정 `shouldHandle(e, doc)`가 거짓이면 무시한다.
- `e.defaultPrevented`, `e.isComposing`, `e.keyCode === 229`, `e.repeat`
- 수정자(⌘, Ctrl, Alt)가 눌림. 단 `⌘K`/`Ctrl+K`는 별도 처리
- 허용 목록: `document.activeElement`가 `body`이거나 `<main>` 안의 `a`, `button`, `article[tabindex]`일 때만
- 조상에 `[role=menu|listbox|combobox|dialog|alertdialog|separator]`가 있으면 무시

글자 키는 `e.code`로 판정한다(`KeyJ` 등). 한글 입력기가 켜져 있으면 `e.key`가 `"ㅓ"`나 `"Process"`로 와서 `e.key`로는 판정할 수 없다. `?`는 `e.code === "Slash" && e.shiftKey`.

| 키 (`e.code`) | 화면 | 동작 | 조건 |
|---|---|---|---|
| `KeyJ` / `KeyK` | 대기함 | 다음/이전 행 링크에 실제 포커스 이동(`focus()`), 처음에는 첫 행 | 행이 있음 |
| `Enter` | 대기함 | 포커스된 링크의 기본 동작(따로 처리하지 않음) | |
| `BracketRight` / `BracketLeft` | 검토 상세 | 다음/이전 **확인 필요** 문장 선택, 끝에서 처음으로 돈다. 확인 필요가 없으면 모든 문장을 돈다 | |
| `KeyA` | 검토 상세 | 화면의 승인 버튼과 같은 처리 함수로 확인 대화상자를 연다. 수정 중이면 "수정 후 승인" 대화상자 | 변호사이고 결정 가능 상태. 재검사 적발이 남아 확인 체크가 필요하면 버튼이 비활성인 것처럼 아무것도 하지 않는다. 파트너는 `AwaitingPartner`일 때 최종 승인 대화상자 |
| `KeyE` | 검토 상세 | 수정 토글(화면의 수정, 수정 취소 버튼과 같은 함수) | 변호사이고 결정 가능 상태 |
| `KeyR` | 검토 상세 | 반려(파트너는 돌려보내기) 사유 입력을 열고 입력창에 포커스 | 결정 가능 상태 |
| `Slash`+Shift | 모든 화면 | 단축키 도움말 대화상자 | |
| `KeyK`+⌘/Ctrl | 모든 화면 | 명령 팔레트 | 입력창 안에서도 동작 |

단축키는 보이는 버튼과 같은 처리 함수를 같은 조건(버튼의 `disabled` 계산)으로 부른다. 명령 등록소는 만들지 않는다. `useShortcuts(map)` 훅이 `keydown`을 `window`에 걸고 `shouldHandle`을 거친다.

### 3.7 명령 팔레트 (`CommandPalette`)

Base UI Dialog 안에 Autocomplete(`inline`, `autoHighlight`). 묶음과 문구:

| 묶음 | 항목 |
|---|---|
| 이동 | 승인 대기함, 실행 이력, 알림함, 법안 레이더, 운영 현황, AI 사용 기록, 디자인 시스템 |
| 검토 | `inboxCache`의 검토마다 "{법령명} · {그룹명}". 검색 필드는 법령명과 그룹명 |
| 역할 | 변호사로 보기, 파트너로 보기, 관리자로 보기 |
| 보기 | 테마: 시스템, 테마: 밝게, 테마: 어둡게 |

- 운영 현황과 AI 사용 기록은 관리자 역할에서만 보인다(헤더 메뉴와 같은 조건).
- 결과가 없으면 `Autocomplete.Empty`에 "찾는 항목이 없어요".
- 조합 중 Enter: 입력의 `onKeyDown`에서 `e.nativeEvent.isComposing || e.keyCode === 229`면 `e.preventDefault()`로 항목 실행을 막는다. 이 판정을 `shortcuts.ts`의 `isComposingEvent`로 두어 테스트한다.
- `inboxCache`가 비어 있으면(대기함을 아직 안 열었거나 서버가 잠듦) 검토 묶음을 그리지 않는다.

### 3.8 토스트 (`ui/Toast.tsx`)

`<ToastProvider>`(Base UI `Toast.Provider` `limit={3}` `timeout={5000}`)를 `layout.tsx`가 감싸고 `Toast.Viewport`는 화면 오른쪽 아래. `useToast()` → `{ add(title: string, description?: string) }`, `priority: "high"`.

| `retryRun` 결과 | 표시 |
|---|---|
| `ok` | 토스트 "재실행을 접수했어요" + 화면 다시 불러오기 |
| `nothing` | 토스트 "다시 실행할 단계가 없어요" |
| `waking` | Callout(지금과 같음) |
| `forbidden`, `error` | Callout(지금과 같음) |

### 3.9 스켈레톤과 WakeGate

지금 WakeGate는 첫 요청이 400ms(`wake.ts` `SHOW_DELAY_MS`) 안에 오지 않으면 렉스 대기 화면으로 바꾼다. 새 규칙: `fallback`이 있으면 처음 400ms 동안 `fallback`(스켈레톤)을 보이고, 그 뒤에도 서버 `/health`가 응답한 상태(`apiUp`)면 계속 스켈레톤, `/health`도 응답하지 않으면 렉스 대기 화면. 즉 렉스는 서버가 잠든 경우에만 나온다.

| 화면 | 스켈레톤 모양 |
|---|---|
| 대기함 | 그룹 카드 2개 × 행 3줄 |
| 검토 상세 | 제목 줄, 문장 카드 3개(1280px 이상은 오른쪽 패널 막대 4줄) |
| 법안 레이더 | 탭 줄, 카드 4개 |
| 운영 현황 | 숫자 타일 4개, 막대 6줄 |

### 3.10 디자인 시스템 화면 (`/design-system`)

구역: 표지(세리프 `text-display`), 색(두 테마 견본을 나란히, 각 견본 상자에 `data-theme`, 대비 값 표), 글자 척도, 읽기 글꼴 비교(같은 초안 문장을 산세리프와 세리프로), 출처 라벨 네 종류와 CheckMark 세 상태, 버튼과 Callout 변형, 분할 화면 축소 예시, 토스트 예시 버튼, 단축키 표, 렉스 사용 위치.

## 4. 단계와 검증

모든 단계의 통과 명령(`web/`에서): `pnpm test && pnpm exec tsc --noEmit && pnpm exec eslint src && pnpm build`. 단계마다 빌드가 통과해야 다음으로 간다.

| 단계 | 내용 | 요구 | 필수 |
|---|---|---|---|
| 1 | 의존성 고정 설치. `cx` 교체와 `cx.test.ts`(회귀: 기존 화면 클래스 조합이 바뀌지 않음). 토큰 두 테마, `@theme inline`, 그림자, `globals.css`, 테마 스크립트와 `ThemeMenu`, 글꼴. 대비, 팔레트, 빌드 검사. **같은 단계에서** `source-*`를 지우고 `badges.tsx`, `ops/page.tsx`(공급자 가로 막대), `design-system/page.tsx`의 `parseColorTokens` 호출부, `RunTimeline.tsx`의 `var(--color-*)`, 2.2 표의 뒤집힘 조합을 고친다 | DS2-01~05, DS2-15 | 예 |
| 2 | `provenanceOf`, `SourceLabel` cva, `AiLabel`, `AiTrail`, `RunTimeline`. Button, Callout cva. `CheckMark`, `checkState`. `LogoMark`, 헤더, `EmptyState`, WakeGate 실패 화면 렉스 | DS2-05, 06, 14, 15 | 예 |
| 3 | 검토 상세: `evidenceLink`, `EvidencePanel`, `SplitPane`, 배치, Popover 마우스 올림, CitationChip `onSelect` | DS2-07, 08 | 예 |
| 4 | 단축키, `ConfirmDialog` 제어형, 도움말, `inboxCache`, 명령 팔레트, 토스트와 RunClient | DS2-09~11 | 다음 |
| 5 | 스켈레톤과 WakeGate 규칙, `/design-system` | DS2-13, 16 | 선택 |
| 6 | 배포 뒤 화면 확인과 README 스크린샷 | 전체 | 예 |

의도한 중간 상태: 없음. 단계 1이 토큰 이름과 쓰는 곳을 함께 바꾸므로 어두운 테마에서 깨진 대비가 남지 않는다.

축소 순서(spec 4.1): 검토 검색 → 분할 폭 끌기(고정 58:42) → 스켈레톤은 대기함과 검토 상세만 → 읽기 글꼴 비교 칸.

### 4.1 자동 테스트

| 파일 | 경우 |
|---|---|
| `lib/cx.test.ts` | `tokens.css`의 글자 크기 이름 전부 × `text-ink`가 둘 다 남음. `shadow-card` + `shadow-pop`은 뒤의 것. `rounded-md` + `rounded-lg`는 뒤의 것. `p-2 p-4`는 `p-4` |
| `lib/theme.test.ts` | 저장 `dark` → `dark`. `light` → `light`. `system` + 시스템 어두움 → `dark`. 모르는 값 → 시스템 따름. `localStorage` 예외 → 속성을 바꾸지 않음 |
| `design/contrast.test.ts` | 두 테마의 모든 쌍. 두 블록의 키 집합 같음. 출처 색상각 × 두 테마 |
| `design/palette.test.ts` | `dark:`, `-[#`, 정의 안 된 색 이름, `outline-offset-0`, `ring-inset` 없음 |
| `scripts/verify-design-css.test.mjs` | 새 계약 통과 예시 1, 실패 예시(어두운 블록 없음, 클래스가 `--color-*`를 씀, `outline-offset` 없음) |
| `lib/labels.test.ts` | `provenanceOf` 3.2 표의 모든 행 |
| `lib/evidenceLink.test.ts` | 같은 조 연결, `law:`와 `oldnew:` 접두, 다른 법령 같은 조 번호는 없음, 칸 부족은 없음 |
| `lib/checkState.test.ts` | 3.5 표의 네 줄과 흔적 없음 |
| `lib/split.test.ts` | 최소, 최대로 자름. 키 이동 2%, 10%, Home, End |
| `lib/shortcuts.test.ts` | `isComposing`, `keyCode 229`, 입력창, 메뉴 조상, 수정자, `e.code` 판정(한글 `e.key`), `⌘K` 예외 |
| `lib/inboxCache.test.ts` | 채우기, 구독 알림, 비었을 때 |

### 4.2 화면 확인 (6단계)

일곱 화면(승인 대기함, 검토 상세, 실행 이력, 실행 타임라인, 알림함, 법안 레이더, 운영 현황)과 AI 사용 기록, `/design-system`을:
- 밝은 테마와 어두운 테마, 1440px과 390px에서 본다.
- 읽을 수 없는 글자, 사라진 경계선, 고정 흰 바탕이 없는지 본다.
- 새로 고침 때 첫 그림이 다른 테마로 깜박이지 않는지 본다(어두운 테마 저장 상태에서).
- macOS 한글 입력기를 켠 채 대기함에서 `j`(ㅓ)가 동작하고, 명령 팔레트에서 "근로" 조합 중 Enter가 항목을 실행하지 않는지 본다.
- 1280px 경계 위아래로 검토 상세를 본다.

## 5. 위험

- `cx` 동작이 바뀌어 화면에서 넘긴 클래스가 부품 기본 클래스를 덮게 된다. 의도한 변화지만 지금 화면이 우연히 두 값을 함께 쓰던 곳이 있으면 모양이 바뀐다. 단계 1 직후 로컬에서 일곱 화면을 훑는다.
- 세리프 대체 글꼴의 `size-adjust`는 추정치다.
- 분할 영역 높이 계산이 결정 상자 높이(6rem)에 묶여 있다. 결정 상자가 사유 입력으로 커지면 아래가 가려질 수 있어, 그때는 분할 영역 아래 여백으로 받는다.

## 7. 재검토 조치 (2026-11-25)

| 지적 | 조치 |
|---|---|
| B1 배경막 | 2.1, 2.2에 `scrim` 토큰. 구현 완료 |
| M1 세리프 빌드 검사 | 단계 1에서 `PageHeader`(`components/ds/layout.tsx`)에 `font-serif`를 붙여 `--font-serif`가 빌드 CSS에 나온다. 구현 완료, 빌드 검사 통과 |
| M2 색 이름 판정 | 규칙: `tokens.css`의 `--color-*` 이름에서 첫 마디(`ink`, `surface`, `prov` 등)와 지운 `source`를 "색 뿌리"로 모은다. `(bg\|text\|border(-[trblxy])?\|ring\|outline\|fill\|stroke\|divide\|decoration\|from\|to\|via)-이름(/불투명도)?`에서 이름의 첫 마디가 색 뿌리일 때만 정의 여부를 본다. 그래서 `text-ui`, `text-left`, `border-l-4`는 대상이 아니다. 예시 문자열 테스트 포함. 구현 완료 |
| M3 테마 스크립트 | 저장소 예외는 시스템 설정을 따른다(`getItem`만 안쪽 `try`). `ThemeMenu`가 `useLayoutEffect`로 한 번 다시 적용하고(Strict Mode 재마운트 대비), 시스템 선택이면 `matchMedia` 감시를 건다. `ThemeMenu`는 단계 1에서 헤더에 붙는다. 구현 완료 |
| M4 `isAiProvider("replay")` | 지금 참이다(3.2의 "지금처럼 거짓"은 사실과 달랐다). 바꾸지 않는다. `provenanceOf`가 `replay`를 AI보다 먼저 판정해 재현에 AI 라벨이 붙지 않는다. 구현 완료 |
| M5 근거 연결 | 필드는 `sourceUrl`, `excerpt`는 선택. 서명은 `oldNewFor(evidenceId, evidence, oldNew)`. 같은 조 번호(`split(":", 3)[2]`)이고, `evidence`에 `oldnew:{같은 법령 ID}:…`가 하나라도 있으면 그 법령 ID와 둘째 칸이 같아야 한다. `oldnew:` 근거가 없으면 조 번호만으로 잇는다. `excerpt`가 없으면 "앞부분을 받지 못했어요"와 원문 링크만 |
| M6 헤더 높이 | `--lw-header-h`는 78px(안내 띠 28px + 메뉴 줄 50px, `xl`에서 줄바꿈 없음). 분할 영역은 높이를 계산하지 않고 `xl:sticky xl:top-[var(--lw-header-h)]`인 오른쪽 패널(`max-h-[calc(100dvh-var(--lw-header-h)-7rem)] overflow-y-auto`)과 페이지와 함께 흐르는 왼쪽 초안으로 둔다. 페이지 스크롤은 하나다 |
| M7 미리보기 포커스 | Base UI Popover는 포커스로 열리지 않는다. DS2-08을 "마우스를 올리면 미리보기, 포커스 뒤 Enter나 클릭이면 고정"으로 좁힌다(spec 수정). `onSelect`가 있으면(1280px 이상 분할 화면) 클릭과 Enter가 팝오버 대신 근거 패널을 바꾸고, 마우스 올림 미리보기는 그대로 둔다. 1280px 미만은 `onSelect`를 넘기지 않아 지금처럼 팝오버로 고정한다 |
| M8 명령 팔레트 | `<Autocomplete.Root inline open autoHighlight>` |
| M9 스켈레톤 규칙 | 건강 상태를 `unknown \| up \| waking` 셋으로 둔다. `fallback`이 있으면 `unknown`과 `up`에서는 스켈레톤, `/health`가 실패하거나 시간 초과일 때만(`waking`) 렉스 대기 화면. 스켈레톤을 보이던 중이면 완료 유지(`DONE_HOLD_MS`)를 건너뛴다 |
| M10 범위 맞춤 | 고정 비율은 58:42 하나(spec도 같게). DS2-16에서 "밀도 비교", DS2-15에서 "표 행"을 뺀다. 두 테마 견본은 단계 1에 넣었다(구현 완료) |
