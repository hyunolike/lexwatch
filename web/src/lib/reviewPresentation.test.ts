import { describe, expect, it } from "vitest";
import { reviewAttention } from "./reviewPresentation";
import type { Draft } from "./api";

const draft = (findings: number[]): Draft => ({
  version: 1,
  createdBy: "agent",
  openQuestions: [],
  sentences: findings.map((count) => ({
    sentence: "확인할 문장",
    citations: [],
    findings: Array.from({ length: count }, () => ({ tier: 2, ruleId: "C2-MISMATCH", message: "숫자가 달라요" })),
  })),
});

describe("검토할 문장 요약", () => {
  it("초안 없음을 검사에서 지적이 없는 상태와 구분한다", () => {
    expect(reviewAttention(null)).toEqual({ hasDraft: false, flaggedSentences: 0, firstFlaggedIndex: null });
    expect(reviewAttention(draft([]))).toEqual({ hasDraft: true, flaggedSentences: 0, firstFlaggedIndex: null });
  });
  it("한 문장의 여러 지적을 한 문장으로 센다", () => {
    expect(reviewAttention(draft([0, 3, 1]))).toEqual({ hasDraft: true, flaggedSentences: 2, firstFlaggedIndex: 1 });
  });
  it("첫 문장의 지적과 지적 없는 초안을 구분한다", () => {
    expect(reviewAttention(draft([1, 0])).firstFlaggedIndex).toBe(0);
    expect(reviewAttention(draft([0, 0])).firstFlaggedIndex).toBeNull();
  });
});
