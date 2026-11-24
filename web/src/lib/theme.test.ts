import { describe, expect, it } from "vitest";
import { parseChoice, resolveTheme, THEME_SCRIPT } from "./theme";

/** THEME_SCRIPT를 가짜 브라우저 위에서 실행하고 <html>에 적힌 data-theme을 돌려준다. */
function run(stored: string | null, systemDark: boolean, opts: { storageThrows?: boolean } = {}) {
  const attrs: Record<string, string> = { "data-theme": "light" };
  const localStorage = {
    getItem: () => {
      if (opts.storageThrows) throw new Error("SecurityError");
      return stored;
    },
  };
  const matchMedia = () => ({ matches: systemDark });
  const document = { documentElement: { setAttribute: (k: string, v: string) => (attrs[k] = v) } };
  new Function("localStorage", "matchMedia", "document", THEME_SCRIPT)(localStorage, matchMedia, document);
  return attrs["data-theme"];
}

describe("THEME_SCRIPT", () => {
  it("저장한 값을 따른다", () => {
    expect(run("dark", false)).toBe("dark");
    expect(run("light", true)).toBe("light");
  });

  it("system이나 모르는 값, 저장 없음은 운영체제 설정을 따른다", () => {
    expect(run("system", true)).toBe("dark");
    expect(run("purple", true)).toBe("dark");
    expect(run(null, false)).toBe("light");
  });

  it("저장소가 예외를 던지면 운영체제 설정을 따른다", () => {
    expect(run("dark", true, { storageThrows: true })).toBe("dark");
    expect(run("dark", false, { storageThrows: true })).toBe("light");
  });
});

describe("parseChoice, resolveTheme", () => {
  it("같은 판정을 한다", () => {
    expect(parseChoice("dark")).toBe("dark");
    expect(parseChoice("x")).toBe("system");
    expect(parseChoice(null)).toBe("system");
    expect(resolveTheme("system", true)).toBe("dark");
    expect(resolveTheme("light", true)).toBe("light");
  });
});
