import { describe, expect, it } from "vitest";
import type { TimelineItem } from "./api";
import { GAP_PX, MIN_BAR_PX, formatGap, layoutTimeline, stepActor } from "./runTimeline";

const T0 = Date.parse("2026-11-24T01:00:00Z");
const at = (sec: number) => new Date(T0 + sec * 1000).toISOString();

function step(name: string, sec: number, durationMs: number | null, p: Partial<Extract<TimelineItem, { kind: "step" }>> = {}): TimelineItem {
  return {
    kind: "step", at: at(sec), step: name, attempt: 1, status: "Succeeded", durationMs, errorCode: null, errorMessage: null,
    providerKind: "cloudflare", providerName: null, groupCode: null, summary: null, ...p,
  };
}

describe("layoutTimeline", () => {
  it("단계 순서와 그룹별 줄, 같은 축의 막대", () => {
    const l = layoutTimeline([
      step("fetch_articles", 0, 2000, { providerKind: "none" }),
      step("classify", 2, 6000),
      step("analyze", 8, 20000, { groupCode: "labor" }),
      step("analyze", 8, 10000, { groupCode: "finance" }),
    ], 300);
    expect(l.rows.map((r) => `${r.step}:${r.groupCode ?? ""}`)).toEqual(["fetch_articles:", "classify:", "analyze:finance", "analyze:labor"]);
    expect(l.gaps).toEqual([]);
    expect(l.busyMs).toBe(28000);
    expect(l.width).toBe(300);
    const labor = l.rows[3].bars[0];
    expect(labor.x).toBeCloseTo((8000 / 28000) * 300);
    expect(labor.w).toBeCloseTo((20000 / 28000) * 300);
    expect(l.rows[0].actor).toBe("rule");
  });

  it("60초를 넘는 빈 시간은 고정 폭으로 접고, 60초 이하는 그대로 둔다", () => {
    const near = layoutTimeline([step("classify", 0, 1000), step("analyze", 60, 1000)], 300);
    expect(near.gaps).toEqual([]);
    const far = layoutTimeline([step("classify", 0, 1000), step("analyze", 62, 1000)], 300);
    expect(far.gaps).toHaveLength(1);
    expect(far.gaps[0].ms).toBe(61000);
    expect(far.width).toBe(300 + GAP_PX);
    expect(far.rows[1].bars[0].x).toBeCloseTo(150 + GAP_PX);
  });

  it("관리자 다시 실행은 같은 줄의 두 번째 막대, 실패와 시도 수를 보존한다", () => {
    const l = layoutTimeline([
      step("classify", 0, 53, { status: "Failed", attempt: 5, errorCode: "AI_UPSTREAM_ERROR", providerKind: "none" }),
      step("classify", 1800, 6600),
    ]);
    expect(l.rows).toHaveLength(1);
    expect(l.rows[0].bars.map((b) => [b.status, b.attempt])).toEqual([["Failed", 5], ["Succeeded", 1]]);
    expect(l.rows[0].bars[0].w).toBeGreaterThanOrEqual(MIN_BAR_PX);
    expect(l.gaps[0].ms).toBe(1800000 - 53);
    expect(l.rows[0].actor).toBe("ai");
  });

  it("진행 중은 남은 durationMs를 무시하고 지금까지 지난 시간, 대기는 폭 0, 병목 분모는 끝난 막대만", () => {
    const l = layoutTimeline([
      step("analyze", 0, 24000, { groupCode: "finance" }),
      // 2번째 시도가 진행 중이지만 앞 시도의 durationMs(9000)가 남아 있다
      step("critic_judge", 24, 9000, { status: "Running", attempt: 2, groupCode: "finance" }),
      step("critic_citation", 30, null, { status: "Pending", attempt: 0, groupCode: "finance" }),
    ], 720, T0 + 27_000);
    const judge = l.rows.find((r) => r.step === "critic_judge")!.bars[0];
    expect(judge.durationMs).toBe(3000);
    const pending = l.rows.find((r) => r.step === "critic_citation")!.bars[0];
    expect([pending.pending, pending.w]).toEqual([true, 0]);
    expect(l.workMs).toBe(24000);
    expect(l.longest).toMatchObject({ step: "analyze", durationMs: 24000, share: 1 });
  });

  it("재시도 대기는 마지막 시도 막대를 그리고 병목 후보에서 빠진다", () => {
    const l = layoutTimeline([
      step("analyze", 0, 30000, { status: "RetryScheduled", attempt: 2, groupCode: "labor" }),
      step("classify", 40, 5000),
    ], 720, T0 + 50_000);
    const a = l.rows.find((r) => r.step === "analyze")!.bars[0];
    expect([a.status, a.durationMs]).toEqual(["RetryScheduled", 30000]);
    expect(l.longest).toMatchObject({ step: "classify" });
    expect(l.workMs).toBe(35000);
  });

  it("같은 줄의 두 번째 기록은 again(재현의 재시도 기록, 관리자 다시 실행)", () => {
    const l = layoutTimeline([
      step("analyze", 0, 4000, { status: "Failed", attempt: 0, provider: "replay" } as never),
      step("analyze", 72, 9100, { attempt: 1 }),
    ]);
    expect(l.rows[0].bars.map((b) => b.again)).toEqual([false, true]);
  });

  it("결정 점은 접힌 시간 뒤에 놓인다", () => {
    const l = layoutTimeline([
      step("analyze", 0, 1000, { groupCode: "labor" }),
      { kind: "approval", at: at(7200), groupCode: "labor", decision: "approve", actorRole: "lawyer", comment: null },
    ], 100);
    expect(l.decisions).toHaveLength(1);
    expect(l.decisions[0].x).toBeCloseTo(100 + GAP_PX);
    expect(formatGap(l.gaps[0].ms)).toBe("2시간");
  });

  it("빈 입력", () => {
    expect(layoutTimeline([]).rows).toEqual([]);
  });
});

describe("stepActor와 formatGap", () => {
  it("대체 구현은 규칙, 재현은 그대로", () => {
    expect(stepActor("analyze", "fallback")).toBe("rule");
    expect(stepActor("analyze", "replay")).toBe("ai");
    expect(stepActor("critic_source", "replay")).toBe("rule");
  });
  it("시간 문구", () => {
    expect(formatGap(32 * 60_000)).toBe("32분");
    expect(formatGap(125 * 60_000)).toBe("2시간 5분");
    expect(formatGap(76 * 3600_000)).toBe("3일 4시간");
  });
});
