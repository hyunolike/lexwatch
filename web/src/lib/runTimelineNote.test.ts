import { describe, expect, it } from "vitest";
import type { Bar } from "./runTimeline";
import { rowNote } from "../components/RunTimeline";

const bar = (p: Partial<Bar>): Bar => ({
  step: "classify", groupCode: null, status: "Succeeded", attempt: 1, actor: "ai", providerKind: null, errorCode: null,
  start: 0, durationMs: 6600, x: 0, w: 10, pending: false, again: false, ...p,
});

describe("rowNote", () => {
  it("앞 기록의 실패를 마지막 막대 글에 합친다(겹침 방지)", () => {
    expect(
      rowNote([bar({ status: "Failed", attempt: 5, durationMs: 53, errorCode: "AI_UPSTREAM_ERROR" }), bar({ again: true })]),
    ).toBe("6.6초 · 다시 시도한 기록 (앞 기록: 53ms · 실패 AI_UPSTREAM_ERROR · 시도 5)");
  });
  it("기록이 하나면 그 설명만", () => {
    expect(rowNote([bar({ longest: true, durationMs: 23800 })])).toBe("23.8초 · 가장 오래 걸림");
  });
});
