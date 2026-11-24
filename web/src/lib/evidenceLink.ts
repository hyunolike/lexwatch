import type { EvidenceItem, OldNew } from "./api";

/** 근거 ID "law:001872:제50조", "oldnew:001872:제50조"를 나눈다. 백엔드 ToOldNew의 Split(':', 3)과 같다. */
export function articleOf(id: string): { kind: "law" | "oldnew"; lawId: string; article: string } | null {
  const parts = id.split(":");
  if (parts.length < 3 || (parts[0] !== "law" && parts[0] !== "oldnew")) return null;
  return { kind: parts[0], lawId: parts[1], article: parts.slice(2).join(":") };
}

/**
 * 근거 하나에 이어지는 신구조문(design.md 7절 M5). 조 번호가 같아야 하고,
 * 이 검토의 근거에 신구조문 근거(oldnew:)가 있으면 그 법령 ID와도 같아야 한다(다른 법령의 같은 조 번호를 막는다).
 */
export function oldNewFor(evidenceId: string, evidence: EvidenceItem[], oldNew: OldNew[]): OldNew | null {
  const a = articleOf(evidenceId);
  if (!a) return null;
  const lawIds = new Set(evidence.map((e) => articleOf(e.id)).filter((x) => x?.kind === "oldnew").map((x) => x!.lawId));
  if (lawIds.size > 0 && !lawIds.has(a.lawId)) return null;
  return oldNew.find((o) => o.articleNo === a.article) ?? null;
}

/** 초안이 인용하지 않은 변경 조문. 근거 패널 맨 아래 접힘 구역에 둔다. */
export function uncitedOldNew(cited: string[], evidence: EvidenceItem[], oldNew: OldNew[]): OldNew[] {
  const linked = new Set(cited.map((id) => oldNewFor(id, evidence, oldNew)?.articleNo).filter(Boolean));
  return oldNew.filter((o) => !linked.has(o.articleNo));
}
