import { describe, expect, it } from "vitest";
import { cycle, focusSpot, isComposingEvent, isPaletteKey, shortcutOf, shouldHandle, type KeyLike } from "./shortcuts";

const key = (code: string, over: Partial<KeyLike> = {}): KeyLike => ({
  code,
  key: "x",
  shiftKey: false,
  metaKey: false,
  ctrlKey: false,
  altKey: false,
  repeat: false,
  defaultPrevented: false,
  ...over,
});

describe("shortcutOf", () => {
  it("e.code로 판정해 한글 입력기의 e.key와 상관없다", () => {
    expect(shortcutOf(key("KeyJ", { key: "ㅓ" }))).toBe("next");
    expect(shortcutOf(key("KeyA", { key: "Process" }))).toBe("approve");
    expect(shortcutOf(key("BracketRight"))).toBe("nextFlag");
    expect(shortcutOf(key("Slash", { shiftKey: true }))).toBe("help");
    expect(shortcutOf(key("KeyJ", { shiftKey: true }))).toBeNull();
    expect(shortcutOf(key("KeyZ"))).toBeNull();
  });
});

describe("shouldHandle", () => {
  it("조합 중, 반복, 이미 처리된 키, 수정자는 받지 않는다", () => {
    expect(shouldHandle(key("KeyJ", { isComposing: true }), "body")).toBe(false);
    expect(shouldHandle(key("KeyJ", { keyCode: 229 }), "body")).toBe(false);
    expect(shouldHandle(key("KeyJ", { repeat: true }), "body")).toBe(false);
    expect(shouldHandle(key("KeyJ", { defaultPrevented: true }), "body")).toBe(false);
    expect(shouldHandle(key("KeyJ", { metaKey: true }), "body")).toBe(false);
  });

  it("본문과 main 안의 대상에서만 받는다", () => {
    expect(shouldHandle(key("KeyJ"), "body")).toBe(true);
    expect(shouldHandle(key("KeyJ"), "main-target")).toBe(true);
    expect(shouldHandle(key("KeyJ"), "blocked")).toBe(false);
  });
});

describe("isPaletteKey, isComposingEvent", () => {
  it("⌘K와 Ctrl+K, 조합 중은 아님", () => {
    expect(isPaletteKey(key("KeyK", { metaKey: true }))).toBe(true);
    expect(isPaletteKey(key("KeyK", { ctrlKey: true }))).toBe(true);
    expect(isPaletteKey(key("KeyK"))).toBe(false);
    expect(isPaletteKey(key("KeyK", { metaKey: true, isComposing: true }))).toBe(false);
    expect(isComposingEvent({ nativeEvent: { isComposing: true } })).toBe(true);
  });
});

describe("cycle", () => {
  it("돌아서 이동한다", () => {
    expect(cycle([1, 2, 3], null, 1)).toBe(1);
    expect(cycle([1, 2, 3], null, -1)).toBe(3);
    expect(cycle([1, 2, 3], 3, 1)).toBe(1);
    expect(cycle([1, 2, 3], 1, -1)).toBe(3);
    expect(cycle([], null, 1)).toBeNull();
  });
});

describe("focusSpot", () => {
  /** closest와 matches만 흉내 내는 가짜 요소. 조상 목록을 선택자 문자열로 준다. */
  const fake = (tag: string, self: string[], ancestors: string[]) =>
    ({
      tagName: tag,
      closest: (sel: string) => (sel.split(",").some((x) => ancestors.includes(x.trim()) || self.includes(x.trim())) ? {} : null),
      matches: (sel: string) => sel.split(",").some((x) => self.includes(x.trim())),
    }) as unknown as Element;

  it("본문, main 안 대상, 막힌 곳을 가른다", () => {
    expect(focusSpot(null)).toBe("body");
    expect(focusSpot(fake("BODY", [], []))).toBe("body");
    expect(focusSpot(fake("A", ["a"], ["main"]))).toBe("main-target");
    expect(focusSpot(fake("TEXTAREA", ["textarea"], ["main"]))).toBe("blocked");
    expect(focusSpot(fake("BUTTON", ["button"], ["main", "[role=dialog]"]))).toBe("blocked");
    expect(focusSpot(fake("BUTTON", ["button"], ["header"]))).toBe("blocked");
  });
});
