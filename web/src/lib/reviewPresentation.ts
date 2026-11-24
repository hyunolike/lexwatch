import type { Draft } from "./api";

export function reviewAttention(draft: Draft | null) {
  const indices = draft?.sentences.flatMap((s, i) => s.findings.length > 0 ? [i] : []) ?? [];
  return { hasDraft: draft !== null, flaggedSentences: indices.length, firstFlaggedIndex: indices[0] ?? null };
}

/** 파트너에게는 자기 결정을 기다리는 검토(AwaitingPartner)를 맨 위에 따로 모아 준다. 다른 역할은 나누지 않는다. */
export function splitForRole<T extends { status: string }>(items: T[], role: string): { mine: T[]; rest: T[] } {
  if (role !== "partner") return { mine: [], rest: items };
  return { mine: items.filter((r) => r.status === "AwaitingPartner"), rest: items.filter((r) => r.status !== "AwaitingPartner") };
}
