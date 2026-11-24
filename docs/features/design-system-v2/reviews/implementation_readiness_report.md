# Stage 9: Implementation Readiness 보고서

- 대상: `docs/features/design-system-v2/design.md` DetailedRange 84–173행(3절 부품, 4절 단계와 검증, 5절 위험). 1–83행의 결정과 토큰도 함께 대조
- 기준: egm-implementation-readiness와 egm-codebase-details는 `implementation-readiness-standard.md`, egm-style은 표준 없음
- 검토자: egm-implementation-readiness, egm-codebase-details, egm-style (각각 새 문맥, 2026-11-25)
- 기준 자료: spec.md, research.md, v1 spec.md와 design.md, `web/src`, `web/scripts`, `web/package.json`, `node_modules/next/dist/docs`

## egm-implementation-readiness

판정: **구현 불가(not ready).** Blocker 3, Major 14, Nit 5.

### Blocker

| ID | 범위 | 지적 | 고칠 내용 |
|---|---|---|---|
| IR-B1 | 105, 137–148행 | 근거 패널의 "선택한 근거 본문 또는 신구조문"을 지금 API로 그릴 수 없다. `EvidenceItem`에는 `excerpt`(최대 240자)만 있고 본문 전체가 없다(`ReviewEndpoints.cs:98, 441`). `OldNew`에는 근거 id가 없어(`api.ts:41-51`) 칩 id로 신구조문을 찾는 규칙을 구현자가 지어내야 한다 | 비`oldnew` 근거는 제목, excerpt, 원문 링크만. `oldnew`는 `id.split(":", 3)[2] === oldNew.articleNo`(백엔드 `ToOldNew`와 같은 규칙)를 `lib/` 순수 함수와 테스트로. 1280px 이상에서 아래 "신구조문 비교" 구역을 없애는지, 인용되지 않은 조문은 어디서 보는지, `selected === null`일 때 패널 내용, "강조" 모양 |
| IR-B2 | 104, 131–148, 172행 | `app/layout.tsx:20` `<main>`이 `max-w-workspace`(70rem = 1120px)라 1280px 이상에서도 두 칸이 1120px에 갇힌다. 왼쪽을 읽기 폭 48rem로 두면 오른쪽은 약 22rem. 헤더 높이는 안내 띠 `h-7`과 줄바꿈 메뉴 때문에 고정이 아니라 `100dvh - 헤더`를 계산할 수 없다 | 검토 상세가 workspace 폭을 벗어나는 방법(경로별 main 클래스 또는 풀블리드 래퍼). 헤더 높이 CSS 변수(`--lw-header-h`) 고정 또는 측정. SplitPane 기본 비율, 최소·최대, 화살표 한 번 이동량, Home/End, `aria-valuenow/min/max`, `lw_split` 형식(%/px), 잘못된 값 처리 |
| IR-B3 | 95–97, 150–152, 171행 | 한국어 입력기가 켜져 있으면 `j`가 `e.key === "ㅓ"`나 `"Process"`로 온다. `e.key`로 판정하면 주 사용자에게 `j/k/a/e/r`이 동작하지 않는다. `e.key`와 `e.code` 중 무엇인지 없다. spec 5절 "수정자 없는 글자 키는 본문에 포커스가 있을 때만"이 3.4에 없다 | 글자 단축키는 `e.code`(`KeyJ` 등), `?`와 `[ ]` 판정 방법도. 포커스 허용 범위(body, main, 행 링크, 버튼). 대화상자 판정 선택자 `closest('[role=dialog],[role=alertdialog]')`. 모두 `shortcuts.test.ts` 표 테스트로 |

### Major

| ID | 범위 | 지적 | 고칠 내용 |
|---|---|---|---|
| IR-M1 | 117행, 14행 | `ops/page.tsx:14-17`이 `bg-source-*`를 쓰고 Groq는 `bg-info`. `--color-*: initial`이라 지우면 막대가 투명해진다. `AiLabel.tsx`(목록에 없음)가 AI를 `info`로 칠해 DS2-05 위반 | 공급자 차트 색 규칙. AiLabel을 `prov-ai`로, 파일 목록에. `source-*` 삭제와 사용처 수정을 같은 단계에 |
| IR-M2 | 109, 119, 121–129행 | `SourceLabel`은 `kind: providerKind`만 받는다. 사람 결정은 `approval.actorRole`, AiTrail `actor: "human"`과 `detail.role`에서 온다. Timeline, AiTrail, ReviewChat, governance가 목록에 없다. 125행 "'AI · NVIDIA' 대신 'AI NVIDIA'" 모호. fallback 라벨을 "대체 구현"에서 "규칙"으로 바꾸는지 없다 | props 타입(`{ kind: ProviderKind \| "human"; role?: Role }`), `provenance(kind) → { kind: "ai"\|"rule"\|"human"\|"replay"\|"unknown", label }`, 모든 호출처, 보이는 글자와 sr-only 문구와 아이콘 |
| IR-M3 | 103, 137–140행 | `findings.length`만으로 판정하면 크리틱이 안 끝났거나(Analyzing, critic 단계 `running/not_run/failed`) 수정 중이라 적발이 낡아도 "검사 통과"가 뜬다. spec이 막으려는 혼동이 그대로 생긴다 | `checkState(sentence, trail, editing) → "passed"\|"flagged"\|"unchecked"` 순수 함수와 판정 표. CheckMark props, 상단 요약 자리(지금 Callout 대체 여부) |
| IR-M4 | 136, 146행 | 아래 고정 상자에는 버튼 외에 재검사 적발 Callout, 반려와 돌려보내기 사유 textarea, 파트너 "최종 승인/돌려보내기", notice가 있다 | 헤더로 올린 뒤 각 요소 위치, sticky 여부, 헤더가 두 칸 위 전체 폭인지 왼쪽 칸 안인지, 1280px 경계에서 상태 공유 방식 |
| IR-M5 | 116, 152행 | `ConfirmDialog`는 트리거로만 열린다(`ConfirmDialog.tsx:20-37`). `a`로 열려면 `open/onOpenChange`가 필요한데 목록에 없다 | 수정 중 `a`, 재검사 적발이 남은 `a`, 파트너 AwaitingPartner의 `a`, `r`의 사유 입력과 포커스, `e`의 토글, `]/[` 순환과 스크롤, `j/k`의 현재 행(포커스인지 강조 인덱스인지, 파트너 `mine/rest` 두 목록)을 표로 |
| IR-M6 | 98, 114행 | "대기함을 한 번 받은 결과"를 공유할 모듈이 없다. 테마 상태를 헤더 메뉴와 팔레트가 함께 쓸 저장소가 없다 | `lib/inboxCache.ts`(`useSyncExternalStore`). 항목 정확한 문구, 검색 필드(`lawName`, `groupName`), 조합 중 Enter 차단(`nativeEvent.isComposing`이면 `preventBaseUIHandler`)을 테스트 가능하게. 서버 layout에 붙일 클라이언트 경계 |
| IR-M7 | 99, 117행 | 재실행은 `app/runs/[id]/RunClient.tsx:23`에 있는데 목록에 없고 목록의 `runs/page.tsx`에는 재실행이 없다. "링크 복사" 기능은 코드에 없다 | `useToast()` 시그니처, timeout 5000, limit 3, `aria-live`, Viewport 위치. `retryRun` 결과(`ok`, `nothing`, `forbidden`, `error`)별 토스트/Callout. 링크 복사를 만들지 뺄지 |
| IR-M8 | 101, 113, 115행 | WakeGate는 400ms(`SHOW_DELAY_MS`) 뒤 WakeWait를 띄운다. 깨어 있는 로딩과 잠든 서버를 언제 무엇으로 가르는지 없다. 검토 상세, 레이더, 운영 현황 스켈레톤이 목록에 없다. `motion-reduce` 처리 없음 | `fallback` prop 타입과 전환 상태도, 화면별 스켈레톤 모양, WakeGate 오류 화면 Mascot 180px |
| IR-M9 | 100, 115, 117행 | 행 높이 값(research 48/40/32px) 없음. `--lw-row`를 읽는 요소 없음. 깜박임 방지 없음. DS2-15 "표 행 cva" 부품 없음. 117행이 DS2-12 대상 아닌 radar와 inbox에 "밀도"를 적었다. 대기함은 `app/page.tsx` | 값, 적용 요소, 인라인 스크립트로 `data-density`, 표 행 부품, 대상 화면 정정, 전환 위치 |
| IR-M10 | 92–94, 114행 | `applyTheme("system")`이 `data-theme`을 지우는지 없다. `color-scheme: light dark` 고정 문제. `useServerInsertedHTML`이 Next 16(cacheComponents)에서 확인되지 않았다. 테마 메뉴 부품, 탭 간 동기화(storage 이벤트) 없음 | 각각 명시. 스크립트 삽입 방식은 `node_modules/next/dist/docs` 기준으로 하나 |
| IR-M11 | 118행 | 어둡게 값이 `:root[data-theme="dark"]`에만 걸려 `/design-system`에서 두 테마 견본을 한 페이지에 동시에 그릴 수 없다 | 선택자를 `[data-theme="dark"]`(`:root` 한정 없이)로 바꿀지 결정(2.1, 2.3 파서, 2.4 검사에 영향). 화면 구역 목록(Showcase.tsx 포함) |
| IR-M12 | 110행 | research 7절은 `PreviewCard`를 권한다. 설계가 PreviewCard인지 Popover `openOnHover`인지 안 골랐다 | 구조, 열림·닫힘 지연, Esc 뒤 포커스 복귀, 분할에서 클릭은 `onPin`만인지, ReviewChat 칩에도 적용하는지 |
| IR-M13 | 88–119행 | 파일 목록이 쓰기 허용 목록으로 불완전하다. 빠진 것: `package.json`, `pnpm-lock.yaml`, `runs/[id]/RunClient.tsx`, `AiTrail.tsx`, `Timeline.tsx`, `ReviewChat.tsx`, `ds/AiLabel.tsx`, `ui/ConfirmDialog.tsx`, `ds/RedlineDiff.tsx`, `WakeWait.tsx`, `design-system/Showcase.tsx`, `lib/labels.test.ts`, `verify-design-css.test.mjs`, README와 스크린샷, 로고 SVG 위치. DS2-04 읽기 글자 사용처와 40em(지금 48rem) 없음. `cx` 처리 범위 모호. Button, Callout, badges의 현재 props 유지 여부 없음 | 목록 보완과 각 결정 |
| IR-M14 | 154–165행 | 테스트 계획이 명령어뿐이다. DS2-03 소스 검사 없음. 4단계 "로컬 화면 확인"과 6단계 체크리스트 없음 | `theme`, `shortcuts`, `cn`(모든 `--text-*`, 그림자, 모서리), `contrast`(두 테마와 블록 동일성), `verify-design-css.test`(통과/실패 예시 CSS, 42행 금지와 32행 `color-scheme:light` 삭제), `labels`, 근거 연결, `checkState`, SplitPane clamp와 키, 밀도 파싱. 화면 체크리스트(두 테마, 1440/390px, 한글 입력기, 첫 화면 깜박임) |

### Nit

| ID | 범위 | 지적 |
|---|---|---|
| IR-N1 | 165행 | "분할 폭 끌기를 고정 비율로 대신"은 DS2-07 수용 기준을 어긴다. 뺀다면 spec도 함께 바꾼다고 적는다 |
| IR-N2 | 158–159행 | 단계 1에 `on-danger`를 넣고 Button은 단계 2에서 고쳐, 사이에는 어둡게 위험 버튼이 대비 미달이다. 의도라면 적는다 |
| IR-N3 | 129행 | "규칙 색 없이 원문 회색"을 "모르는 값은 `neutral`"로 |
| IR-N4 | 148행 | `selected` 초기값과 수정 중 선택 동작 |
| IR-N5 | 172행 | 오른쪽 칸 아래 footer(`layout.tsx:23`)가 페이지 스크롤을 만든다. 검토 상세에서 footer 처리 |

### DS2 요구사항별 연결

| 요구 | 상태 |
|---|---|
| DS2-01 | 부분(IR-M10). 깜박임 확인 없음 |
| DS2-02 | 연결됨 |
| DS2-03 | 부분. 소스 검사 없음(IR-M14) |
| DS2-04 | 부분(IR-M13) |
| DS2-05 | 부분(IR-M1, IR-M2) |
| DS2-06 | 부분(IR-M3) |
| DS2-07 | 연결 안 됨(IR-B1, IR-B2, IR-M4) |
| DS2-08 | 부분(IR-M12) |
| DS2-09 | 연결 안 됨(IR-B3, IR-M5) |
| DS2-10 | 부분(IR-M6) |
| DS2-11 | 부분(IR-M7) |
| DS2-12 | 부분(IR-M9) |
| DS2-13 | 부분(IR-M8) |
| DS2-14 | 부분. WakeGate 오류 화면 180px |
| DS2-15 | 부분. 표 행 부품 없음(IR-M9) |
| DS2-16 | 부분(IR-M11) |

백엔드 데이터를 따로 요구하는 곳은 IR-B1의 "근거 본문"뿐이다. 설계에서 excerpt로 한정하면 범위 안에 머문다.

## egm-codebase-details

판정: **구현 준비 미달.** Blocker 4, Major 11, Nit 7.

확인된 사실(문제 없음): `@base-ui/react@1.9.0`에 toast, autocomplete, dialog, preview-card가 모두 있다. `parseColorTokens`, `CONTRAST_PAIRS`, `lib/cx.ts`, `SourceLabel`, `CitationChip`(Popover 사용), `AiTrail`, `WakeGate`, `RunTimeline`, `PageHeader`, `EmptyState`(현재 180px)가 있다. `useServerInsertedHTML`로 테마 스크립트를 넣는 방식은 Next 문서(`next/dist/docs/01-app/02-guides/preventing-flash-before-hydration.md`)가 허용한다. research 4.4 대비 쌍은 정확히 47쌍이다.

### Blocker

| ID | 범위 | 지적 | 고칠 내용 |
|---|---|---|---|
| CD-B1 | 74–78행 | 77행 `var(--color-surface)` 기대값은 `@theme inline`과 모순된다(실제 `var(--lw-surface)`). `verify-design-css.mjs:24-31` 규칙도 모두 실패한다. 반드시 실패할 기존 검사: 14–21행 COLORS 루프(`--color-X: #hex`가 없어 18행 `expected.toLowerCase()` TypeError), 41행 `--text-title-lg:1.5rem`(research 2.3은 1.625rem), 42행 `prefers-color-scheme:dark` 금지, 32행 `color-scheme:light`. `verify-design-css.test.mjs:13-15` 고정값도 v1 형태 | 77행을 `var(--lw-surface)`로. 지울 검사 명시(COLORS 루프, 32·41·42행). `--background`/Arial 감지는 유지. 새 기대값 `--text-title-lg:1.625rem`, `color-scheme:light dark`, 압축 CSS의 `[data-theme=dark]`. test.mjs 새 고정값 |
| CD-B2 | 156–162행 | 단계 1이 빌드 가능한 상태로 끝나지 않는다. `design-system/page.tsx:4,12`가 `parseColorTokens`를 import해 tsc 실패. `contrast.test.ts:4,12,40`이 `SOURCE_TOKENS`와 `tokens["source-nvidia-bg"]`를 쓴다. `badges.tsx:32-56`, `ops/page.tsx:15-16`의 `bg-source-*`는 색이 조용히 빠진다. `design-system/page.tsx:19` `/^source-/` 묶음이 빈다 | 단계 1에 `design-system/page.tsx` 최소 수정(`parseThemes().light`), 색상각 검사를 `PROV_TOKENS`와 두 테마로, `badges.tsx`와 `ops/page.tsx` 교체를 단계 1로 당기거나 `source-*` 삭제를 단계 2로 |
| CD-B3 | 105, 139, 148행 | IR-B1과 같음. `EvidenceItem`은 `{id, kind, title, sourceUrl, excerpt?}`(`api.ts:53-60`), 근거 id 예 `law:005698:제7조`(`Showcase.tsx:13`), 신구조문 `articleNo: "제7조"` | "본문"은 excerpt(240자)로 명시. `kind === "law"`이고 `id.split(":").at(-1) === oldNew.articleNo`일 때만 신구조문. 다른 법령의 같은 조 번호 처리 |
| CD-B4 | 37–50, 77행 | 화면 코드가 직접 쓰는 `var(--color-*)`: `RunTimeline.tsx:20`(`--color-danger`, `--color-danger-bg`), `RunTimeline.tsx:128`(`--color-line`). inline 테마에서 빌드 CSS에 이 변수가 남는다는 보장이 없다 | `var(--lw-*)`로 바꾸고 RunTimeline 행에 적거나, `--color-*` 출력을 빌드 검사로 확인 |

### Major

| ID | 범위 | 지적 | 고칠 내용 |
|---|---|---|---|
| CD-M1 | 109, 117, 119행, 3.2 | `AiTrail.tsx:8-12`, `RunTimeline.tsx:12-16, 106, 109, 169`는 AI를 `info`, 사람을 `success`로 칠한다. `ds/AiLabel.tsx:18,27`도 `info`. 재사용 가능: `TrailActor`(`api.ts:80`), `ACTOR_LABEL`(`aiTrail.ts:16`), `PROVIDERS`(`aiTrail.ts:95-`), `stepActor`(`runTimeline.ts:24`) | 행위자 네 종류 → `prov-*` 대응을 cva 한 곳에. 세 파일을 "고침"에 |
| CD-M2 | 121–129행 | 출처 대응은 `labels.ts`가 아니라 `ds/badges.tsx:28-59` `SOURCES`에 있다. `ProviderKind`에 `"none"`이 있고 지금은 그리지 않는다(`badges.tsx:66`). `isAiProvider("replay") === true`(`aiTrail.ts:152-153`, `aiTrail.test.ts:86`)가 "replay는 재현"과 충돌. 사람 판정 입력 없음. "AI NVIDIA" 이름 출처(`PROVIDERS`의 "NVIDIA Nemotron"인지 badges label인지) | 표가 놓일 파일, `none` 행, `isAiProvider` 수정 여부, 사람 입력 소스 |
| CD-M3 | 117행 | IR-M1과 같음(ops 공급자 막대) | 무늬, 명도 단계, 글 라벨 중 결정 |
| CD-M4 | 117행 | IR-M7과 같음. `RetryOutcome`에는 `waking`도 있다. `clipboard` 검색 0건 | 결과별 토스트/Callout, 링크 복사 위치 |
| CD-M5 | 3.1 | IR-M5와 같음(`ConfirmDialog.tsx:21,36-37`) | `open`/`onOpenChange` 추가 명시 |
| CD-M6 | 3.1 | `ConfirmDialog.tsx:39` `bg-ink/40`은 어둡게(`ink` `#ECEDEF`)에서 밝은 막. `Floating.tsx:35` 툴팁 `bg-ink text-on-brand`는 쌍이 없다. `globals.css:44-48` `.lw-stripes` 흰색 rgba | 배경막 토큰 또는 고정 검정 rgba, 툴팁은 `surface-raised`와 `ink` 또는 쌍 추가. 두 파일 목록에 |
| CD-M7 | 70행 | Callout 본문은 `*-bg` 위 `text-ink`(`Callout.tsx:39`), `text-ink-muted`도 쓴다. 47쌍에 `ink`/`*-bg`가 없다. `contrast.test.ts:37-46` 색상각 검사 이관 없음. `neutral/neutral-bg`(`tokens.ts:59`) 빠지는지 없음 | 쌍 추가와 이관 명시 |
| CD-M8 | 110행 | IR-M12와 같음. 미리보기 카드의 출처 라벨(research P2) 없음 | |
| CD-M9 | 113행 | WakeGate는 응답 전엔 깨어 있는지 모른다. 400ms(`wake.ts:6`) 뒤 무조건 WakeWait(`WakeGate.tsx:46-49,131`), 그 전엔 같은 높이 빈 자리(`WakeWait.tsx:37`). 깨어 있는데 느린 응답이 렉스 화면이 된다 | 판정 규칙(예: `wait == null`인 동안 fallback, `/health` up이면 스켈레톤 복귀) |
| CD-M10 | 146–147행 | IR-M4와 같음(`ReviewClient.tsx:184-303`: 재검사 Callout과 "그대로 승인" 186–211, 반려 사유 251–267, 파트너 상자 270–303). 147행 "AiTrail은 헤더 아래 한 줄(지금과 같음)"은 사실과 다르다. 지금은 확인 안내 Callout 아래(111행) | 위치 결정, 147행 정정 |
| CD-M11 | 90행 | `cx`가 `cn`을 다시 내보내면 기존 `cx` 호출 전체(badges, Button, AppHeader, RoleSwitch, RunTimeline, AiTrail, CitationChip, ops 등)에 tailwind-merge가 걸리는 동작 변경이다. 영향 검토 없음 | `cx`는 그대로 두고 cva 부품만 `cn`, 또는 일괄 치환 단계와 회귀 테스트 |

### Nit

| ID | 범위 | 지적 |
|---|---|---|
| CD-N1 | 12행 | 색과 글자 클래스를 쓰는 tsx는 35개(374건/35파일). "40여 개" → "35개" |
| CD-N2 | 13행 | 목록에 `on-danger`, `--lw-row`, `--lw-shadow-*` 누락 |
| CD-N3 | 50행 | 그림자는 `--shadow-card: var(--lw-shadow-card)` 형태 명시 |
| CD-N4 | 82행 | `"Noto Serif KR Fallback"` `@font-face`(size-adjust, research 142행)와 import 경로 `@fontsource-variable/noto-serif-kr/wght.css` |
| CD-N5 | 94, 100행 | Next 문서 요구 두 가지: `useServerInsertedHTML`을 `useRef` 플래그로 한 번만, 개발 Strict Mode에서 속성이 지워지므로 `useLayoutEffect`로 재적용. `data-density`도 같은 스크립트로 |
| CD-N6 | 3.1 | DS2-15의 "표 행"과 "Badge" 변형 위치가 없다. 지금 Badge 부품은 `CountBadge`뿐 |
| CD-N7 | 152행 | 대화상자 판정 선택자 명시 |

## egm-style

전체 판단: 구조와 결정은 읽히지만 빈 칸, "같음", "등", 약칭이 남고 수치(분할 비율, 행 높이, 헤더 높이)와 판정 조건이 빠져 구현자가 추측할 곳이 10곳 넘는다. Blocker 없음. Major 17, Nit 15.

| ID | 등급 | 범위 | 지적 | 수정안 |
|---|---|---|---|---|
| SY9-M1 | Major | 91, 170행 | `cn.test.ts` "등"으로 열려 있고 90행(글자 크기, 그림자, 모서리)과 170행(글자 크기만)의 범위가 다르다 | "글자 크기, 그림자, 모서리 토큰 이름을 모두 읽어 색 클래스와 함께 `cn()`에 넣어도 남는지 검사". 170행 맞춤 |
| SY9-M2 | Major | 96행 | `shortcuts.test.ts` 내용 칸 빈 칸 | 152행 거짓 조건마다 `false`, `⌘K`/`Ctrl+K`와 `Shift+?`는 `true` |
| SY9-M3 | Major | 98행 | 팔레트 "보기"가 무엇인지 없다 | "이동(화면), 검토(법령명 검색), 역할(전환), 보기(테마, 밀도)" |
| SY9-M4 | Major | 100행 | 밀도 값과 단위 없음 | `comfortable`/`default`/`compact`, 각 `--lw-row` px, 잘못된 값은 `default` |
| SY9-M5 | Major | 104, 165행 | SplitPane 기본 비율, 최소·최대, 키 이동량, 저장 단위, "고정 비율" 값 없음 | 수치 명시 |
| SY9-M6 | Major | 110행 | 칩 동작을 무엇이 고르는지 없다. 포커스 미리보기(DS2-08) 누락 | "`onPin`이 주어지면 `onPin(citationId)`, 없으면 팝오버" |
| SY9-M7 | Major | 117행 | 여섯 파일에 "토큰과 밀도, 재실행 토스트"를 한꺼번에. `page.tsx`와 `inbox/page.tsx` 중 어느 것이 "대기함"인지 불명확 | 행 분리, 파일 옆 화면 이름 |
| SY9-M8 | Major | 125행 | 버린 형태와 쓸 형태가 한 칸 | "`AI NVIDIA`, `AI Cloudflare`, `AI Groq`(가운뎃점 없음)" |
| SY9-M9 | Major | 126행 | "가드레일 거절도 여기" 판정 기준 없음 | 어느 필드와 값인지 |
| SY9-M10 | Major | 128행 | API 열에 API 값이 없고 반려 제외 이유, 변호사/파트너 선택 규칙 없음 | 실제 필드와 역할 대응 |
| SY9-M11 | Major | 129행 | 두 가지로 읽힘, "v1 DS-05와 같은 원칙" 내용 없음 | 종류 "없음(중립)", 라벨 "API 원문 값을 `neutral`로" |
| SY9-M12 | Major | 148행 | `null`일 때 패널 내용 없음. spec은 `]/[`가 확인 필요 문장으로 가는데 모든 문장처럼 읽힘 | "다음/이전 확인 필요 문장(없으면 그대로)", `null`이면 "문장을 고르면 근거가 보여요" |
| SY9-M13 | Major | 152행 | "거짓이면 무시:" 뒤가 거짓이 되는 조건이라 논리가 뒤집혀 읽힌다. Shift 처리, "표"가 무엇인지, `e`·`r`도 같은지 없다 | "하나라도 맞으면 `false`… Shift는 수정자로 보지 않는다. 할 수 없는 동작(`a`, `e`, `r`)은 키 표와 도움말에서 빠지고 눌러도 아무 일 없음" |
| SY9-M14 | Major | 161, 163행 | "로컬 화면 확인", "브라우저 확인"의 통과 기준 없음 | 확인 항목 명시 |
| SY9-M15 | Major | 169행 | "일곱 화면" 목록과 확인 항목 없음 | 목록과 "읽을 수 없는 글자, 사라진 경계선, 상태 색 뒤바뀜 없음" |
| SY9-M16 | Major | 170행 | "조용히 사라진다"의 원인 없음 | "`text-ui`를 색 클래스로 판단해 `text-ink`와 충돌한다고 보고 지운다. 오류는 나지 않는다" |
| SY9-M17 | Major | 172행 | 헤더 높이 값과 단위 없음. 위험보다 설계 결정 | 3.3으로 옮겨 `calc(100dvh - var(--lw-header-h))`, 두 칸 `overflow-y: auto` |
| SY9-N1 | Nit | 93행 | `theme.test.ts` 기대 결과 없음 | "`readTheme()`가 `"system"`" |
| SY9-N2 | Nit | 94행 | "`<head>`에 스크립트" | "`lib/theme.ts`의 인라인 스크립트 문자열을 `<head>`에" |
| SY9-N3 | Nit | 105행 | 신구조문이 언제 나오는지 | 조건 명시 |
| SY9-N4 | Nit | 106, 108, 118행 | 내용 칸이 절 번호나 ID뿐, 118행은 두 칸 모두 DS2-16 | 실제 내용 |
| SY9-N5 | Nit | 109행 | `SourceLabel` 문구. DS2-15의 StatusLozenge와 표 행 cva가 3.1에 없다 | 확인 |
| SY9-N6 | Nit | 112행 | "렉스 최대 120px"이 너비인지 높이인지 | 명시 |
| SY9-N7 | Nit | 113행 | "깨어 있는 로딩" | "서버가 깨어 있을 때의 로딩" |
| SY9-N8 | Nit | 114–117행 | 요구 ID 축약 표기("DS2-01, 04", "DS2-06~09") | 전체 표기 |
| SY9-N9 | Nit | 119행 | "출처 종류"와 "종류" 혼용 | "`providerKind` 값 → 네 출처 종류(3.2절)" |
| SY9-N10 | Nit | 137–139행 | 그림의 `◯`, `!`, `║`, "다음" 범례 없음 | 범례 |
| SY9-N11 | Nit | 147행 | "(지금과 같음)" | "(변경 없음)". 단 CD-M10에 따르면 사실과 다르다 |
| SY9-N12 | Nit | 158행 | "의존성 고정 설치" | 패키지와 버전 나열 |
| SY9-N13 | Nit | 159–162행 | "같음" | "1단계와 같은 명령" |
| SY9-N14 | Nit | 162행 | "렉스 축소"가 1.1 표현과 다름 | "렉스를 빈 화면과 대기 화면으로 제한(DS2-14)" |
| SY9-N15 | Nit | 171행 | 확인 시점과 통과 기준 | "단계 3에서 macOS 한글 입력기로 조합 중 Enter와 `j`/`k`/`a`가 동작하지 않음 확인" |

## 검토자 간 이견

- `useServerInsertedHTML` 사용 가능 여부: egm-zoom-out(ZO-N2)과 egm-implementation-readiness(IR-M10)는 의심했고, egm-codebase-details는 `node_modules/next/dist/docs/01-app/02-guides/preventing-flash-before-hydration.md`가 이 방식을 허용함을 확인했다(CD-N5에 문서가 요구하는 두 조건). 통합 보고서는 문서를 확인한 쪽을 따른다.
