import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { CONTRAST_PAIRS, contrastRatio, hsv, parseColorTokens, SOURCE_TOKENS } from "./tokens";

const css = readFileSync(path.resolve(__dirname, "../styles/tokens.css"), "utf8");
const tokens = parseColorTokens(css);

describe("parseColorTokens", () => {
  it("@theme의 --color-* 값을 읽는다", () => {
    expect(tokens.canvas).toBe("#f6f7f9");
    expect(tokens["source-nvidia-bg"]).toBe("#efebfc");
  });

  it("기본 팔레트를 비우는 선언은 값으로 읽지 않는다", () => {
    expect(Object.keys(tokens)).not.toContain("*");
  });
});

describe("contrastRatio", () => {
  it("WCAG 상대 휘도 공식", () => {
    expect(contrastRatio("#000000", "#ffffff")).toBeCloseTo(21, 1);
    expect(contrastRatio("#ffffff", "#ffffff")).toBeCloseTo(1, 5);
  });
});

describe("토큰 대비 (WCAG 2.2 AA, design.md 2.2절)", () => {
  for (const { fg, bg, min } of CONTRAST_PAIRS) {
    it(`${fg} / ${bg} ≥ ${min}`, () => {
      expect(tokens[fg], fg).toBeDefined();
      expect(tokens[bg], bg).toBeDefined();
      expect(contrastRatio(tokens[fg], tokens[bg])).toBeGreaterThanOrEqual(min);
    });
  }
});

describe("출처 색은 상태 색 계열이 아니다 (DS-03)", () => {
  // 빨강, 주황, 노랑, 초록 색상각. 채도 0.3 미만(회색 계열)은 제외.
  const banned = (h: number) => h < 70 || (h >= 90 && h < 170) || h >= 330;
  for (const name of SOURCE_TOKENS) {
    it(name, () => {
      const { h, s } = hsv(tokens[name]);
      if (s >= 0.3) expect(banned(h), `${name} 색상각 ${Math.round(h)}°`).toBe(false);
    });
  }
});

describe("팔레트", () => {
  it("Tailwind 기본 색 팔레트를 비운다", () => {
    expect(css).toMatch(/--color-\*:\s*initial;/);
  });
});
