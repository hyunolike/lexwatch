import { GUIDE_KEY, markDone, parseGuide, type GuideState, type StepId } from "./guide";

/** 화면이 구독하는 값. focusRequested는 저장하지 않는다(다시 열기 직후 한 번만 포커스). */
export type GuideView = { state: GuideState; focusRequested: boolean };

/**
 * 처음 방문 안내 상태 저장소. 저장소 접근은 모두 try/catch로 감싸고,
 * 쓰기에 실패해도 메모리 상태는 바꿔 그 화면에서는 닫기가 유지된다(FG-06).
 * 스냅샷은 바뀌기 전까지 같은 객체다(useSyncExternalStore 요구).
 */
export function createGuideStore(storage: () => Storage | undefined) {
  const listeners = new Set<() => void>();
  let view: GuideView | null = null;

  const read = (): GuideState => {
    try {
      return parseGuide(storage()?.getItem(GUIDE_KEY) ?? null);
    } catch {
      return parseGuide(null);
    }
  };
  const getSnapshot = (): GuideView => (view ??= { state: read(), focusRequested: false });

  const set = (next: GuideView, persist: boolean) => {
    if (persist) {
      try {
        storage()?.setItem(GUIDE_KEY, JSON.stringify(next.state));
      } catch {
        // 저장소가 막혀도 메모리 상태로 계속 간다
      }
    }
    view = next;
    listeners.forEach((l) => l());
  };

  return {
    subscribe(cb: () => void) {
      listeners.add(cb);
      return () => {
        listeners.delete(cb);
      };
    },
    getSnapshot,
    dismiss() {
      const cur = getSnapshot();
      if (cur.state.dismissed && !cur.focusRequested) return;
      set({ state: { ...cur.state, dismissed: true }, focusRequested: false }, true);
    },
    reopen() {
      const cur = getSnapshot();
      set({ state: { ...cur.state, dismissed: false }, focusRequested: true }, true);
    },
    markStep(step: StepId) {
      const cur = getSnapshot();
      const next = markDone(cur.state, step);
      if (next === cur.state) return;
      set({ ...cur, state: next }, true);
    },
    consumeFocus() {
      const cur = getSnapshot();
      if (cur.focusRequested) set({ ...cur, focusRequested: false }, false);
    },
  };
}

export const guideStore = createGuideStore(() => (typeof window === "undefined" ? undefined : window.localStorage));
