# Stage 5: Initial Zoom Out 보고서

- 대상: `docs/features/design-system-v2/design.md` (전체 1–173행)
- 기준: `zoom-out-standard.md`
- 검토자: egm-zoom-out, egm-inventive-catalyst (각각 새 문맥, 2026-11-25)
- 기준 자료: spec.md, research.md, v1 spec.md와 design.md, `web/src`, `web/scripts`, `web/package.json`

## egm-zoom-out

전체 판단: 방향은 단순하고 기존 자산(Base UI, 의미 토큰 이름, 대비 테스트)을 재사용한다. 다만 출처 표현이 코드에 네 갈래로 나뉘어 있는데 설계는 한 갈래만 고친다. Blocker 1, Major 4, Nit 4.

| ID | 등급 | 범위 | 지적 | 제안 |
|---|---|---|---|---|
| ZO-B1 | Blocker | 14, 109, 117, 119, 121–129행 | 출처 어휘가 이미 여러 벌이다. `ds/badges.tsx`의 `SOURCES`(회사별 색), `ds/AiLabel.tsx`(`bg-info-bg text-info`), `AiTrail.tsx`(ai는 `info`, human은 `success`), `RunTimeline.tsx`의 `ACTOR`(`bg-info`, `bg-success`). 타입도 `lib/api.ts:80` `TrailActor = "ai" \| "rule" \| "human"`과 `lib/aiTrail.ts:16` `ACTOR_LABEL`로 있다. 3.1에 AiLabel과 AiTrail이 없고 RunTimeline은 "토큰과 밀도"로만 올라 있어, 구현해도 AI와 사람 표시가 상태 색을 계속 쓴다. DS2-05 위반이고 spec 1절 문제 2가 남는다. 3.2의 `labels.ts` 새 대응표는 네 번째 어휘가 된다 | `TrailActor`에 `"replay"`를 더한 `Provenance` 타입과 `providerKind → Provenance` 함수 하나(`stepActor`, `ACTOR_LABEL`을 넓혀서). 색과 아이콘은 `SourceLabel` cva 변형 한 벌. `AiLabel`, `AiTrail`, `RunTimeline`의 `ActorBadge`와 막대가 이 표를 쓰게 하고 3.1에 명시 |
| ZO-M1 | Major | 37–50, 70, 75–76행 | 어둡게 값을 미디어 쿼리 블록과 `[data-theme="dark"]` 블록에 두 번 적고, 같은지 보는 검사를 대비 테스트와 빌드 검사 양쪽에 새로 둔다 | (a) CSS `light-dark()`로 한 줄에 두 값, `color-scheme`만 `[data-theme]`로 전환. 또는 (b) 인라인 스크립트가 늘 실제 테마를 `data-theme`에 적고 CSS는 `[data-theme=dark]` 블록 하나. 두 블록 동일성 검사는 뺀다 |
| ZO-M2 | Major | 92, 100, 104행 | 테마, 밀도, 분할 폭 저장을 각자 만든다. `lib/guideStore.ts`가 이미 try/catch, 메모리 대체, `useSyncExternalStore` 패턴을 테스트와 함께 갖췄다. 밀도는 CSS 변수로 동작하므로 `DensityProvider`가 필요 없고, 첫 그림 뒤 적용되면 행 높이가 튄다 | `createPrefStore(key, parse)`로 일반화해 셋을 인스턴스로. 인라인 스크립트 하나가 `data-theme`과 `data-density`를 함께 적용. `DensityProvider.tsx` 삭제 |
| ZO-M3 | Major | 66, 70행 | 테마에 따라 뒤집히는 조합이 `on-danger` 하나뿐이다. `ui/Floating.tsx:35` 툴팁 `bg-ink text-on-brand`, `ui/ConfirmDialog.tsx:39` 배경막 `bg-ink/40`, `globals.css`의 `.lw-stripes` `rgb(255 255 255 / 0.28)`, `tokens.css` 그림자 세 개의 고정 `rgb(26 34 51 / …)` | 2.2에 "뒤집히는 조합" 목록. `inverse`/`on-inverse`, `scrim` 토큰, 줄무늬 토큰화, 그림자 `--lw-shadow-*`. 새 쌍을 `CONTRAST_PAIRS`에, `Floating.tsx`와 `ConfirmDialog.tsx`를 3.1에 |
| ZO-M4 | Major | 14, 117행 | `app/ops/page.tsx:14-18` `PROVIDER`가 `bg-source-nvidia`, `bg-source-cloudflare`, `bg-info`로 공급자별 쌓은 막대와 범례를 칠한다. `source-*`를 지우면 깨지는데 대신할 방법이 없다 | 차트 칸을 모두 `prov-ai`로 칠하고 칸 사이에 `surface` 1px 틈. 범례 글자가 회사를 말한다. 차트 전용 척도는 만들지 않는다 |
| ZO-N1 | Nit | 90행 | `cx`를 `cn`으로 다시 내보내고 "점차 지운다"면 같은 이름 둘이 남는다. `@/lib/cx` 사용 파일은 16개 | 단계 1에서 한 번에 바꾸거나 이름은 `cx`로 두고 내부만 `twMerge` |
| ZO-N2 | Nit | 16, 94행 | `useServerInsertedHTML`은 CSS-in-JS용이다. `app/layout.tsx`의 `<head>`에 인라인 `<script>`를 직접 두면 된다 | 직접 못 넣는 이유를 적거나 `ThemeScript.tsx`를 없앤다 |
| ZO-N3 | Nit | 148, 152행 | 상태 변수 타입과 함수 조건 나열은 구현 세부다. 단 한글 조합과 입력창 제외는 남길 가치가 있다 | 선택 사항 |
| ZO-N4 | Nit | 112–113행 | `WakeGate.tsx:121` 실패 화면이 `<Mascot pose="error" size={180} />`이다. DS2-14는 깨우기 화면에서도 작게 | 113행에 "실패 화면 렉스 120px" |

참고: 인용 미리보기(DS2-08)에 Base UI의 마우스 올림 카드 계열을 쓴다고 한 줄 적으면 새 부품을 만든다는 오해를 막는다. 토큰 이름 유지, next-themes·shadcn·sonner·cmdk 제외, 분할 화면 직접 구현과 축소 순서, `cn.test.ts`가 `tokens.css`를 읽는 방식은 적절하다.

## egm-inventive-catalyst

전체 판단: 조건부 통과. 기반 결정은 타당하지만 Major 6건은 구조를 바꾸면 없앨 수 있는 타협이다. Blocker 없음.

| ID | 등급 | 범위 | 거짓 타협 | 제안 | 부수 비용 |
|---|---|---|---|---|---|
| IC-1 | Major | 15, 37–50, 72–78행 | 시스템 따르기와 수동 전환을 함께 하려면 어둡게 값 중복이 불가피하다고 보고 검사로 감시만 더한다 | `light-dark()`로 한 번만 적고 `color-scheme`으로 전환. 미디어 블록과 동일성 검사 삭제. 그림자도 `light-dark()` | Lightning CSS가 대상 브라우저에 따라 `--lightningcss-light/dark`로 낮출 수 있어 빌드 검사 정규식을 바꿔야 한다. 단계 1에서 산출물 확인. Safari 17.5 미만 미지원, 지원 하한 명시 |
| IC-2 | Major | 95–98, 150–152행 | 같은 동작을 단축키 표, 팔레트 묶음, `?` 도움말 세 곳에 정의한다. 역할 판정은 단축키에만 있다. 루트 layout의 팔레트가 "대기함을 한 번 받은 결과"(spec 5절)를 얻는 방법이 없다 | 명령 등록소 `lib/commands.ts`(`{id, label, group, key?, enabled(ctx), run}`). 화면이 `useCommands`로 등록, 세 소비자는 읽기만. 역할 판정은 `enabled` 한 곳 | 등록과 해제 생명주기, "한 번 받은 결과"를 위해 마지막 값을 남겨야 함 |
| IC-3 | Major | 152행 | spec 5절은 "본문에 포커스가 있을 때만"(허용 목록)인데 설계는 거부 목록이다. 메뉴의 글자 탐색, Autocomplete 목록, Popover, 분리선에 포커스가 있을 때 `a`, `e`, `r`, `j`, `k`가 함께 실행된다 | 허용 목록으로 뒤집는다. `body`나 `[data-shortcut-scope]` 안에서만. `[role=menu\|listbox\|combobox\|dialog\|separator]` 조상이나 `e.defaultPrevented`이면 무시. `keyCode === 229`도 조합 중 | 영역 표시를 빠뜨리면 단축키가 안 듣는 쪽으로 실패(안전한 방향) |
| IC-4 | Major | 110, 146–148행 | 칩이 폭에 따라 두 동작(`onPin` 또는 팝오버)을 갖고, 미리보기까지 레이어가 둘이다. 1280px 미만에서 미리보기와 팝오버가 겹친다. 1280px 분기를 CSS가 할지 JS(matchMedia)가 할지 없다 | 칩은 늘 `onSelect(id)` 하나. `selected` 상태가 두 배치를 움직이고 배치 전환은 CSS 그리드만. 떠 있는 레이어는 Base UI Popover 하나(`openOnHover` + `delay`), 올림은 임시, 클릭과 Enter는 고정, 분할에서는 패널로 | 올림으로 열 때 포커스를 가져가지 않는지 확인 |
| IC-5 | Major | 107, 158–159, 169행 | 단계 1에서 `source-*`를 지우지만 `badges.tsx`, `ops/page.tsx`, `design-system/page.tsx`는 단계 2 이후 고친다. 그 사이 `bg-source-nvidia`는 CSS만 안 생기고 모든 명령을 통과한다. DS2-03을 소스에서 강제하는 장치가 없다. `palette.test.ts`는 원색 거부 목록이라 `dark:`, `bg-[#...]`, 지운 토큰을 못 잡는다 | `palette.test.ts`를 허용 목록으로. 색 유틸리티가 `tokens.css`에 정의된 이름만 쓰는지, `dark:`와 `-[#`이 없는지 | 색 아닌 `text-*` 구분에 글자 크기 이름 목록 필요(`cn.ts`와 공유). 조합 문자열은 못 잡으므로 cva 정적 문자열 규칙 |
| IC-6 | Major | 70행, DS2-09 | 어둡게에서 포커스/brand가 1.09:1이라 검사에서 빼고 "늘 띄운다"는 규칙에 기댄다. 강제 장치가 없고, 대기함 `j`/`k` 행 이동이 금지한 "행 안쪽 표시"를 새로 만든다 | 두 겹 포커스(`outline` + 안쪽 `box-shadow` 바탕 띠)를 `:focus-visible` 전역 규칙으로. 행 선택은 왼쪽 막대와 `brand-subtle` 배경, 그 쌍을 대비 테스트에 | 기존 그림자 토큰과 `box-shadow` 합성, tailwind-merge 그림자 묶음 확인 |
| IC-7 | Nit | 92–94, 100, 104행 | 테마만 첫 그림 전에 적용되고 밀도와 분할 폭은 하이드레이션 뒤라 튄다. 저장 값 검증이 두 번 작성된다 | `lib/prefs.ts` `readPref(key, allowed, fallback)` 하나, 같은 인라인 스크립트가 `data-theme`, `data-density`, `--lw-split`을 적용 | 스크립트 크기, CSP 해시 |
| IC-8 | Nit | 62행 | `neutral = ink-muted`를 값 복사로 두면 어긋난다 | `--lw-neutral: var(--lw-ink-muted)` 별칭, `parseThemes`가 한 단계 풂 | 파서 복잡도 |
| IC-9 | Nit | 146행 | 결정 버튼을 두 위치에 렌더하면 수정 중 상태가 둘로 나뉜다 | `DecisionBar` 하나, CSS 그리드와 `sticky`만 전환 | DOM 순서와 보이는 순서 차이(WCAG 2.4.3). DOM은 헤더 쪽 |
| IC-10 | Nit | 82행 | 부품마다 `font-serif`를 붙인다. `"Noto Serif KR Fallback"` `@font-face`(`size-adjust`)가 없다 | `@utility` 묶음 유틸리티(tailwind-merge에 등록). 대체 `@font-face` 명시 | |
