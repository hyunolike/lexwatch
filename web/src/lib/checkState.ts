import type { TrailStage } from "./api";

/** 문장별 검증 상태(DS2-06, design.md 3.5). 출처(누가 썼나)와 따로 본다. */
export type CheckState = "passed" | "flagged" | "unchecked";

export const CHECK_LABEL: Record<CheckState, string> = { passed: "검사 통과", flagged: "확인 필요", unchecked: "검사 전" };

const CRITIC_KEYS = ["critic_citation", "critic_source", "critic_judge"] as const;

/**
 * 위에서부터 먼저 맞는 조건으로 정한다.
 * 1. 수정 중에 고친 문장은 검사를 다시 받지 않았으므로 검사 전
 * 2. 적발이 있으면 확인 필요
 * 3. 크리틱 단계가 하나라도 끝나지 않았으면 검사 전(흔적이 없는 옛 응답은 이 조건을 건너뛴다)
 * 4. 그 밖은 검사 통과. 통과는 "규칙과 AI 판정이 문제를 찾지 못했다"이지 "맞다"가 아니다.
 */
export function checkState(input: {
  findings: number;
  stages?: TrailStage[] | null;
  edited?: boolean;
}): CheckState {
  if (input.edited) return "unchecked";
  if (input.findings > 0) return "flagged";
  if (input.stages && input.stages.length > 0) {
    const critic = input.stages.filter((s) => (CRITIC_KEYS as readonly string[]).includes(s.key));
    if (critic.length === 0 || critic.some((s) => s.status !== "done")) return "unchecked";
  }
  return "passed";
}

/** 상단 요약의 개수. */
export function checkCounts(states: CheckState[]): Record<CheckState, number> {
  const out: Record<CheckState, number> = { passed: 0, flagged: 0, unchecked: 0 };
  for (const s of states) out[s]++;
  return out;
}
