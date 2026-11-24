# EGM 통합 검토 보고서: 디자인 시스템 v2 설계

- 대상: `docs/features/design-system-v2/design.md` (초안 v1, 2026-11-24, 검토 중 변경 없음). 요구 기준은 `spec.md` DS2-01~16
- 방식: 통합 보고서(중간 확인 없이 다섯 단계 실행). FeatureRange 1–83(1–2절), DetailedRange 84–173(3–5절)
- 다시 열지 않은 작성자 결정: 법률 AI 편집 디자인 방향, 렉스 축소 유지, 다크 모드 포함, cva와 tailwind-merge, Base UI Toast와 Autocomplete, Noto Serif KR. 백엔드와 AI 서비스 변경은 범위 밖
- 검토자 10회 실행, 모두 새 문맥, 실패나 미실행 없음:

| 단계 | 검토자 | 상태 | 보고서 |
|---|---|---|---|
| 5 범위 | egm-zoom-out, egm-inventive-catalyst | completed | [zoom_out_report.md](zoom_out_report.md) |
| 6 이해도 | egm-comprehension | completed | [comprehension_report.md](comprehension_report.md) |
| 7 설계 비판 | egm-design, egm-structural, egm-style | completed | [critic_report.md](critic_report.md) |
| 8 중간 범위 | egm-zoom-out | completed | [intermediate_zoom_out_report.md](intermediate_zoom_out_report.md) |
| 9 구현 준비 | egm-implementation-readiness, egm-codebase-details, egm-style | completed | [implementation_readiness_report.md](implementation_readiness_report.md) |

## 결론

방향(토큰 이름 유지, 값만 두 테마로, 출처를 "누가 만들었나" 네 종류로, Base UI 재사용, next-themes와 shadcn과 cmdk 배제)에 반대한 검토자는 없다. 그러나 **구현에 들어갈 수 없다.** 근거는 세 가지다.

1. 단계 1이 통과 명령을 넘지 못한다. 빌드 검사 기대값이 스스로 고른 `@theme inline`과 모순되고, 지운 `source-*` 토큰과 바뀐 `parseColorTokens`를 쓰는 파일은 뒤 단계에서야 고친다.
2. 핵심 문제 1(근거 대조)의 분할 화면을 지금 API와 화면 틀로 그릴 수 없다. 근거 본문은 240자 excerpt뿐이고 근거와 신구조문을 잇는 규칙이 없으며, `<main>`이 1120px로 묶여 있다.
3. 핵심 문제 2(누가 썼나와 맞나의 구분)가 고쳐지지 않는다. 출처 색이 코드 네 곳에 있는데 설계는 한 곳만 고쳐, AI와 사람 표시가 계속 상태 색(`info`, `success`)을 쓴다.

단계별 판정: Stage 5 조건부 통과(Blocker 1), Stage 7 수정 후 재검토(Blocker 1, 두 검토자가 같은 결함), Stage 8 조건부 통과, Stage 9 구현 불가(Blocker 7, 중복 제거 뒤 4).

## 조치 항목

### 🔴 Blocker

| 조치 | 내용 | 원 지적 |
|---|---|---|
| A1 | **빌드 검사 계약을 다시 쓴다.** 2.4를 "유지 / 삭제 / 변경 / 추가" 표로. 클래스 기대값은 `var(--lw-*)`(`@theme inline`은 `--color-*`를 거치지 않음). 삭제: `verify-design-css.mjs` 14–21행 COLORS 루프(v2 소스에 `--color-X: #hex`가 없어 TypeError), 32행 `color-scheme:light`. 변경: 41행 `--text-title-lg`를 `1.625rem`으로, 24–31행 `var(--color-*)` 규칙. 유지: `--background`/Arial 감지. `verify-design-css.test.mjs` 고정값 갱신. `RunTimeline.tsx:20, 128`이 직접 쓰는 `var(--color-danger)`, `var(--color-danger-bg)`, `var(--color-line)`을 `var(--lw-*)`로 | CR-B1, ST-B1, CD-B1, CD-B4 |
| A2 | **출처 표현을 한 곳으로 모은다.** `TrailActor`(`lib/api.ts:80`)에 `"replay"`를 더하고 `aiTrail.ts`의 `ACTOR_LABEL` 옆에 `providerKind → TrailActor` 함수 하나. 색은 `SourceLabel` cva 한 벌을 `AiLabel`, `AiTrail`, `RunTimeline` ActorBadge와 막대가 쓴다. 3.1에 세 파일, 3.2 표는 이 함수의 명세로(위치는 `labels.ts`가 아니라 지금 `badges.tsx` `SOURCES`). 표에 넣을 것: `none`은 그리지 않음, `isAiProvider("replay")` 수정 여부, 사람 종류의 입력(`approval.actorRole`, `actor: "human"`과 `detail.role`)과 "변호사"/"파트너" 선택, "가드레일 거절"의 판정 필드, 모르는 값은 `neutral`, 최종 라벨 글자(`AI NVIDIA` 등)와 sr-only 문구 | ZO-B1, ZO2-A5, CD-M1, CD-M2, IR-M2, IR-M1(AiLabel), SY9-M8~M11, IR-N3, CO-3, ZO2-N6 |
| A3 | **근거 패널을 지금 API 안에서 정의한다.** "본문"은 excerpt(240자)로 한정. 신구조문 연결 규칙 `id.split(":", 3)[2] === oldNew.articleNo`(백엔드 `ToOldNew`와 같음)를 `lib/` 순수 함수와 테스트로, 다른 법령의 같은 조 번호 처리. `selected === null`일 때 내용, "강조" 모양, 인용되지 않은 조문을 어디서 보는지, 1280px 미만에서도 같은 `EvidencePanel`을 CSS 그리드로 아래에 두고 기존 신구조문 영역을 옮긴다 | IR-B1, CD-B3, SY9-M12, SY9-N3, IR-N4, ZO2-N3 |
| A4 | **분할 화면이 들어갈 틀을 정한다.** 검토 상세가 `max-w-workspace`(1120px, `app/layout.tsx:20`)를 벗어나는 방법. 헤더 높이를 `--lw-header-h`로 고정하고 두 칸 `calc(100dvh - var(--lw-header-h))`, 각자 `overflow-y: auto`(5절에서 3.3으로 이동). footer(`layout.tsx:23`) 처리. SplitPane 기본 비율, 최소·최대, 화살표 한 번 이동량, Home/End, `aria-valuenow/min/max` | IR-B2, SY9-M5, SY9-M17, IR-N5 |
| A5 | **단축키 판정과 동작을 확정한다.** 글자 키는 `e.code`(`KeyJ` 등)로 판정(한글 입력기에서는 `e.key`가 `"ㅓ"`나 `"Process"`). spec 5절대로 허용 목록: `body`이거나 `<main>` 안 링크·버튼일 때만, `[role=menu\|listbox\|combobox\|dialog\|alertdialog\|separator]` 조상, `e.defaultPrevented`, `isComposing`, `keyCode 229`이면 무시. 키 표(키, 동작, 화면, 필요한 역할과 상태). 동작 표: 수정 중 `a`, 재검사 적발이 남은 `a`, 파트너 AwaitingPartner의 `a`, `r`의 사유 입력 포커스, `e` 토글, `]/[`는 확인 필요 문장만 순환하는지, `j/k`는 행 링크에 실제 포커스 이동. `ConfirmDialog`에 `open`/`onOpenChange` 추가. 단축키는 보이는 버튼과 같은 처리 함수를 같은 조건으로 부른다(명령 등록소는 만들지 않음) | IR-B3, IC-3, ZO2-A3, ZO2-A2, IC-2, IR-M5, CD-M5, CD-N7, SY9-M2, SY9-M13, CO-5, ZO-N3, ZO2-N2 |
| A6 | **단계마다 빌드가 통과하게 한다.** `source-*` 삭제를 그 토큰을 쓰는 `badges.tsx:32-56`, `ops/page.tsx:15-16`, `design-system/page.tsx:19`를 고치는 단계와 같은 단계로. `parseColorTokens`를 바꾸는 단계에 `design-system/page.tsx:4,12`와 `contrast.test.ts:4,12,40`의 최소 수정을 포함. 단계 1과 2 사이 어두운 테마 위험 버튼의 대비 미달처럼 의도한 중간 상태는 적는다 | CD-B2, IC-5, IR-N2 |

### 🟡 Major

| 조치 | 내용 | 원 지적 |
|---|---|---|
| A7 | **테마 CSS를 두 블록으로 줄인다.** 인라인 스크립트가 늘 실제 테마를 `data-theme`에 적고, CSS는 `:root{}`와 `[data-theme="dark"]{}`(`:root` 한정 없이, `/design-system`에서 견본 일부만 어둡게 그릴 수 있게) 두 블록만. 같은 블록에 `color-scheme: light`/`dark`. "시스템"이면 `matchMedia` change 리스너. 미디어 블록과 동일성 검사는 삭제, 밝은 블록과 어두운 블록의 키 집합 동일 테스트는 추가. 1.2에 `light-dark()`를 버린 이유(Lightning CSS 변환 미확인). 스크립트 원본은 문자열 하나로 두고 `theme.test.ts`가 가짜 `localStorage`와 `matchMedia` 위에서 실행. 삽입 방식은 `node_modules/next/dist/docs/01-app/02-guides/preventing-flash-before-hydration.md`가 안내하는 방식 하나로 정하고 그 조건(한 번만 삽입하는 `useRef` 플래그, Strict Mode 재적용)을 적는다. `applyTheme`이 저장 값과 `data-theme`을 어떻게 다루는지, 테마 메뉴 부품, 탭 간 동기화 여부 | ZO-M1, IC-1, CR-M6, CR-M1, CR-M3, ST-M5, ST-M7, SY-M8, CO-12, IR-M10, IR-M11, ZO2-A1, ZO2-M7, ZO-N2, CD-N5 |
| A8 | **대비 쌍과 소스 검사를 사용처 기준으로.** `CONTRAST_PAIRS` = research 47쌍 + `on-danger/danger` + 실제 쓰는 조합(`ink`와 `ink-muted` / Callout `*-bg`, 툴팁 쌍, `neutral/neutral-bg`). `text-brand`를 글자로 쓰는 세 곳(`AppHeader.tsx:53`, `FirstVisitGuide.tsx:16`, `WakeWait.tsx:217`)은 `brand-text`로. v1 `focus/brand` 한 쌍만 삭제(47쌍에는 원래 없음). 색상각 검사를 `PROV_TOKENS` × 두 테마로, `prov-replay` 외 채도 0.3 이상 단언. 포커스: 전역 `:focus-visible{outline-offset:2px}`와 빌드 검사 한 줄, `palette.test.ts`에 `outline-offset-0`/`ring-inset` 금지(두 겹 링은 만들지 않음). DS2-03 소스 검사: `palette.test.ts`가 `dark:`, `-[#`, `tokens.css`에 없는 색 이름(지운 `source-*` 포함)을 실패로. 밝은 테마 값 변경에 따른 `contrast.test.ts:12` 기대값 수정. `on-danger`의 계산 대비값(어두운 테마 흰 글자 ≈ 2.4:1 미달) | CR-M4, CR-M5, CR-N5, ST-M3, ST-M4, ST-N3, SY-M10, SY-M11, CD-M7, IC-5, IC-6, ZO2-A4, ZO2-N5, IR-M14(DS2-03) |
| A9 | **테마에 따라 뒤집히는 조합과 그림자.** 2.2에 목록: 툴팁 `bg-ink text-on-brand`(`Floating.tsx:35`), 배경막 `bg-ink/40`(`ConfirmDialog.tsx:39`), `.lw-stripes` 흰색 rgba(`globals.css:44-48`), 고정 `rgb(26 34 51 / …)` 그림자. 그림자는 `@theme inline { --shadow-card: var(--lw-shadow-card) }` 꼴과 세 그림자 두 테마 값. `surface-raised`로 옮길 부품 목록. `neutral`은 `@theme inline`에서 `--color-neutral: var(--lw-ink-muted)` 별칭(파서 수정 불필요). 위험 버튼 hover는 `hover:bg-danger/90`(새 토큰 없음) | ZO-M3, CD-M6, CR-N2, CR-N3, ST-M6, SY-N7, CD-N3, IC-8, CO-9, ZO2-A8 |
| A10 | **운영 현황 공급자 차트.** `ops/page.tsx`의 쌓은 막대를 공급자별 가로 막대 목록(이름, `prov-ai` 막대, 건수와 비율)으로. 새 토큰 없음. 1.1에 결정 한 줄 | ZO-M4, CR-M2, IR-M1, CD-M3, ZO2-A6 |
| A11 | **CheckMark 상태를 정의한다.** `DraftEditor.tsx:41, 94`의 문장별 표시를 추출한다. 단 `findings.length`만 보면 크리틱이 안 끝났거나(`running/not_run/failed`) 수정 중이라 적발이 낡아도 "검사 통과"가 뜬다. `checkState(sentence, trail, editing) → "passed" \| "flagged" \| "unchecked"`와 판정 표, 기호와 색, props, 상단 요약 문구의 자리 | IR-M3, ZO2-M3, CO-6 |
| A12 | **[작성자 결정] 결정 버튼 위치.** DS2-07은 버튼 위치를 요구하지 않는다. 헤더로 올리면 사유 입력, 재검사 적발 Callout, 파트너 상자(`ReviewClient.tsx:184-303`)의 위치와 1280px 경계 상태 공유가 모두 새로 정해져야 한다. 검토자 권고는 아래 고정 상자 유지(146행과 그림의 헤더 버튼 삭제). 올린다면 위 요소 위치, sticky, 두 칸 위 전체 폭인지, `DecisionBar` 하나를 DOM에 한 번만 두는 방식. 147행 "AiTrail은 헤더 아래(지금과 같음)"는 사실과 다르니 정정 | ZO2-M1, IR-M4, CD-M10, IC-9, SY9-N11 |
| A13 | **[작성자 결정] 마감에 맞춘 범위.** (1) 밀도(DS2-12)를 지금 다음 범위로 넘기고 spec 3절 "하지 않을 것"으로(대기함은 표가 아니라 링크 격자, `app/page.tsx:23`). 남긴다면 값(48/40/32px), `--lw-row`를 읽는 요소, 스크립트로 첫 그림 전 적용, `DensityProvider` 삭제. (2) 분할 폭 저장 `lw_split` 삭제(spec에 없음). (3) 토스트는 재실행 접수(`app/runs/[id]/RunClient.tsx:23`) 하나로, 테마 변경과 링크 복사(코드에 없음) 토스트를 빼는 spec DS2-11 수정. `retryRun` 결과(`ok`, `nothing`, `forbidden`, `error`, `waking`)별 토스트와 Callout, `useToast()` 시그니처, 5000ms, 3개, Viewport 위치. (4) 4절에 필수(DS2-01~08, 14, 15의 Button, badges, Callout)와 선택의 경계, 분할 화면 단계를 단축키 앞으로. (5) 축소 순서: 검토 검색 → 분할 폭 끌기 → 스켈레톤을 대기함과 검토 상세로 → `/design-system` 비교 칸. 하나 뺄 때마다 해당 DS2 수용 기준 수정을 한 줄씩(끌기를 빼면 DS2-07 위반). DS2-15 "표 행" cva는 대상이 없으면 뺀다. 저장 설정이 테마 하나로 줄면 RoleSwitch 구독 패턴을 따르고 일반화하지 않는다 | ZO2-M2, ZO2-M4, ZO2-M5, ZO2-M6, ZO2-A7, IR-M7, IR-M9, IR-N1, CD-M4, CD-N6, SY9-M4, SY9-M7, ZO-M2, IC-7, CO-7 |
| A14 | **인용 미리보기.** 기존 `ui/Floating.tsx`의 Base UI Popover에 `openOnHover`와 지연을 더해 레이어 하나로(마우스 올림은 임시, 클릭과 Enter는 고정, 분할 화면에서는 패널로 이동). PreviewCard를 따로 쓰지 않는다면 그 이유 한 줄. 칩은 `onSelect(id)` 콜백 하나, 1280px 분기는 ReviewClient 한 곳에서 CSS로. 포커스로도 열림(DS2-08), Esc 뒤 포커스 복귀, 카드 내용(제목, 앞 240자, 원문 링크, 출처 라벨 여부), ReviewChat 칩 적용 여부 | IC-4, IR-M12, CD-M8, SY9-M6, CO-10, ZO2-A8 |
| A15 | **명령 팔레트.** 항목 정확한 문구와 묶음("보기" = 테마, 밀도). "대기함을 한 번 받은 결과"를 공유할 모듈(`lib/inboxCache.ts`, `useSyncExternalStore`), 검색 필드. 조합 중 Enter 차단 방법(`nativeEvent.isComposing`)을 테스트 가능하게. 서버 `layout.tsx`에 붙일 클라이언트 경계 | IR-M6, SY9-M3 |
| A16 | **스켈레톤과 WakeGate.** 400ms(`wake.ts:6`) 뒤 무조건 WakeWait로 바뀌는 지금 구조에서 "깨어 있는데 느린 응답"을 스켈레톤으로 보일 판정 규칙(예: `wait == null`인 동안 fallback). `fallback` prop 타입, 화면별 모양, `motion-reduce:animate-none`. WakeGate 실패 화면 렉스 180px → 120px(`WakeGate.tsx:121`) | IR-M8, CD-M9, ZO-N4, ZO2-N4, SY9-N7 |
| A17 | **파일 목록을 쓰기 허용 목록으로 완성한다.** 추가: `package.json`, `pnpm-lock.yaml`, `app/runs/[id]/RunClient.tsx`, `AiTrail.tsx`, `Timeline.tsx`, `ReviewChat.tsx`, `ds/AiLabel.tsx`, `ui/ConfirmDialog.tsx`, `ui/Floating.tsx`, `ds/RedlineDiff.tsx`, `WakeWait.tsx`, `design-system/Showcase.tsx`, `lib/labels.test.ts`, `verify-design-css.test.mjs`, README와 스크린샷, 로고 SVG 위치. 117행을 파일별로 나누고 화면 이름을 붙인다. 경로 기준(`web/src`, scripts는 `web/`)과 명령 실행 위치. `cx`와 `cn`: 이름을 하나로 정하고, 기존 `cx` 호출 전체에 tailwind-merge가 걸리는 동작 변경이면 일괄 치환 단계와 회귀 테스트를 적는다. Button, Callout, badges의 현재 props 유지 여부. 118행(`/design-system`)의 실제 구역 목록 | IR-M13, CD-M11, ZO-N1, ZO2-N1, SY9-N4, SY9-N5, CO-8, CO-15 |
| A18 | **테스트 계획과 화면 확인 기준.** 케이스: `theme`(세 값, 모르는 값, 저장소 예외 → `"system"`), `shortcuts`(A5 표), `cn`(`tokens.css`의 글자 크기, 그림자, 모서리 이름 전부, 5절 170행과 범위 일치), `contrast`(두 테마, 키 집합), `verify-design-css.test`(통과/실패 예시 CSS), `labels`/출처 함수, 근거 연결, `checkState`, SplitPane clamp와 키. 4단계와 6단계, 5절 169행의 체크리스트(일곱 화면 목록, 두 테마, 1440/390px, macOS 한글 입력기로 조합 중 Enter와 글자 키, 첫 화면 깜박임, 읽을 수 없는 글자와 사라진 경계선) | IR-M14, SY9-M1, SY9-M14, SY9-M15, SY9-M16, SY9-N1, SY9-N15, CO-16 |
| A19 | **글꼴과 읽기 폭(DS2-04).** `"Noto Serif KR Fallback"` `@font-face`(v1 `Pretendard Fallback` 형식, `size-adjust`는 추정치로 표시하고 확인 단계 연결), import 경로 `@fontsource-variable/noto-serif-kr/wght.css`와 위치, 세리프 적용 범위(`text-display` 포함 여부, `@utility` 묶음은 만들지 않음), "한 줄 최대 40em" 토큰(지금 `--container-reading` 48rem), 읽기 글자 17px 사용처(조문, 알림 본문, 채팅 답), 척도 값 표 | ST-M8, CR-N4, IC-10, CD-N4, SY-N8, SY-N2 |
| A20 | **1절 결정 표를 자족적으로.** 토큰을 "유지 / 추가 / 삭제" 한 표로(`on-danger`, `--lw-row`, `--lw-shadow-*` 포함, 상태 토큰 이름 나열, `prov-*` 각 `-bg`, `brand-text`가 `text-brand-text`를 만드는 이름 문제). "관련 요구" 열과 DS2 요구 한 줄 요약. research 참조마다 한 줄 근거(next-themes: 배포 중단과 React 19 경고 이슈 #385, sonner: +9.4KB 대 +6.5KB, cmdk: Radix Dialog 의존, `@custom-variant`는 미확인 항목). 대안 표에 research 이름 채택, `light-dark()`, 쿠키 저장, 분할 라이브러리 행. 렉스, 역할, `WakeGate`, `AiTrail`은 정의 대신 v1 문서나 코드 위치를 한 줄로 가리킨다. 사실 정정: v1 출처 다섯 쌍은 회사 다섯이 아니라 NVIDIA, Cloudflare, Groq, 대체 구현, 재현. "40여 개" → 35개. 2.2 `neutral` 행은 "research에 없음"으로 분리. 18행 tailwind-merge 근거를 반대로 읽히지 않게 | CO-1, CO-2, CO-4, CO-11, CO-13, CO-14, ST-M1, ST-M2, ST-N1, ST-N2, SY-M1~SY-M7, SY-M9, SY-M13, CR-N1, CD-N1, CD-N2 |

### 🟢 Nit

| 조치 | 내용 | 원 지적 |
|---|---|---|
| A21 | 용어 통일(본문은 "밝은 테마 / 어두운 테마", "밝게/어둡게"는 설정 값에만), 16진수 대소문자와 선택자 표기 통일, 요구 ID 전체 표기, 빈 칸과 "같음", "등" 제거, 그림 범례, 평가형 표현("가장 짧은 길", "얻는 것이 없다") 정리 | SY-M12, SY-N3~SY-N6, SY9-N2, SY9-N6, SY9-N8~SY9-N10, SY9-N12~SY9-N14 |
| A22 | 문서 머리: 작성 줄, 상태를 검토 반영본으로 갱신하고 이 보고서 링크 | ST-N4, SY-N1 |

## 작성자에게 묻는 것

A12와 A13은 spec 수정이 따르거나 설계 범위를 바꾸므로 작성자가 정한다. 나머지는 설계만 고치면 된다. 검토자 권고는 A12는 "아래 고정 상자 유지", A13은 "밀도 이월, `lw_split` 삭제, 토스트는 재실행 하나"다.

## 검토자 간 이견과 판정

| 쟁점 | 의견 | 판정 |
|---|---|---|
| 어두운 테마 값 이중 기재 | IC-1은 `light-dark()`, ZO-M1과 ZO2-A1은 스크립트가 늘 `data-theme`을 적는 방식 | ZO2-A1(A7). `light-dark()`는 빌드 산출물이 미확인 |
| 명령 등록소 | IC-2는 등록소, ZO2-A2는 폐기 | 폐기(A5). 팔레트와 단축키가 거의 겹치지 않는다 |
| 포커스 링 | IC-6은 두 겹 링, ZO2-A4는 전역 offset 한 규칙 | 한 규칙(A8) |
| 운영 차트 | ZO-M4는 전부 `prov-ai`, CR-M2는 차트 토큰, ZO2-A6은 가로 막대 목록 | 가로 막대 목록(A10) |
| 테마 스크립트 삽입 | ZO-N2, IR-M10, ZO2-M7은 `useServerInsertedHTML`을 의심, CD-N5는 Next 문서가 허용함을 확인 | 문서 방식 하나를 고르고 조건을 적는다(A7) |
| `cx`와 `cn` | ZO-N1과 ZO2-N1은 이름 하나로 내부만 교체, CD-M11은 전체 호출의 동작 변경을 경고 | 이름 하나, 단 회귀 테스트를 단계에 넣는다(A17) |

## 추적표

| 원 지적 | 조치 |
|---|---|
| ZO-B1 | A2 |
| ZO-M1 | A7 |
| ZO-M2 | A13 |
| ZO-M3 | A9 |
| ZO-M4 | A10 |
| ZO-N1 | A17 |
| ZO-N2 | A7 |
| ZO-N3 | A5 |
| ZO-N4 | A16 |
| IC-1 | A7 |
| IC-2, IC-3 | A5 |
| IC-4 | A14 |
| IC-5, IC-6 | A8 (IC-5는 A6에도) |
| IC-7 | A13 |
| IC-8 | A9 |
| IC-9 | A12 |
| IC-10 | A19 |
| CO-1, CO-2, CO-4 | A20 |
| CO-3 | A2 |
| CO-5 | A5 |
| CO-6 | A11 |
| CO-7 | A13 |
| CO-8 | A17 |
| CO-9 | A9 |
| CO-10 | A14 |
| CO-11, CO-13, CO-14 | A20 |
| CO-12 | A7 |
| CO-15 | A17 |
| CO-16 | A18 |
| CR-B1 | A1 |
| CR-M1, CR-M3, CR-M6 | A7 |
| CR-M2 | A10 |
| CR-M4, CR-M5 | A8 |
| CR-N1 | A20 |
| CR-N2, CR-N3 | A9 |
| CR-N4 | A19 |
| CR-N5 | A8 |
| ST-B1 | A1 |
| ST-M1, ST-M2 | A20 |
| ST-M3, ST-M4 | A8 |
| ST-M5, ST-M7 | A7 |
| ST-M6 | A9 |
| ST-M8 | A19 |
| ST-N1, ST-N2 | A20 |
| ST-N3 | A8 |
| ST-N4 | A22 |
| SY-M1~SY-M7, SY-M9, SY-M13 | A20 |
| SY-M8 | A7 |
| SY-M10, SY-M11 | A8 |
| SY-M12 | A21 |
| SY-N1 | A22 |
| SY-N2, SY-N8 | A19 |
| SY-N3~SY-N6 | A21 |
| SY-N7 | A9 |
| ZO2-A1 | A7 |
| ZO2-A2, ZO2-A3 | A5 |
| ZO2-A4 | A8 |
| ZO2-A5 | A2 |
| ZO2-A6 | A10 |
| ZO2-A7 | A13 |
| ZO2-A8 | A8, A9, A14 |
| ZO2-M1 | A12 |
| ZO2-M2, ZO2-M4, ZO2-M5, ZO2-M6 | A13 |
| ZO2-M3 | A11 |
| ZO2-M7 | A7 |
| ZO2-N1 | A17 |
| ZO2-N2 | A5 |
| ZO2-N3 | A3 |
| ZO2-N4 | A16 |
| ZO2-N5 | A8 |
| ZO2-N6 | A2 |
| IR-B1 | A3 |
| IR-B2 | A4 |
| IR-B3 | A5 |
| IR-M1 | A2, A10 |
| IR-M2 | A2 |
| IR-M3 | A11 |
| IR-M4 | A12 |
| IR-M5 | A5 |
| IR-M6 | A15 |
| IR-M7, IR-M9 | A13 |
| IR-M8 | A16 |
| IR-M10, IR-M11 | A7 |
| IR-M12 | A14 |
| IR-M13 | A17 |
| IR-M14 | A18 (DS2-03 소스 검사는 A8) |
| IR-N1 | A13 |
| IR-N2 | A6 |
| IR-N3 | A2 |
| IR-N4 | A3 |
| IR-N5 | A4 |
| CD-B1, CD-B4 | A1 |
| CD-B2 | A6 |
| CD-B3 | A3 |
| CD-M1, CD-M2 | A2 |
| CD-M3 | A10 |
| CD-M4 | A13 |
| CD-M5 | A5 |
| CD-M6 | A9 |
| CD-M7 | A8 |
| CD-M8 | A14 |
| CD-M9 | A16 |
| CD-M10 | A12 |
| CD-M11 | A17 |
| CD-N1, CD-N2 | A20 |
| CD-N3 | A9 |
| CD-N4 | A19 |
| CD-N5 | A7 |
| CD-N6 | A13 |
| CD-N7 | A5 |
| SY9-M1, SY9-M14~SY9-M16 | A18 |
| SY9-M2, SY9-M13 | A5 |
| SY9-M3 | A15 |
| SY9-M4, SY9-M7 | A13 |
| SY9-M5, SY9-M17 | A4 |
| SY9-M6 | A14 |
| SY9-M8~SY9-M11 | A2 |
| SY9-M12 | A3 |
| SY9-N1, SY9-N15 | A18 |
| SY9-N2, SY9-N6, SY9-N8~SY9-N10, SY9-N12~SY9-N14 | A21 |
| SY9-N3 | A3 |
| SY9-N4, SY9-N5 | A17 |
| SY9-N7 | A16 |
| SY9-N11 | A12 |
