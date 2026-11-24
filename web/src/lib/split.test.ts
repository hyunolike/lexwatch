import { describe, expect, it } from "vitest";
import { clampRatio, ratioFromKey, ratioFromPointer, SPLIT } from "./split";

describe("split", () => {
  it("최소와 최대로 자른다", () => {
    expect(clampRatio(10)).toBe(SPLIT.min);
    expect(clampRatio(99)).toBe(SPLIT.max);
    expect(ratioFromPointer(500, 1000)).toBe(50);
    expect(ratioFromPointer(5, 0)).toBe(SPLIT.initial);
  });
  it("키로 움직인다", () => {
    expect(ratioFromKey(58, "ArrowLeft", false)).toBe(56);
    expect(ratioFromKey(58, "ArrowRight", true)).toBe(68);
    expect(ratioFromKey(68, "ArrowRight", true)).toBe(70);
    expect(ratioFromKey(58, "Home", false)).toBe(40);
    expect(ratioFromKey(58, "End", false)).toBe(70);
    expect(ratioFromKey(58, "Enter", false)).toBeNull();
  });
});
