import { describe, expect, it } from "vitest";
import type { TrailStage } from "./api";
import { checkCounts, checkState } from "./checkState";

const stage = (key: TrailStage["key"], status: TrailStage["status"]): TrailStage =>
  ({ key, actor: "rule", status, providerKind: null, providerName: null, detail: {} }) as unknown as TrailStage;
const done = [stage("critic_citation", "done"), stage("critic_source", "done"), stage("critic_judge", "done")];

describe("checkState (design.md 3.5)", () => {
  it("고친 문장은 적발이 있어도 검사 전", () => {
    expect(checkState({ findings: 2, stages: done, edited: true })).toBe("unchecked");
  });
  it("적발이 있으면 확인 필요", () => {
    expect(checkState({ findings: 1, stages: done })).toBe("flagged");
  });
  it("크리틱이 끝나지 않았으면 검사 전", () => {
    for (const st of ["running", "waiting", "not_run", "failed", "skipped"] as const)
      expect(checkState({ findings: 0, stages: [...done.slice(0, 2), stage("critic_judge", st)] })).toBe("unchecked");
  });
  it("크리틱 단계가 흔적에 없으면 검사 전", () => {
    expect(checkState({ findings: 0, stages: [stage("analyze", "done")] })).toBe("unchecked");
  });
  it("모두 끝나고 적발이 없으면 검사 통과", () => {
    expect(checkState({ findings: 0, stages: done })).toBe("passed");
  });
  it("흔적이 없는 옛 응답은 적발 수만 본다", () => {
    expect(checkState({ findings: 0 })).toBe("passed");
    expect(checkState({ findings: 0, stages: [] })).toBe("passed");
  });
  it("개수를 센다", () => {
    expect(checkCounts(["passed", "flagged", "flagged", "unchecked"])).toEqual({ passed: 1, flagged: 2, unchecked: 1 });
  });
});
