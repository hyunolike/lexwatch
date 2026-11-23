import { describe, expect, it } from "vitest";
import { EXPECTED_MS, SNORES, TIPS, elapsedText, isSlow, nextTip, snoreFor, steps, timeFraction, waitHint } from "./wake";

describe("steps", () => {
  it("실제 응답으로만 완료", () => {
    expect(steps(false, false)).toEqual(["active", "todo"]);
    expect(steps(true, false)).toEqual(["done", "active"]);
    expect(steps(true, true)).toEqual(["done", "done"]);
  });
  it("데이터가 /health보다 먼저 와도 모두 완료", () => {
    expect(steps(false, true)).toEqual(["done", "done"]);
  });
});

describe("시간 막대와 경과", () => {
  it.each([
    [-5, 0],
    [0, 0],
    [Number.NaN, 0],
    [30_000, 0.5],
    [EXPECTED_MS, 1],
    [90_000, 1],
  ])("%s ms → %s", (ms, f) => expect(timeFraction(ms)).toBe(f));

  it("60초부터 느림 문장", () => {
    expect(isSlow(59_999)).toBe(false);
    expect(waitHint(59_999)).toBe("보통 30초에서 1분 걸려요.");
    expect(isSlow(60_000)).toBe(true);
    expect(waitHint(60_000)).toMatch(/^평소보다 오래/);
  });

  it.each([
    [-1, "0초째"],
    [12_900, "12초째"],
    [59_999, "59초째"],
    [60_000, "1분째"],
    [75_400, "1분 15초째"],
  ])("%s ms → %s", (ms, t) => expect(elapsedText(ms)).toBe(t));
});

describe("렉스와 이야기 카드", () => {
  it("누르기 전 안내, 이후 잠꼬대가 돈다", () => {
    expect(snoreFor(0)).toBe("렉스를 눌러 보세요");
    expect(snoreFor(1)).toBe(SNORES[0]);
    expect(snoreFor(SNORES.length + 1)).toBe(SNORES[0]);
  });
  it("잠꼬대에 진행이나 시간처럼 읽히는 말이 없다", () => {
    for (const s of SNORES) expect(s).not.toMatch(/일어났|깼|[0-9]+\s*분|빨라/);
  });
  it("카드는 양쪽으로 돈다", () => {
    expect(nextTip(TIPS.length - 1)).toBe(0);
    expect(nextTip(0, -1)).toBe(TIPS.length - 1);
  });
});
