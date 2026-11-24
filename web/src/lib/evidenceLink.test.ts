import { describe, expect, it } from "vitest";
import type { EvidenceItem, OldNew } from "./api";
import { articleOf, oldNewFor, uncitedOldNew } from "./evidenceLink";

const on = (articleNo: string): OldNew => ({ articleNo, before: "a", after: "b", beforeMissing: false, afterMissing: false, oldMarks: null, newMarks: null });
const ev = (id: string): EvidenceItem => ({ id, kind: "law", title: id, sourceUrl: null });
const oldNew = [on("제50조"), on("제53조")];

describe("evidenceLink", () => {
  it("근거 ID를 나눈다", () => {
    expect(articleOf("law:001872:제50조")).toEqual({ kind: "law", lawId: "001872", article: "제50조" });
    expect(articleOf("bill:1:2")).toBeNull();
    expect(articleOf("law:001872")).toBeNull();
  });

  it("같은 조 번호의 신구조문을 잇는다(law:와 oldnew: 모두)", () => {
    const evidence = [ev("law:001872:제50조"), ev("oldnew:001872:제50조")];
    expect(oldNewFor("law:001872:제50조", evidence, oldNew)?.articleNo).toBe("제50조");
    expect(oldNewFor("oldnew:001872:제50조", evidence, oldNew)?.articleNo).toBe("제50조");
  });

  it("다른 법령의 같은 조 번호는 잇지 않는다", () => {
    const evidence = [ev("oldnew:001872:제50조"), ev("law:009999:제50조")];
    expect(oldNewFor("law:009999:제50조", evidence, oldNew)).toBeNull();
  });

  it("신구조문 근거가 없으면 조 번호만으로 잇는다", () => {
    expect(oldNewFor("law:001872:제53조", [ev("law:001872:제53조")], oldNew)?.articleNo).toBe("제53조");
  });

  it("인용하지 않은 변경 조문을 고른다", () => {
    const evidence = [ev("oldnew:001872:제50조")];
    expect(uncitedOldNew(["law:001872:제50조"], evidence, oldNew).map((o) => o.articleNo)).toEqual(["제53조"]);
  });
});
