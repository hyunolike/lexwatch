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

const TOKENS_CSS = readFileSync(path.resolve(SRC, "styles/tokens.css"), "utf8");
const COLORS = new Set([...TOKENS_CSS.matchAll(/--color-([a-z0-9-]+)\s*:/g)].map((m) => m[1]).filter((n) => n !== "*"));
/** 색 토큰 이름의 첫 마디(ink, surface, prov 등). 이 마디로 시작하는 클래스는 색 클래스로 본다. 지운 source도 넣는다. */
const ROOTS = new Set([...[...COLORS].map((n) => n.split("-")[0]), "source"]);
const COLOR_CLASS = /\b(?:bg|text|border(?:-[trblxy])?|ring|outline|fill|stroke|divide|decoration|from|to|via)-([a-z][a-z0-9-]*)(?:\/\d+)?\b/g;

function scan(test: (line: string) => string | null): string[] {
  const hits: string[] = [];
  for (const f of files(SRC)) {
    readFileSync(f, "utf8")
      .split("\n")
      .forEach((line, i) => {
        const why = test(line);
        if (why) hits.push(`${path.relative(SRC, f)}:${i + 1}: ${why}`);
      });
  }
  return hits;
}

describe("화면 코드는 테마를 모른다 (DS2-03)", () => {
  it("dark: 변형과 임의 16진수 색 클래스가 없다", () => {
    // 클래스의 dark: 변형만 잡는다. 객체 키 "dark: Moon"처럼 콜론 뒤가 띄어진 것은 클래스가 아니다.
    expect(scan((l) => (/(?:^|[\s"'`])dark:(?=[a-z[!-])/.test(l) ? "dark:" : /-\[#/.test(l) ? "-[#" : null))).toEqual([]);
  });

  it("tokens.css에 없는 색 이름을 쓰지 않는다(지운 source-* 포함)", () => {
    expect(
      scan((l) => {
        for (const m of l.matchAll(COLOR_CLASS)) {
          const name = m[1];
          if (ROOTS.has(name.split("-")[0]) && !COLORS.has(name)) return m[0];
        }
        return null;
      }),
    ).toEqual([]);
  });

  it("포커스 링을 버튼에 붙이는 클래스가 없다(링은 늘 2px 띄운다)", () => {
    expect(scan((l) => (/\b(outline-offset-0|ring-inset)\b/.test(l) ? "outline-offset-0/ring-inset" : null))).toEqual([]);
  });

  it("색 이름 판정 예시", () => {
    const names = (s: string) => [...s.matchAll(COLOR_CLASS)].map((m) => m[1]);
    expect(names("bg-surface-raised text-ui text-ink/80 border-line")).toEqual(["surface-raised", "ui", "ink", "line"]);
    expect(COLORS.has("source-nvidia")).toBe(false);
  });
});
