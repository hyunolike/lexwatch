import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { cx, RADII, SHADOWS, TEXT_SIZES } from "./cx";

const css = readFileSync(path.resolve(__dirname, "../styles/tokens.css"), "utf8");
const declared = (prefix: string) =>
  [...css.matchAll(new RegExp(`--${prefix}-([a-z0-9-]+?)\\s*:`, "g"))].map((m) => m[1]).filter((n) => !n.includes("--"));

describe("cx", () => {
  it("knows every text size declared in tokens.css", () => {
    const sizes = [...new Set(declared("text"))].filter((n) => !n.includes("line-height") && !n.includes("font-weight"));
    expect(new Set(TEXT_SIZES)).toEqual(new Set(sizes));
  });

  it.each(TEXT_SIZES)("keeps text-%s next to a color", (size) => {
    expect(cx(`text-${size}`, "text-ink")).toBe(`text-${size} text-ink`);
  });

  it("later value wins within the same group", () => {
    expect(cx("text-ui", "text-label")).toBe("text-label");
    expect(cx("text-ink", "text-danger")).toBe("text-danger");
    expect(cx("shadow-card", "shadow-pop")).toBe("shadow-pop");
    expect(cx("rounded-md", "rounded-lg")).toBe("rounded-lg");
    expect(cx("p-2", "p-4")).toBe("p-4");
  });

  it("knows shadows and radii", () => {
    for (const s of SHADOWS) expect(cx(`shadow-${s}`, "text-ink")).toBe(`shadow-${s} text-ink`);
    for (const r of RADII) expect(cx(`rounded-${r}`, "p-2")).toBe(`rounded-${r} p-2`);
  });

  it("skips falsy parts", () => {
    expect(cx("a", false, null, undefined, "b")).toBe("a b");
  });
});
