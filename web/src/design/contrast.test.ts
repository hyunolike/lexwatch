import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { CONTRAST_PAIRS, contrastRatio, hsv, parseThemes, PROV_TOKENS, type Theme } from "./tokens";

const css = readFileSync(path.resolve(__dirname, "../styles/tokens.css"), "utf8");
const themes = parseThemes(css);
const THEMES: Theme[] = ["light", "dark"];

describe("parseThemes", () => {
  it("두 테마 블록의 --lw-* 값을 읽는다", () => {
    expect(themes.light.canvas).toBe("#f7f6f3");
    expect(themes.dark.canvas).toBe("#111214");
    expect(themes.dark["prov-ai-bg"]).toBe("#221c3b");
  });

  it("@theme inline의 별칭을 풀어 넣는다", () => {
    expect(themes.light.neutral).toBe(themes.light["ink-muted"]);
    expect(themes.dark["neutral-bg"]).toBe(themes.dark["surface-sunken"]);
  });

  it("두 테마의 색 이름 집합이 같다", () => {
    expect(Object.keys(themes.dark).sort()).toEqual(Object.keys(themes.light).sort());
  });

  it("모든 색 클래스가 --lw- 변수를 가리킨다(테마를 모르는 클래스)", () => {
    const inline = css.slice(css.indexOf("@theme inline"), css.indexOf("@theme {"));
    for (const m of inline.matchAll(/--color-([a-z0-9-]+)\s*:\s*([^;]+);/g)) {
      if (m[1] === "*") continue;
      expect(m[2].trim(), m[1]).toMatch(/^var\(--lw-[a-z0-9-]+\)$/);
    }
  });
});

describe("contrastRatio", () => {
  it("WCAG 상대 휘도 공식", () => {
    expect(contrastRatio("#000000", "#ffffff")).toBeCloseTo(21, 1);
    expect(contrastRatio("#ffffff", "#ffffff")).toBeCloseTo(1, 5);
  });
});

for (const theme of THEMES) {
  describe(`토큰 대비, ${theme === "light" ? "밝은" : "어두운"} 테마 (WCAG 2.2 AA, design.md 2.5절)`, () => {
    for (const { fg, bg, min } of CONTRAST_PAIRS) {
      it(`${fg} / ${bg} ≥ ${min}`, () => {
        const t = themes[theme];
        expect(t[fg], fg).toBeDefined();
        expect(t[bg], bg).toBeDefined();
        expect(contrastRatio(t[fg], t[bg])).toBeGreaterThanOrEqual(min);
      });
    }
  });

  describe(`출처 색은 상태 색 계열이 아니다, ${theme} (DS2-05)`, () => {
    // 빨강, 주황, 노랑, 초록 색상각. 채도 0.3 미만(회색 계열)은 제외.
    const banned = (h: number) => h < 70 || (h >= 90 && h < 170) || h >= 330;
    for (const name of PROV_TOKENS) {
      it(name, () => {
        const { h, s } = hsv(themes[theme][name]);
        if (s >= 0.3) expect(banned(h), `${name} 색상각 ${Math.round(h)}°`).toBe(false);
      });
    }
  });
}

describe("출처 색은 회색이 아니다 (재현 제외, 밝은 테마)", () => {
  for (const name of PROV_TOKENS.filter((n) => n !== "prov-replay")) {
    it(name, () => expect(hsv(themes.light[name]).s).toBeGreaterThanOrEqual(0.3));
  }
});

describe("팔레트", () => {
  it("Tailwind 기본 색 팔레트를 비운다", () => {
    expect(css).toMatch(/--color-\*:\s*initial;/);
  });

  it("두 테마 블록이 color-scheme을 적는다", () => {
    expect(css).toMatch(/:root\s*\{\s*color-scheme:\s*light;/);
    expect(css).toMatch(/\[data-theme="dark"\]\s*\{\s*color-scheme:\s*dark;/);
  });
});
