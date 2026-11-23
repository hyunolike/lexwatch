import type { Role } from "./api";

/** 처음 방문 안내(first-visit-guide)의 상태. 브라우저 저장소에 JSON으로 둔다. */
export type StepId = "open-review" | "approve" | "admin-run";
export type GuideState = { v: 1; dismissed: boolean; done: StepId[] };

export const GUIDE_KEY = "lw_guide";
export const STEP_IDS: readonly StepId[] = ["open-review", "approve", "admin-run"];
export const INITIAL_GUIDE: GuideState = { v: 1, dismissed: false, done: [] };

const isStep = (x: unknown): x is StepId => typeof x === "string" && (STEP_IDS as readonly string[]).includes(x);

/** 저장된 값을 읽는다. 없거나 깨졌거나 모르는 형식이면 처음 상태(FG-06). */
export function parseGuide(raw: string | null): GuideState {
  if (!raw) return INITIAL_GUIDE;
  let data: unknown;
  try {
    data = JSON.parse(raw);
  } catch {
    return INITIAL_GUIDE;
  }
  if (typeof data !== "object" || data === null) return INITIAL_GUIDE;
  const o = data as Record<string, unknown>;
  if (o.v !== 1 || typeof o.dismissed !== "boolean" || !Array.isArray(o.done)) return INITIAL_GUIDE;
  const done = STEP_IDS.filter((id) => (o.done as unknown[]).filter(isStep).includes(id));
  return { v: 1, dismissed: o.dismissed, done };
}

/** 방문한 경로가 완료시키는 단계. 목록 화면 자체는 완료가 아니다. 승인(approve)은 경로가 아니라 승인 성공으로 정한다(FG-04). */
export function stepForVisit(path: string, role: Role): StepId | null {
  if (/^\/reviews\/[^/]+\/?$/.test(path)) return "open-review";
  if (role === "admin" && /^\/runs\/[^/]+\/?$/.test(path)) return "admin-run";
  return null;
}

/** 단계를 완료로 표시한다. 이미 완료면 같은 객체를 돌려준다. */
export function markDone(state: GuideState, step: StepId): GuideState {
  if (state.done.includes(step)) return state;
  return { ...state, done: STEP_IDS.filter((id) => id === step || state.done.includes(id)) };
}

export function progress(state: GuideState): { done: number; total: number } {
  return { done: state.done.length, total: STEP_IDS.length };
}
