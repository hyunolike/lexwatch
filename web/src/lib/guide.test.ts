import { describe, expect, it } from "vitest";
import { INITIAL_GUIDE, markDone, parseGuide, progress, roleSentence, stepForVisit } from "./guide";

describe("parseGuide", () => {
  it.each([null, "", "{", "null", "[]", '"x"', '{"v":2,"dismissed":true,"done":[]}', '{"v":1,"dismissed":"yes","done":[]}', '{"v":1,"dismissed":true}'])(
    "%s → 처음 상태",
    (raw) => expect(parseGuide(raw)).toEqual(INITIAL_GUIDE),
  );

  it("모르는 단계와 중복을 버리고 순서를 맞춘다", () => {
    expect(parseGuide('{"v":1,"dismissed":true,"done":["approve","x",3,"open-review","approve"]}')).toEqual({
      v: 1,
      dismissed: true,
      done: ["open-review", "approve"],
    });
  });
});

describe("stepForVisit", () => {
  it.each([
    ["/reviews/abc", "lawyer", "open-review"],
    ["/reviews/abc/", "admin", "open-review"],
    ["/inbox", "lawyer", null],
    ["/reviews/", "lawyer", null],
    ["/reviews/abc/edit", "lawyer", null],
    ["/runs/abc/", "admin", "admin-run"],
    ["/runs/abc", "admin", "admin-run"],
    ["/runs/abc", "lawyer", null],
    ["/runs", "admin", null],
    ["/reviews", "lawyer", null],
    ["/", "admin", null],
    ["/design-system", "lawyer", null],
  ] as const)("%s (%s) → %s", (path, role, step) => expect(stepForVisit(path, role)).toBe(step));
});

describe("markDone, progress", () => {
  it("완료를 더하고 순서를 유지한다", () => {
    const s = markDone(markDone(INITIAL_GUIDE, "admin-run"), "open-review");
    expect(s.done).toEqual(["open-review", "admin-run"]);
    expect(progress(s)).toEqual({ done: 2, total: 3 });
  });

  it("이미 완료면 같은 객체", () => {
    const s = markDone(INITIAL_GUIDE, "approve");
    expect(markDone(s, "approve")).toBe(s);
  });
});

describe("roleSentence", () => {
  it("describes the role actually selected", () => {
    expect(roleSentence("partner")).toContain("파트너 역할");
    expect(roleSentence("lawyer")).toContain("변호사 역할");
    expect(roleSentence("admin")).toContain("관리자 역할");
  });
});
