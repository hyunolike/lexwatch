/**
 * 신구조문 대조. 설계: docs/features/design-system/design.md 5.1절
 * 법제처 공식 변경 표시가 있으면 그것을 쓰고, 없으면 화면에서 단어 단위로 비교한다.
 */
import { diffWordsWithSpace } from "diff";
import type { MarkRange, OldNew } from "./api";

export type Part = { kind: "same" | "del" | "ins"; text: string };
export type Redline =
  | { status: "diff"; source: "official" | "computed"; old: Part[]; new: Part[]; changes: number }
  | { status: "same" }
  | { status: "added" }
  | { status: "removed" }
  | { status: "empty" }
  | { status: "tooLong" };

/** 화면 계산의 상한. 넘으면 하이라이트를 포기해 화면이 멈추지 않게 한다. */
const MAX_EDIT_LENGTH = 400;
const MISSING = "(없음)";

const isMissing = (text: string, flag: boolean) => flag || text.trim() === "" || text.trim() === MISSING;

/** 구간으로 글을 나눈다. 구간 밖은 same, 안은 kind. 겹치거나 글을 넘는 구간은 잘라 낸다. */
function split(text: string, marks: MarkRange[], kind: "del" | "ins"): Part[] {
  const parts: Part[] = [];
  let at = 0;
  for (const [s0, e0] of [...marks].sort((a, b) => a[0] - b[0])) {
    const s = Math.max(at, Math.min(s0, text.length));
    const e = Math.min(Math.max(e0, s), text.length);
    if (s > at) parts.push({ kind: "same", text: text.slice(at, s) });
    if (e > s) parts.push({ kind, text: text.slice(s, e) });
    at = Math.max(at, e);
  }
  if (at < text.length) parts.push({ kind: "same", text: text.slice(at) });
  return parts;
}

const runs = (parts: Part[], kind: Part["kind"]) => parts.filter((p) => p.kind === kind).length;

export function redline(item: OldNew): Redline {
  const noOld = isMissing(item.before, item.beforeMissing);
  const noNew = isMissing(item.after, item.afterMissing);
  if (noOld && noNew) return { status: "empty" };
  if (noOld) return { status: "added" };
  if (noNew) return { status: "removed" };

  const hasOfficial =
    (Array.isArray(item.oldMarks) || Array.isArray(item.newMarks)) &&
    ((item.oldMarks?.length ?? 0) > 0 || (item.newMarks?.length ?? 0) > 0);
  if (hasOfficial) {
    const old = split(item.before, item.oldMarks ?? [], "del");
    const neu = split(item.after, item.newMarks ?? [], "ins");
    const changes = Math.max(runs(old, "del"), runs(neu, "ins"));
    return changes === 0 ? { status: "same" } : { status: "diff", source: "official", old, new: neu, changes };
  }

  if (item.before === item.after) return { status: "same" };
  const diff = diffWordsWithSpace(item.before, item.after, { maxEditLength: MAX_EDIT_LENGTH });
  if (!diff) return { status: "tooLong" };
  const old: Part[] = [];
  const neu: Part[] = [];
  for (const c of diff) {
    if (c.added) neu.push({ kind: "ins", text: c.value });
    else if (c.removed) old.push({ kind: "del", text: c.value });
    else {
      old.push({ kind: "same", text: c.value });
      neu.push({ kind: "same", text: c.value });
    }
  }
  const changes = Math.max(runs(old, "del"), runs(neu, "ins"));
  return changes === 0 ? { status: "same" } : { status: "diff", source: "computed", old, new: neu, changes };
}
