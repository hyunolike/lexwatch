import { describe, expect, it } from "vitest";
import type { OldNew } from "./api";
import { redline, type Part } from "./redline";

const item = (before: string, after: string, extra: Partial<OldNew> = {}): OldNew => ({
  articleNo: "제1조",
  before,
  after,
  beforeMissing: false,
  afterMissing: false,
  oldMarks: null,
  newMarks: null,
  ...extra,
});

const changed = (parts: Part[], kind: Part["kind"]) => parts.filter((p) => p.kind === kind).map((p) => p.text);

describe("redline: 공식 변경 표시", () => {
  it("구간을 그대로 삭제와 추가로 나눈다", () => {
    const r = redline(item("기소한 검찰청의 검사", "기소한 공소청의 검사", { oldMarks: [[4, 7]], newMarks: [[4, 7]] }));
    expect(r.status).toBe("diff");
    if (r.status !== "diff") return;
    expect(r.source).toBe("official");
    expect(changed(r.old, "del")).toEqual(["검찰청"]);
    expect(changed(r.new, "ins")).toEqual(["공소청"]);
    expect(r.old.map((p) => p.text).join("")).toBe("기소한 검찰청의 검사");
    expect(r.changes).toBe(1);
  });

  it("글 길이를 넘는 구간은 잘라 낸다", () => {
    const r = redline(item("가나다", "가나다라", { oldMarks: [], newMarks: [[3, 99]] }));
    if (r.status !== "diff") throw new Error(r.status);
    expect(changed(r.new, "ins")).toEqual(["라"]);
  });

  it("두 구간 목록이 비었는데 글이 다르면 화면 계산으로 넘어간다", () => {
    const r = redline(item("40시간", "52시간", { oldMarks: [], newMarks: [] }));
    expect(r.status === "diff" && r.source).toBe("computed");
  });
});

describe("redline: 화면 계산", () => {
  it("한글 조사 변경은 조사만 바뀐 것으로 나온다", () => {
    const r = redline(item("근로자는 1주 40시간을 초과할 수 없다.", "근로자가 1주 52시간을 초과할 수 없다."));
    if (r.status !== "diff") throw new Error(r.status);
    expect(r.source).toBe("computed");
    expect(changed(r.old, "del")).toEqual(["는", "40"]);
    expect(changed(r.new, "ins")).toEqual(["가", "52"]);
    expect(r.changes).toBe(2);
  });

  it("변경이 너무 많으면 tooLong", () => {
    const a = Array.from({ length: 900 }, (_, i) => `가${i}`).join(" ");
    const b = Array.from({ length: 900 }, (_, i) => `나${i}`).join(" ");
    expect(redline(item(a, b)).status).toBe("tooLong");
  });

  it("같으면 same", () => {
    expect(redline(item("같은 조문", "같은 조문")).status).toBe("same");
  });
});

describe("redline: 한쪽이 없는 조문", () => {
  it("구조문이 없으면 신설", () => {
    expect(redline(item("", "새 조문", { beforeMissing: true })).status).toBe("added");
    expect(redline(item("(없음)", "새 조문")).status).toBe("added");
  });

  it("신조문이 없으면 삭제", () => {
    expect(redline(item("옛 조문", "(없음)", { afterMissing: true })).status).toBe("removed");
  });

  it("둘 다 없으면 empty", () => {
    expect(redline(item("", "  ")).status).toBe("empty");
  });
});
