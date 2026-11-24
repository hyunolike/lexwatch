import type { BillStage, RadarBill } from "./api";

/** 걸음 표시 칸(bill-radar BR-08). */
export const BILL_STEPS = ["접수", "위원회", "법사위", "본회의", "의결"] as const;

const STEP_OF: Record<BillStage, number | null> = {
  Received: 0,
  Committee: 1,
  Judiciary: 2,
  Plenary: 3,
  Passed: 4,
  Merged: 4,
  Closed: null,
};

/** 현재 칸(0~4). 종료는 걸음 대신 처리 결과를 보이므로 null. 모르는 값도 null. */
export function stageStep(stage: string): number | null {
  return stage in STEP_OF ? STEP_OF[stage as BillStage] : null;
}

export const RECENT_DAYS = 7;
const DAY_MS = 24 * 60 * 60 * 1000;

/** "YYYY-MM-DD"를 KST 자정으로 본다. */
function kstDay(date: string): number {
  return Date.parse(`${date}T00:00:00+09:00`);
}

export type BillBadge = "new" | "changed";

/** 새 발의는 발의일 기준, 단계 바뀜은 최근 사건 기준 7일 안(BR-08). */
export function billBadges(bill: Pick<RadarBill, "proposedOn" | "lastEvent">, now: number): BillBadge[] {
  const out: BillBadge[] = [];
  const proposed = kstDay(bill.proposedOn);
  if (!Number.isNaN(proposed) && now - proposed < RECENT_DAYS * DAY_MS) out.push("new");
  const e = bill.lastEvent;
  if (e?.kind === "stage_changed" && now - Date.parse(e.at) < RECENT_DAYS * DAY_MS) out.push("changed");
  return out;
}

/** 그룹 탭으로 거른다. "all"이면 그대로. */
export function billsOfGroup<T extends Pick<RadarBill, "groupCodes">>(bills: T[], group: string): T[] {
  return group === "all" ? bills : bills.filter((b) => b.groupCodes.includes(group));
}

/** "2026-11-23" → "2026. 11. 23." */
export function formatDay(date: string | null): string {
  if (!date) return "";
  const [y, m, d] = date.split("-").map(Number);
  return `${y}. ${m}. ${d}.`;
}

export const RADAR_FULL_LIMIT = 1000;

/** 탭 숫자. 서버가 센 전체 건수가 있으면 그것을, 없으면(옛 응답) 받은 목록의 건수를 쓴다. */
export function tabCount(
  totals: { all: number; byGroup: Record<string, number> } | undefined,
  group: string,
  received: number,
): number {
  if (!totals) return received;
  return group === "all" ? totals.all : (totals.byGroup[group] ?? received);
}

/** 목록이 잘렸으면 안내할 문장. 잘리지 않았으면 null. */
export function truncationNote(totals: { all: number; shown: number } | undefined): string | null {
  if (!totals || totals.shown >= totals.all) return null;
  return `최근 움직임 순으로 ${totals.shown}건만 보여요. 전체는 ${totals.all}건이에요.`;
}
