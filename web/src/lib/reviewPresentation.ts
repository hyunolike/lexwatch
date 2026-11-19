import type { Draft } from "./api";

export function reviewAttention(draft: Draft | null) {
  const indices = draft?.sentences.flatMap((s, i) => s.findings.length > 0 ? [i] : []) ?? [];
  return { hasDraft: draft !== null, flaggedSentences: indices.length, firstFlaggedIndex: indices[0] ?? null };
}
