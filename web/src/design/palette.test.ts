import { readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * 화면 코드가 원색 클래스를 쓰지 않는지(DS-06). tokens.css가 기본 팔레트를 비우므로
 * 원색 클래스는 CSS가 생성되지 않아 색이 빠진다. 이 테스트는 그런 클래스를 미리 잡는다.
 */
const SRC = path.resolve(__dirname, "..");
const RAW = /\b(bg|text|border|ring|outline|fill|stroke|from|to|via|divide|decoration|shadow)-(slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-\d/;
const BW = /\b(bg|text|border|ring|outline|divide)-(white|black)\b/;

function files(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const p = path.join(dir, name);
    if (statSync(p).isDirectory()) return files(p);
    return /\.(ts|tsx)$/.test(name) && !/\.test\.tsx?$/.test(name) ? [p] : [];
  });
}

describe("원색 클래스 금지 (DS-06)", () => {
  it("src 아래 화면 코드에 원색 클래스와 white/black 클래스가 없다", () => {
    const hits: string[] = [];
    for (const f of files(SRC)) {
      readFileSync(f, "utf8")
        .split("\n")
        .forEach((line, i) => {
          if (RAW.test(line) || BW.test(line)) hits.push(`${path.relative(SRC, f)}:${i + 1}: ${line.trim()}`);
        });
    }
    expect(hits).toEqual([]);
  });

  it("neutral은 팔레트 이름이 아니라 토큰 이름이라 허용한다", () => {
    expect(RAW.test("bg-neutral-bg text-neutral")).toBe(false);
    expect(RAW.test("bg-neutral-100")).toBe(true);
  });
});
