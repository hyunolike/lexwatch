# Stage 8: Intermediate Zoom Out 보고서

- 대상: `docs/features/design-system-v2/design.md` (전체 1–173행, 앞 단계 이후 변경 없음)
- 기준: `zoom-out-standard.md`
- 검토자: egm-zoom-out (새 문맥, 2026-11-25). Stage 5–7 보고서를 읽고 제안된 고침의 크기를 판정
- 기준 자료: spec.md, `web/src`(tokens.css, verify-design-css.mjs, guideStore.ts, RoleSwitch.tsx, Floating.tsx, CitationChip.tsx, aiTrail.ts, ops/page.tsx, app/page.tsx, DraftEditor.tsx, ReviewClient.tsx, RunClient.tsx, api.ts)

전체 판단: 조건부 통과. 앞 단계가 찾은 결함은 대부분 실제 결함이지만 제안된 고침 가운데 약 3분의 1은 설계를 불필요하게 키운다. 범위도 마감에 비해 넓다. 새 발견 Blocker 0, Major 7, Nit 6.

## A. 앞 단계 제안 판정 (경쟁하는 고침 중 가장 작은 것)

| ID | 대상 지적 | 판정 | 내용 |
|---|---|---|---|
| ZO2-A1 | ZO-M1, IC-1, CR-M6, CR-M1(b), CR-M3, CO-12, SY-M8, ST-M5 | 채택: 스크립트가 늘 `data-theme` | 인라인 스크립트가 늘 실제 테마(`light`/`dark`)를 `data-theme`에 적고 CSS는 `:root{}`와 `[data-theme="dark"]{}` 두 블록만. 미디어 블록과 두 블록 동일성 검사가 사라진다. `color-scheme`을 같은 두 블록에 적어 CR-M3도 해결. "시스템"일 때 OS 변경은 `matchMedia` change 리스너. JS가 꺼지면 밝은 테마뿐인데 이 앱은 클라이언트 렌더링 전제라 잃는 것이 없다. **폐기: `light-dark()`**(Lightning CSS가 `--lightningcss-*`로 낮출 수 있어 빌드 검사와 파서를 미확인 산출물에 맞춰야 함). **유지: CR-M1(a)** 밝은 블록과 어두운 블록 키 집합 동일 테스트. 1.2에 `light-dark()`를 버린 이유 한 줄 |
| ZO2-A2 | IC-2 명령 등록소 | 폐기 | 팔레트 항목(이동, 역할, 테마, 밀도, 검토 검색)과 단축키는 거의 겹치지 않는다. 단축키는 보이는 버튼과 같은 처리 함수를 같은 조건으로 부른다(버튼이 없으면 단축키도 무동작). `?` 도움말은 정적 `SHORTCUTS` 상수, 팔레트는 자체 정적 목록 |
| ZO2-A3 | IC-3 허용 목록 | 최소형 채택 | `body`이거나 `<main>` 안 링크·버튼일 때만. `[role=menu\|listbox\|combobox\|dialog\|separator]` 조상, `e.defaultPrevented`, 조합 중(`isComposing`, `keyCode 229`)이면 무시. **폐기: `[data-shortcut-scope]` 표시 계약.** 대기함 `j/k`는 행 링크에 실제 포커스를 옮겨 전역 `:focus-visible`을 그대로 쓴다 |
| ZO2-A4 | IC-6, CR-M5, ST-M4 | 두 겹 링 폐기 | 전역 `:focus-visible{outline-offset:2px}`와 빌드 검사 한 줄. `palette.test.ts`의 `ring-inset` 금지는 넣어도 됨. focus 쌍 바탕을 모든 `*-bg`로 늘리지 않는다 |
| ZO2-A5 | ZO-B1, CD-M1, CD-M2, IR-M2 | 최소형 채택 | 새 모듈 없이 `TrailActor`(`lib/api.ts`)에 `"replay"`를 더하고 `aiTrail.ts`의 `ACTOR_LABEL` 옆에 `providerKind → TrailActor` 함수 하나. 색은 `SourceLabel` cva 한 벌을 `AiLabel`, `AiTrail`, `RunTimeline` ActorBadge가 쓴다. 3.2 표는 이 함수의 명세로 |
| ZO2-A6 | ZO-M4, CR-M2, IR-M1, CD-M3 | 둘 다 그대로는 불채택 | ops 막대는 `aria-hidden`이라 범례 색 칸으로만 구분된다. 모두 `prov-ai`로 칠하면 범례가 무의미해지고, 차트 전용 토큰은 차트 하나에 토큰과 쌍을 늘린다. **쌓은 막대를 공급자별 가로 막대 목록(이름, `prov-ai` 막대, 건수와 비율)으로.** 범례(122–129행)를 줄 단위로 합치면 된다 |
| ZO2-A7 | ZO-M2, IC-7 | 조건부 폐기 | B4와 B5를 받으면 저장 설정은 테마 하나라 RoleSwitch 구독 패턴(모듈 리스너 + `useSyncExternalStore`)을 따르면 된다. `DensityProvider.tsx`는 어느 경우든 삭제 |
| ZO2-A8 | 기타 | 개별 판정 | IC-8: `@theme inline`에서 `--color-neutral: var(--lw-ink-muted)`로 별칭(파서 수정 불필요, CR-M4(5)도 해소). CR-N3 `danger-hover` 폐기(`hover:bg-danger/90`). IC-10 `@utility` 폐기, 대체 `@font-face`는 유지(v1 `Pretendard Fallback` 복사). CR-M4는 실제 쓰는 조합만, `text-brand` 세 곳은 `brand-text`로. IC-4 채택: 미리보기는 기존 `ui/Floating.tsx` Popover에 `openOnHover`, 칩 콜백 하나, 1280px 분기는 ReviewClient 한 곳. IC-5: `source-*` 삭제를 단계 2로 옮기고 허용 목록 검사는 `tokens.css` 색 이름과 `--text-*`를 읽는 테스트 하나. CR-B1/ST-B1은 필수, "유지 / 삭제 / 변경 / 추가" 표면 충분하고 A1을 받으면 `verify-design-css.mjs:42` 금지 규칙은 유지 가능. Comprehension 제안 중 설계에 새로 쓸 것은 단축키 표(CO-5)와 CheckMark 상태 표(CO-6)뿐, 역할(CO-4)·렉스(CO-2)·일곱 화면(CO-16)은 기존 코드나 v1 문서를 한 줄로 가리킨다. SY-M12 용어 정비와 ST-M1 대안 네 행은 Nit로 내림 |

## B. 앞 단계가 놓친 문제

| ID | 등급 | 범위 | 지적 | 권고 |
|---|---|---|---|---|
| ZO2-M1 | Major | 146행 | 결정 버튼을 헤더로 올리는 것은 spec에 없는 범위다. DS2-07은 버튼 위치를 요구하지 않는다. 두 위치 렌더링, 수정 중 상태 분리(IC-9), DOM 순서 문제가 따라온다 | 146행 삭제, 지금 아래 고정 상자를 모든 폭에서 유지. 133–144행 그림의 헤더 버튼도 삭제. IR-M4, CD-M10, IC-9가 함께 사라진다 |
| ZO2-M2 | Major | 99, 117행, DS2-11 | 재실행 접수는 `app/runs/[id]/RunClient.tsx:23`인데 3.1은 `runs/page.tsx`를 적었다. "링크 복사"는 코드에 없다. 테마·밀도 변경은 화면에 즉시 보이는 결과라 DS2-11 제목과 spec이 스스로 모순된다 | 토스트는 재실행 접수 하나로, 3.1에 RunClient.tsx. 테마 변경과 링크 복사 토스트를 빼는 spec 수정을 작성자에게 제안. Toast 도입 자체는 유지 |
| ZO2-M3 | Major | 103행 | `DraftEditor.tsx:41, 94`가 이미 문장별 "확인 필요"와 적발 목록을 그린다(`DraftSentence.findings`) | "DraftEditor의 문장별 표시를 `CheckMark`로 추출"로 쓴다. 상태 범위는 IR-M3 판정 참고 |
| ZO2-M4 | Major | 104행 | 분할 폭 저장(`lw_split`)은 spec에 없다. 저장소, 첫 그림 후 튐, 일반화 압력이 따라온다 | `lw_split` 삭제, 폭은 세션 상태 |
| ZO2-M5 | Major | 165행, DS2-12 | 밀도는 저장소, Provider, `--lw-row`, 팔레트 항목, 토스트, `/design-system` 비교, 세 화면 수정을 끌고 온다. 대기함은 표가 아니라 링크 격자(`app/page.tsx:23`, `py-4`)라 변수 하나로 끝나지 않는다. spec 1절 문제 1–6 중 밀도와 이어진 것이 없다 | 지금 다음 범위로 넘기고 spec 3절 "하지 않을 것"으로. 축소 순서: 검토 검색 → 분할 폭 끌기(DS2-07 수용 기준 수정 동반) → 스켈레톤을 대기함과 검토 상세로 → `/design-system` 읽기 글꼴과 밀도 비교. 뺄 때마다 해당 DS2 수용 기준 수정을 한 줄씩 |
| ZO2-M6 | Major | 4절 | 마감에 맞는 최소 범위가 없다. 16개 요구, 35행 파일이 평평하다. 핵심 문제 1(근거 대조)인 분할 화면이 단계 4로 단축키·팔레트 뒤에 있다 | "필수(DS2-01~08, 14, 15의 Button·badges·Callout)와 선택(09~13, 16 일부)" 경계. 분할 화면 단계를 단축키 앞으로. DS2-15 "표 행" cva는 대상이 사실상 없으므로 뺀다 |
| ZO2-M7 | Major | 92–94행 | `readTheme`/`applyTheme`과 인라인 스크립트 문자열이 같은 판정을 두 번 구현한다 | 원본은 인라인 스크립트 문자열 하나, `theme.test.ts`는 가짜 `localStorage`와 `matchMedia` 위에서 그 문자열을 실행. 런타임 전환은 `data-theme`을 적고 저장만. `ThemeScript.tsx` 대신 `app/layout.tsx` `<head>`에 직접 넣는 쪽을 권하되 Next 16 문서 확인 필요 |
| ZO2-N1 | Nit | 90행 | `cn`을 새로 만들지 말고 `cx` 이름을 유지하고 내부만 `extendTailwindMerge` | |
| ZO2-N2 | Nit | 148, 152행 | 함수 조건 나열은 A2, A3 규칙 한 문단으로. 키 표는 표로 | |
| ZO2-N3 | Nit | 105행 | `EvidencePanel`은 1280px 미만에서도 같은 컴포넌트를 CSS 그리드로 아래에 둔다. 기존 신구조문 영역을 이 패널로 옮긴다고 적는다 | |
| ZO2-N4 | Nit | 113행 | `WakeGate.tsx:121` 실패 화면 렉스 120px(ZO-N4 동의) | |
| ZO2-N5 | Nit | 169행 | 밝은 테마 값도 research 4.3 값으로 바뀐다(canvas `#f6f7f9` → `#f7f6f3`). `contrast.test.ts:12` 고정 기대값 수정을 3.1에 | |
| ZO2-N6 | Nit | 3.2 | 출처 네 종류 명칭과 판별 기준(CO-3)은 A5 함수 시그니처와 3.2 표로 해결. 별도 정의 절은 만들지 않는다 | |

## C. 마감 적합성

현재 범위로는 빠듯하다. 결정 버튼 이동(ZO2-M1), 분할 폭 저장(ZO2-M4), 밀도(ZO2-M5)를 빼고 등록소(A2), 차트 전용 토큰(A6), A8 폐기 항목을 채택하지 않으면 파일 표가 약 6행 줄고 새 저장소와 추상화 3개가 사라진다. 165행에서 밀도를 맨 앞에 둔 것은 맞지만 "밀리면 뺄 것"이 아니라 지금 미룰 대상이다.

## 검토자 간 이견 메모

ZO2-M7은 `useServerInsertedHTML` 대신 `<head>` 직접 삽입을 권했다. Stage 9의 egm-codebase-details는 Next 문서(`next/dist/docs/01-app/02-guides/preventing-flash-before-hydration.md`)가 `useServerInsertedHTML` 방식을 허용한다고 확인했다(CD-N5). 통합 보고서는 "문서가 안내하는 방식 하나를 고르고 그 문서의 조건을 적는다"로 정리한다.
