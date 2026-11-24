import { describe, expect, it } from "vitest";
import { hoursText, maxOf, pct, sparkPoints, stackParts } from "./ops";

describe("ops 계산", () => {
  it("스파크라인: 최댓값이 위, 0만 있으면 바닥선", () => {
    expect(sparkPoints([0, 5, 0], 10, 10, 0)).toBe("0.0,10.0 5.0,0.0 10.0,10.0");
    expect(sparkPoints([0, 0], 10, 10, 1)).toBe("1.0,9.0 9.0,9.0");
    expect(sparkPoints([])).toBe("");
  });
  it("비율과 시간 문구", () => {
    expect(pct(0.043)).toBe("4.3%");
    expect(pct(0.5)).toBe("50%");
    expect(pct(null)).toBe("—");
    expect(hoursText(0.5)).toBe("30분");
    expect(hoursText(3)).toBe("3시간");
    expect(hoursText(72)).toBe("3.0일");
    expect(hoursText(null)).toBe("—");
  });
  it("누적 막대는 0을 빼고 비율을 낸다", () => {
    expect(stackParts([["nvidia", 0], ["cloudflare", 3], ["groq", 1]])).toEqual([
      { key: "cloudflare", value: 3, share: 0.75 },
      { key: "groq", value: 1, share: 0.25 },
    ]);
    expect(stackParts([["a", 0]])).toEqual([]);
    expect(maxOf([1, null, 4])).toBe(4);
  });
});
