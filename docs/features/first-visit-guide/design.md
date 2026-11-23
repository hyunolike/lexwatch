# 설계: 처음 방문 안내

- 명세: [spec.md](spec.md), 조사: [research.md](research.md), 검토: [reviews/2026-11-23-round1.md](reviews/2026-11-23-round1.md)

## 1. 화면

승인 대기함(`/`)의 머리말 아래, 목록(`WakeGate`)보다 위에 둘러보기 영역을 둔다. `WakeGate` 바깥이라 서버를 깨우는 동안에도 보인다(FG-01).

```
┌ 처음 오셨나요? 이렇게 둘러보세요                 1/3 완료  [×] ┐
│ 매일 공포되는 법령 개정을 모아, AI가 각 담당 팀(실무그룹)에     │
│ 미칠 영향을 정리한 초안을 써요. 자동 검사가 근거와 맞지 않는    │
│ 문장을 표시하고, 변호사가 근거를 대조해 승인해야 팀에 알림이    │
│ 가요. AI 초안은 틀릴 수 있고, 자동 검사도 모든 오류를 잡지는    │
│ 못해요. 지금은 초안을 승인하는 변호사 역할로 보고 있어요.       │
│                                                                 │
│ 1 ✓ 확인 필요 초안 열기                                         │
│     아래 목록에서 '확인 필요'가 붙은 초안을 열고, 표시된 문장을 │
│     근거 법령과 비교해 보세요.                                  │
│ 2 ○ 초안 승인하기                                               │
│     1에서 연 화면에서 승인하면 완료돼요. 승인한 초안은          │
│     [알림함]에 팀 알림으로 쌓여요.                              │
│ 3 ○ [관리자 화면에서 작업 기록 보기]                            │
│     누르면 관리자 역할로 바뀌어요. 작업 하나를 열면 법령 수집   │
│     부터 승인까지 AI가 한 일이 단계별로 보여요.                 │
│                                                                 │
│ 데모 데이터는 매일 새벽 초기화돼요. 첫 접속은 서버를 깨우느라   │
│ 1분쯤 걸려요.                                                   │
└─────────────────────────────────────────────────────────────────┘
```

- 역할 문장은 현재 역할을 따른다. 관리자면 "지금은 실패한 작업을 확인하는 관리자 역할로 보고 있어요. 승인은 변호사 역할에서 해요."
- 1단계는 링크가 없다. 바로 아래 목록이 그 자리다.
- 2단계의 [알림함]은 `/inbox` 링크다. 완료는 승인 성공으로만 정한다.
- 3단계 제목 전체가 링크다. 누르면 역할을 관리자로 바꾸고 `/runs`로 간다.
- 모두 마치면 "3/3 완료 · 다 둘러봤어요"를 보인다. 영역은 사용자가 닫을 때까지 남는다.
- 화면의 "안내"는 이 영역만 가리키고, 팀에 가는 것은 "알림"으로 부른다(알림함과 같은 말).
- 새 컴포넌트 `FirstVisitGuide`(`web/src/components/FirstVisitGuide.tsx`). `CARD` 위에 왼쪽 `info` 색 띠를 둔다. 마스코트는 쓰지 않는다.

머리(`AppHeader`)의 역할 전환 왼쪽에 "둘러보기" `<button>`(물음표 아이콘, 좁은 화면에서는 아이콘만 보이고 `aria-label="둘러보기 안내 열기"`)을 둔다. 누르면 `guideStore.reopen()` 후 `router.push("/")`. 이미 `/`에 있어도 동작한다(2절의 `focusRequested`).

대안: 첫 방문 때 대화상자를 띄우는 방식은 포커스를 가두고 목록을 가리며, NN/g 연구에서 성과 개선이 없었다. 화면 위에 겹치는 강조 투어는 화면마다 위치 계산과 키보드 처리가 필요하고 휴대폰에서 깨지기 쉽다. 둘 다 고르지 않았다.

## 2. 상태와 저장

`localStorage` 키 `lw_guide`, 값은 JSON.

```ts
type StepId = "open-review" | "approve" | "admin-run";
type GuideState = { v: 1; dismissed: boolean; done: StepId[] };
```

순수 함수 (`web/src/lib/guide.ts`):

- `parseGuide(raw: string | null): GuideState` — null, 깨진 JSON, `v`가 1이 아니거나 필드 형식이 틀리면 처음 상태. 모르는 단계 ID는 버리고 중복은 하나로, 순서는 단계 순서로.
- `stepForVisit(path, role): StepId | null` — `^/reviews/[^/]+/?$` → open-review, 관리자이고 `^/runs/[^/]+/?$` → admin-run. 그 밖에는 null. approve는 경로로 정하지 않는다.
- `markDone(state, step)` — 이미 있으면 같은 객체를 돌려준다.
- `progress(state)` — `{ done, total: 3 }`.

저장소 (`web/src/lib/guideStore.ts`):

```ts
type GuideView = { state: GuideState; focusRequested: boolean };
function createGuideStore(storage: () => Storage | undefined): {
  subscribe(cb: () => void): () => void;
  getSnapshot(): GuideView;   // 처음 호출 때 한 번 읽어 캐시. 바뀌기 전까지 같은 객체
  dismiss(): void;            // dismissed=true 저장
  reopen(): void;             // dismissed=false 저장, focusRequested=true
  markStep(step: StepId): void; // markDone이 같은 객체면 저장도 알림도 안 함
  consumeFocus(): void;       // focusRequested=false
};
export const guideStore = createGuideStore(() => (typeof window === "undefined" ? undefined : window.localStorage));
export function useGuide(): GuideView | null; // useSyncExternalStore, 서버 스냅샷 null
```

- 저장소 접근(`storage()` 호출 포함)과 읽기, 쓰기를 모두 `try/catch`로 감싼다. 쓰기가 실패해도 메모리 상태는 바뀌므로 그 화면에서는 닫기가 유지된다(FG-06).
- `focusRequested`는 저장하지 않고 스냅샷에만 둔다.
- 서버 스냅샷이 `null`이라 서버 렌더링과 첫 클라이언트 렌더링 모두 안내를 그리지 않는다. 하이드레이션 직후 클라이언트 스냅샷으로 다시 그린다(FG-07). 이때 목록이 아래로 밀리지만, 처음 방문 때는 목록도 서버를 깨우는 중이라 받아들인다.
- 다른 탭과의 동기화(`storage` 이벤트)는 하지 않는다.

## 3. 완료 기록

- **open-review, admin-run**: `AppHeader`에 `const role = useRole()`을 더하고 `useEffect(() => { const s = stepForVisit(path, role); if (s) guideStore.markStep(s); }, [path, role])`. 렌더링 중에는 부르지 않는다. 하이드레이션 전 `useRole`은 서버 스냅샷 `"lawyer"`라 관리자 화면에서는 효과가 lawyer로 한 번, admin으로 한 번 돈다. 두 번째에서 완료되므로 문제없다.
- **approve**: `ReviewClient`의 `decide`에서 승인 결과가 성공일 때 `guideStore.markStep("approve")`. 반려와 409, 422는 완료가 아니다.

한계: 1단계는 초안을 "열면" 완료다. 실제로 근거를 비교했는지는 알 수 없다. 둘러보기 안내라서 이 정도로 충분하다.

## 4. 역할 전환

`RoleSwitch.tsx`에서 `switchRole(role)`(쿠키 저장과 구독자 알림)을 내보내고 `RoleSwitch`도 이것을 쓴다. 3단계 링크는 Next `Link`이고 `onClick`에서 `switchRole("admin")`을 부른 뒤 기본 이동을 그대로 둔다. 가운데 클릭과 Ctrl 클릭은 `onClick`이 불리지 않아 역할이 바뀌지 않는다(spec 오류 상황에 기록).

## 5. 접근성

- `<section aria-labelledby>`와 `<h2 tabIndex={-1}>`. 대화상자 역할을 쓰지 않는다.
- 단계는 `<ol>`. 완료는 체크 아이콘(`aria-hidden`)과 숨김 글자 "완료", 미완료는 빈 원 아이콘과 숨김 글자 "남음".
- 진행 "1/3 완료"는 화면 글자 그대로 읽혀도 뜻이 통하므로 별도 `aria-label`을 두지 않는다.
- 닫기 버튼은 `aria-label="둘러보기 안내 닫기"`. 닫으면 `document.getElementById("main")?.focus()`(이미 `tabIndex=-1`).
- 다시 열기: `FirstVisitGuide`의 `useEffect([visible, focusRequested])`에서 보이고 요청이 있으면 제목에 포커스를 주고 `consumeFocus()`.

## 6. 검증

- vitest(node 환경): 순수 함수, 그리고 가짜 `Storage`를 넣은 `createGuideStore`로 읽기 예외, 쓰기 예외, 깨진 JSON, 스냅샷 객체 동일성, `markStep` 중복 시 알림 없음, `reopen`과 `consumeFocus`.
- 브라우저(Playwright, 로컬): 처음 방문, 깨우는 중 표시, 세 단계 완료, 닫기 후 새로고침, `/`에서 둘러보기로 다시 열기와 포커스, `lw_guide`와 `lw_role=admin`을 미리 넣은 상태로 `/`를 열어 콘솔 오류와 하이드레이션 경고 없음, 휴대폰 폭.
