# Stage 7: Critic Review 보고서

- 대상: `docs/features/design-system-v2/design.md` FeatureRange 1–83행(1절 결정과 대안, 2절 토큰)
- 기준: egm-design과 egm-structural은 `feature-spec-standard.md`, egm-style은 표준 없음
- 검토자: egm-design, egm-structural, egm-style (각각 새 문맥, 2026-11-25)
- 기준 자료: spec.md, research.md, v1 spec.md와 design.md, `web/src`, `web/scripts`

중복: CR-B1과 ST-B1은 같은 결함(2.4 빌드 검사의 `var(--color-*)` 기대값)을 각각 찾았다. 아래에는 둘 다 남기고 통합 보고서에서 하나로 묶는다.

## egm-design

전체 판단: 방향은 타당하지만 수정 후 재검토가 필요하다. Blocker 1, Major 6, Nit 5. Tailwind 동작은 빌드를 돌리지 않고 v4 문서에 근거해 판단했다.

| ID | 등급 | 범위 | 지적 | 제안 |
|---|---|---|---|---|
| CR-B1 | Blocker | 77행 | `@theme inline`은 유틸리티에 값을 그대로 넣는다. 빌드 결과는 `.bg-surface{background-color:var(--lw-surface)}`이고 77행이 요구하는 `var(--color-surface)`는 나오지 않는다. 지금 스크립트의 `:focus-visible`(`var(--color-focus)`)과 `body`(`var(--color-canvas)`) 검사도 실패한다. 스크립트는 소스에서 `--color-X: #hex`를 정규식으로 찾는데(`verify-design-css.mjs:16`) v2 소스에는 그 줄이 없어 `expected.toLowerCase()`에서 TypeError가 난다. 41행 `--text-title-lg:1.5rem`도 새 척도(26px)에서 실패한다 | 2.4를 "클래스는 `var(--lw-*)`를 참조한다"로. 검사 대상(bg-surface, bg-brand, text-ink, text-danger, text-warning, focus, body)을 `--lw-*` 기준으로 다시 적고, 기대값은 `parseThemes()` 결과에서 읽는다. 42행의 `prefers-color-scheme:dark` 금지 규칙과 41행 제목 크기 값의 삭제 또는 변경을 목록에 |
| CR-M1 | Major | 50, 70행 | 밝게와 어둡게의 `--lw-*` 키 집합이 같은지 보지 않는다. 어둡게 블록에 빠진 토큰은 밝은 값을 상속해 짙은 글자가 짙은 바탕에 놓인다. 대비 테스트는 쌍에 든 토큰만 본다 | 2.3에 단위 테스트: (a) 세 블록 키 집합 동일, (b) dark와 media-dark 값 동일, (c) `@theme inline`의 모든 `--color-*`가 가리키는 `--lw-*`가 세 블록에 있음 |
| CR-M2 | Major | 14, 63행 | `app/ops/page.tsx:14-18`이 공급자별 누적 막대를 `bg-source-nvidia`, `bg-source-cloudflare`, `bg-info`로 칠한다. 막대에는 글자가 없어 "회사는 라벨이 말한다"가 통하지 않는다. `--color-*: initial` 때문에 지운 클래스는 조용히 투명해지고 `palette.test.ts`는 못 잡는다. groq의 `bg-info`는 이미 DS-03 위반 | 차트 처리 결정(전용 토큰 또는 패턴과 범례)을 1.1에. `palette.test.ts`에 `tokens.css`에 없는 의미 색 이름 금지 |
| CR-M3 | Major | 78행 | `html { color-scheme: light dark }` 고정이면 OS 다크에서 "밝게"를 골라도 스크롤바, 입력창, select가 어둡게 그려진다 | `:root[data-theme="light"]{color-scheme:light}`, `:root[data-theme="dark"]{color-scheme:dark}`를 더하고 2.4 검사에도 |
| CR-M4 | Major | 70행 | research 47쌍으로 바꾸면 v1보다 검사가 준다. (1) `text-brand`가 글자로 쓰이는데(`AppHeader.tsx:53`, `FirstVisitGuide.tsx:16`, `WakeWait.tsx:217`) research는 `accent`를 3:1(면)로만 본다. `brand-text`로 옮긴다는 말이 없다. (2) `neutral`/`neutral-bg` 쌍이 없다(`badges.tsx`, `ReviewChat.tsx`가 씀). (3) `Floating.tsx:35` 툴팁 `bg-ink text-on-brand` 쌍이 없다. (4) 어둡게 `surface-raised` 위 상태와 출처 글자가 없다. (5) `neutral`을 `var()` 별칭으로 두면 16진수 전용 파서(`tokens.ts:9`)가 깨진다 | 쌍을 사용처 기준으로: 상태, 출처, `brand-text` 글자 × 네 바탕, `neutral`/`neutral-bg`, 툴팁 쌍, `text-brand` 사용처 이전 명시. 별칭 처리 방식 결정 |
| CR-M5 | Major | 70행 | 포커스 쌍을 빼는 근거인 "2px 띄움"을 강제하는 장치가 없다. 링 옆 바탕은 Callout `*-bg`, `accent-subtle`, `prov-*-bg`일 수도 있다. "포커스와 `brand` 쌍"은 한 쌍인지 두 쌍인지 모호하다 | "focus/brand 한 쌍만 뺀다". 빌드 검사에 `outline-offset:2px`, `palette.test.ts`에 `outline-offset-0`, `ring-inset`, `focus:ring` 금지. focus 쌍 바탕에 `accent-subtle`과 `*-bg` 추가 |
| CR-M6 | Major | 23–31, 50행 | 어둡게 값 이중 기재에 대안 검토가 없다. `light-dark()`와 `color-scheme` 전환, 또는 스크립트가 늘 `data-theme`을 넣는 방식 | 1.2에 두 대안과 버린 이유(색 아닌 값, 파서 비용, 브라우저 하한). 채택하면 2.1, 2.3, 2.4 수정 |
| CR-N1 | Nit | 12–13행 | `text-text-primary`를 이유로 이름 변경을 거절하면서 `brand-text`는 `text-brand-text`를 만든다. 13행에 `on-danger` 누락, "40여 개" 근거 없음 | `link`나 `brand-ink` 같은 이름, `on-danger` 추가, 개수는 측정값으로 |
| CR-N2 | Nit | 50행 | 그림자 선언 위치(inline 여부)와 어둡게 값이 없다. `var()`만 쓰면 `shadow-*/opacity` 치환이 안 될 수 있다. `surface-raised`를 적용할 부품(ConfirmDialog, Floating, 결정 상자)이 없다 | 그림자 세 개의 두 테마 값과 위치, `surface-raised` 이전 부품 목록 |
| CR-N3 | Nit | 66행 | `Button.tsx:12` `hover:brightness-110`은 어둡게 분홍에서 차이가 거의 없고 hover 대비를 검사하지 않는다 | `danger-hover` 토큰과 `on-danger/danger-hover` 쌍 |
| CR-N4 | Nit | 80–82행 | DS2-04 "한 줄 최대 40em"에 대응하는 토큰이 없다. 지금 `--container-reading`은 48rem(17px에서 40em ≈ 680px). `"Noto Serif KR Fallback"` `@font-face` 없음 | 읽기 폭 토큰, 대체 `@font-face`, 바뀐 척도 이름을 `cn.test` 대상으로 |
| CR-N5 | Nit | 70행 | `contrast.test.ts`의 색상각 검사는 `SOURCE_TOKENS` 대상이다. `prov-*`와 두 테마로 옮긴다는 말이 없다. 어둡게 `prov-ai`와 `prov-human`은 채도 0.32로 기준 0.3에 가까워 조금만 바뀌어도 검사 대상에서 빠진다 | `PROV_TOKENS` × {light, dark} 색상각 검사, `prov-replay` 외 채도 0.3 이상 단언 |

## egm-structural

전체 판단: 수정 후 재검토 필요. 제목 계층과 Markdown 형식은 문제없다. Blocker 1, Major 8, Nit 4.

| ID | 등급 | 범위 | 지적 | 제안 |
|---|---|---|---|---|
| ST-B1 | Blocker | 74–78행 | CR-B1과 같은 결함. 더해, 2.4는 추가 항목만 적고 지울 것과 유지할 것이 없다. 지금 스크립트는 15행(`--color-*` 값이 16진수와 같음), 30–31행(`var(--color-focus)`, `var(--color-canvas)`), 42행(`prefers-color-scheme:dark`가 있으면 실패)을 검사한다. 42행을 지운다는 말이 없으면 v2 CSS는 반드시 실패한다 | 2.4를 "유지 / 삭제 / 변경 / 추가" 표로. v1 7절 CSS 검사 계약을 항목별로 처리 |
| ST-M1 | Major | 23–31행 | 대안 표가 Alternatives Rule을 못 채운다. 28–29행 이유가 "research 3.2/3.3" 링크뿐이다. 분할 화면 직접 구현(라이브러리 비교), 어둡게 이중 기재(`light-dark()`), 테마 저장소 localStorage(쿠키면 서버 렌더에서 테마를 앎), research 이름 채택 대안이 1.2에 없다 | 한 줄 이유 본문화(next-themes: 2025-03 이후 배포 없음, React 19 경고 이슈 #385; sonner: +9.4KB 대 +6.5KB; cmdk: Radix Dialog 의존). 네 행 추가, 1.1과 짝을 맞추는 "항목" 열 |
| ST-M2 | Major | 10–21행 | v1에 있던 "관련 요구" 열과 요구 요약이 없어 결정이 어느 DS2를 만족하는지 추적할 수 없다 | 1.1에 관련 요구 열, v1 "0. 읽기 전에"에 해당하는 요구 요약 |
| ST-M3 | Major | 2절 | DS2-03의 소스 검사("화면과 부품에 `dark:`나 16진수 없음")가 설계에 없다. 2.4는 빌드 CSS만 본다 | "2.6 소스 검사(`palette.test.ts`)": `\bdark:`, `#[0-9a-fA-F]{3,8}\b`, `-\[#` 0건 |
| ST-M4 | Major | 68–70행 | DS2-02 "포커스 링 항상 2px 띄움"을 강제하는 장치가 없다 | `palette.test.ts`가 `outline-offset-0`, `ring-inset`, `focus-visible:ring` 금지, 빌드 검사가 `outline-offset:2px` 확인 |
| ST-M5 | Major | 50, 70행 | `SOURCE_TOKENS` → `prov-*` 교체와 두 테마 색상각 검사가 없다(`prov-replay`는 채도 미달로 빠짐). 50행은 대비 테스트도 두 블록 동일성을 본다는데 `parseThemes`는 미디어 블록을 읽지 않는다 | 2.3에 교체와 두 테마 검사. `parseThemes`가 세 블록을 읽고 단위 테스트로 동일성 확인, 또는 50행을 "빌드 검사만"으로 |
| ST-M6 | Major | 50행 | 그림자를 `@theme`(정적)에 두면서 어둡게에서 바꾼다는 모순. 값 없음 | `@theme inline { --shadow-card: var(--lw-shadow-card) }`, 세 그림자 두 테마 값 표 |
| ST-M7 | Major | 78행 | CR-M3과 같음 | 같음 |
| ST-M8 | Major | 82행 | DS2-04 "글꼴을 못 받아도 배치가 깨지지 않는다"에 대응하는 설계가 없다. 대체 `@font-face`(local, `size-adjust`) 없음(v1은 Pretendard Fallback `size-adjust: 96%` 명시). 불러오기 경로, `text-display`의 세리프 여부, 40em 유지 여부 없음 | 대체 `@font-face`(N%는 추정치로 표시하고 5절 위험에 연결), import 경로와 위치, 세리프 적용 범위, 읽기 폭 |
| ST-N1 | Nit | 13, 66행 | `on-danger` 누락, `prov-replay(+-bg)` 표기가 네 토큰 모두에 `-bg`가 붙는지 모호 | "`prov-*` 네 종류와 각 `-bg`" |
| ST-N2 | Nit | 56–64행 | 비고 열이 한 행만 찼고 `neutral` 설명이 research 이름 칸에 있다. `on-danger` 계산 대비값 없음 | 비고 정리, 계산값 열 |
| ST-N3 | Nit | 70행 | "포커스와 brand 쌍은 뺀다"가 47쌍에서 빼는 것으로 읽힌다(47쌍에는 원래 없음). v1 `neutral/neutral-bg` 쌍이 사라지는 이유 없음 | "v1의 `focus/brand` 쌍을 지운다(47쌍에는 원래 없음)" |
| ST-N4 | Nit | 1–4행 | v1에 있던 작성 줄과 검토 보고서 링크 자리가 없다 | 작성 줄, 검토 뒤 "검토 반영본 vN"과 보고서 링크 |

## egm-style

전체 판단: 결정은 대부분 읽히지만 Major가 있다. research 절 번호만 적고 근거를 생략한 곳이 많고, 새 토큰 목록과 검사 범위 서술이 서로 어긋난다. Blocker 없음. Major 12, Nit 8.

| ID | 등급 | 범위 | 지적 | 제안 |
|---|---|---|---|---|
| SY-M1 | Major | 13행 | "새 토큰만 더한다"인데 14행이 `source-*`를 지우고, `on-danger`가 빠졌고, 2.2의 `surface-sunken`, `brand-hover`, `focus`, `neutral`, `diff-*`가 유지인지 새것인지 모른다 | "더한다 / 지운다 / 유지한다" 세 목록 |
| SY-M2 | Major | 13행 | "다크 모드의 층(Carbon 방식)" 설명이 없고 `brand-subtle`, `brand-text`의 근거가 없다 | `surface-raised`: 어둡게에서 그림자 대신 바탕을 밝혀 층 구분. 두 brand 토큰 용도 |
| SY-M3 | Major | 14행 | "회사별 다섯 색"은 사실과 다르다. v1 다섯 쌍은 NVIDIA, Cloudflare, Groq, 대체 구현, 재현이다(v1 design.md 97–101행). "네 색"도 어떤 넷인지 없다 | "v1의 다섯 쌍(…)에서 네 쌍(AI, 규칙, 사람, 재현)으로" |
| SY-M4 | Major | 15행 | "가장 짧은 길"은 비교 없는 평가. "`@custom-variant` 선택자 문제"는 research 6절에서 실제로는 "빌드해 보지 않은" 미확인 항목이다 | "화면 코드에 테마 분기가 없다(DS2-03). `dark:`를 쓰지 않으므로 검증하지 못한 `@custom-variant` 선택자가 필요 없다" |
| SY-M5 | Major | 16, 28–29행 | 근거 자리에 절 번호만 있다 | 한 줄씩 본문화(ST-M1과 같은 내용) |
| SY-M6 | Major | 18행 | "기본 설정은 `text-ui`를 지운다"만 보면 tailwind-merge를 쓰면 안 되는 이유처럼 읽힌다 | "기본 설정은 `text-ui text-ink`에서 `text-ui`를 색으로 오인해 지우므로 `extendTailwindMerge`로 글자 크기, 그림자, 모서리 토큰을 등록한다" |
| SY-M7 | Major | 21행 | 렉스 정의 없음. "헤더에서 내리고"는 어디로인지, "줄여서"는 무엇을인지 모호. "인상을 가장 크게 좌우"는 검증 불가 | "마스코트 렉스는 헤더에서 빼고 (남는 위치)에만. 헤더는 평면 SVG 마크. 헤더는 모든 화면에 나온다" |
| SY-M8 | Major | 50, 70행 | 50행은 대비 테스트도 동일성을 본다는데 2.3은 미디어 블록을 읽지 않는다. "두 블록"이 어느 둘인지 없다 | "두 블록(`@media` 안과 `[data-theme="dark"]`)의 값 동일은 빌드 검사가 확인한다(2.4)" |
| SY-M9 | Major | 62행 | `neutral`, `neutral-bg`는 research 4.3 표에 없으므로 "같음"은 틀렸다. "상태 8개"가 8종인지 4종 × 2인지 모호 | 행을 나눠 `success`/`warning`/`danger`/`info`(+`-bg`)와 "research에 없음" 행 |
| SY-M10 | Major | 70행 | 빼는 대상이 47쌍인지 v1 쌍인지 모호, 최종 쌍 수(48) 없음, "2px 띄움 전제" 축약 | "47쌍 + `on-danger/danger` = 48쌍. v1 `focus/brand` 쌍 삭제. 포커스 링은 `outline-offset: 2px`라 링 옆이 바탕이므로 링과 네 바탕만 검사" |
| SY-M11 | Major | 66행 | "흰 글자가 안 된다"에 수치가 없다. 검토자 계산으로 `#FFFFFF`/`#F08A80` ≈ 2.4:1 (기존 계산 스크립트로 확인 필요) | "약 2.4:1로 4.5:1 미달" |
| SY-M12 | Major | 13, 31, 45–50, 66행 | "다크 모드", "다크 값", "어둡게 값", "어둡게에서"가 섞인다. "어둡게"는 부사형이라 명사로 어색 | 본문은 "밝은 테마 / 어두운 테마", "밝게/어둡게"는 설정 값과 표 열 제목에만 |
| SY-M13 | Major | 12행 | "화면 파일 40여 개" 출처 없음, "얻는 것이 없다" 단정 | 측정 명령과 개수, "바뀌는 것은 이름뿐" |
| SY-N1 | Nit | 3행 | "EGM 검토 전" 상태 | 검토 후 갱신 |
| SY-N2 | Nit | 17행 | "조각 로딩" 모호 | "`unicode-range`로 나눈 파일 중 필요한 것만 받는다" |
| SY-N3 | Nit | 20행 | "키보드 조작을 직접 보장"의 내용 없음 | "화살표 키로 폭 조절, `aria-valuenow` 갱신(DS2-07)" |
| SY-N4 | Nit | 27행 | "얻는 것은 복사한 코드뿐" 판단 섞임 | "같은 부품 위에 cva 스타일 코드가 복사될 뿐" |
| SY-N5 | Nit | 30행 | `/design-system`이 앱 경로인지, "다음에"가 언제인지 | "앱의 `/design-system` 화면에서 비교한 뒤 (조건)에" |
| SY-N6 | Nit | 43–47, 66행 | 16진수 대소문자 불일치. 빌드 검사가 문자열 비교면 문제 | 표기 하나로 |
| SY-N7 | Nit | 50행 | 그림자 "더 진하게" 정도 없음, 구조가 2.1 코드에 없음 | CR-N2, ST-M6과 같음 |
| SY-N8 | Nit | 74–77, 82행 | "고정 16진수"의 뜻, 75행 `--lw-canvas` 하나와 76행 `--lw-*` 전체의 범위 차이, 선택자 표기 `:root[data-theme="dark"]`와 `[data-theme=dark]` 혼용. "Noto Serif KR Fallback" 정의 위치, "법령명 제목" 부품 이름, 척도 중 `text-reading`만 값이 있음 | 각각 명시. 척도는 표로 옮기거나 "research 2.3 표가 기준" |
