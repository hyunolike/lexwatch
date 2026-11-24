import type { TimelineItem, TrailActor } from "./api";
import { stepLabel } from "./labels";

/**
 * 실행 타임라인 배치(ops-visibility OV-02~07, 설계 1절). Temporal의 이벤트 타임라인과 Langfuse 워터폴을 참고했다.
 * 단계 기록 한 행에는 마지막 시도의 시작 시각(StartedAt)과 소요 시간만 남는다(StepQueue가 같은 행의 Attempt를 올린다).
 * 관리자 다시 실행은 새 행이라 같은 줄에 막대가 하나 더 놓인다.
 */

export const STEP_ORDER = ["fetch_articles", "classify", "index_articles", "analyze", "critic_citation", "critic_source", "critic_judge", "notify"];

const STEP_ACTOR: Record<string, TrailActor> = {
  fetch_articles: "rule",
  classify: "ai",
  index_articles: "ai",
  analyze: "ai",
  critic_citation: "rule",
  critic_source: "rule",
  critic_judge: "ai",
  notify: "rule",
};

/** 대체 구현은 AI가 아니다(ai-footprints AF-06). 재현은 담당을 바꾸지 않는다. */
export function stepActor(step: string, providerKind: string | null | undefined): TrailActor {
  const base = STEP_ACTOR[step] ?? "rule";
  return base === "ai" && providerKind === "fallback" ? "rule" : base;
}

export const GAP_FOLD_MS = 60_000;
export const GAP_PX = 28;
export const MIN_BAR_PX = 3;

type StepItem = Extract<TimelineItem, { kind: "step" }>;
type ApprovalItem = Extract<TimelineItem, { kind: "approval" }>;

export interface Bar {
  step: string;
  groupCode: string | null;
  status: string;
  attempt: number;
  actor: TrailActor;
  providerKind: string | null;
  errorCode: string | null;
  start: number;
  durationMs: number | null;
  /** 화면 x(px)와 폭 */
  x: number;
  w: number;
  /** 아직 시작하지 않음(Pending, 예정 시각에 빈 표시) */
  pending: boolean;
  /** 같은 줄의 두 번째 이후 기록(자동 재시도 기록이 따로 있는 재현 데이터, 관리자 다시 실행) */
  again: boolean;
  longest?: boolean;
}

export interface Row {
  key: string;
  step: string;
  label: string;
  groupCode: string | null;
  actor: TrailActor;
  bars: Bar[];
}

export interface Gap {
  x: number;
  ms: number;
}

export interface Decision {
  x: number;
  at: number;
  decision: string;
  actorRole: string;
  groupCode: string | null;
}

export interface Layout {
  rows: Row[];
  gaps: Gap[];
  decisions: Decision[];
  width: number;
  origin: number;
  busyMs: number;
  /** 끝난 막대 구간의 합집합 길이(병목 비율의 분모) */
  workMs: number;
  /** 가장 오래 걸린 막대와 workMs 중 비율 */
  longest: { step: string; groupCode: string | null; durationMs: number; share: number } | null;
}

const ms = (iso: string) => Date.parse(iso);

/** 바쁜 구간을 합친다. 입력은 [start, end]. */
function merge(spans: [number, number][]): [number, number][] {
  const sorted = [...spans].sort((a, b) => a[0] - b[0]);
  const out: [number, number][] = [];
  for (const [s, e] of sorted) {
    const last = out[out.length - 1];
    if (last && s <= last[1] + GAP_FOLD_MS) last[1] = Math.max(last[1], e);
    else out.push([s, e]);
  }
  return out;
}

/**
 * 상태별 막대(설계 1절, 검토 1차 반영)
 * - Pending: 막대 없이 예정 시각에 빈 표시
 * - Running: 남아 있는 durationMs는 앞 시도의 값이라 무시하고, 지금까지 지난 시간으로 그린다
 * - RetryScheduled: 마지막 시도 막대(실패 톤)와 "재시도 대기". 다음 예정 시각은 응답에 없어 그리지 않는다
 */
export function layoutTimeline(items: TimelineItem[], targetWidth = 720, now = Date.now()): Layout {
  const steps = items.filter((i): i is StepItem => i.kind === "step");
  const approvals = items.filter((i): i is ApprovalItem => i.kind === "approval");

  const durationOf = (s: StepItem): number | null =>
    s.status === "Pending" ? null : s.status === "Running" ? Math.max(now - ms(s.at), 0) : s.durationMs;
  const spans: [number, number][] = [];
  const work: [number, number][] = [];
  for (const s of steps) {
    const start = ms(s.at);
    const d = Math.max(durationOf(s) ?? 0, 0);
    spans.push([start, start + d]);
    if (["Succeeded", "Failed", "NeedsReview", "RetryScheduled"].includes(s.status) && d > 0) work.push([start, start + d]);
  }
  for (const a of approvals) spans.push([ms(a.at), ms(a.at)]);
  if (spans.length === 0) return { rows: [], gaps: [], decisions: [], width: 0, origin: 0, busyMs: 0, workMs: 0, longest: null };
  const workMs = work
    .sort((a, b) => a[0] - b[0])
    .reduce<[number, number][]>((acc, [s, e]) => {
      const last = acc[acc.length - 1];
      if (last && s <= last[1]) last[1] = Math.max(last[1], e);
      else acc.push([s, e]);
      return acc;
    }, [])
    .reduce((sum, [s, e]) => sum + (e - s), 0);

  // 60초 넘게 아무 일도 없던 구간은 접는다(OV-05)
  const busy = merge(spans);
  const busyMs = busy.reduce((sum, [s, e]) => sum + (e - s), 0);
  const scale = targetWidth / Math.max(busyMs, 1000);
  const origin = busy[0][0];
  const gaps: Gap[] = [];
  const offsets: { start: number; end: number; x: number }[] = [];
  let x = 0;
  busy.forEach(([s, e], i) => {
    if (i > 0) {
      gaps.push({ x, ms: s - busy[i - 1][1] });
      x += GAP_PX;
    }
    offsets.push({ start: s, end: e, x });
    x += (e - s) * scale;
  });
  const toX = (t: number) => {
    const seg = offsets.find((o) => t >= o.start && t <= o.end) ?? offsets[offsets.length - 1];
    return seg.x + (Math.min(Math.max(t, seg.start), seg.end) - seg.start) * scale;
  };

  // 줄: (단계, 그룹)마다 하나. 가장 이른 시작 순, 같으면 단계 순서
  const byKey = new Map<string, Row>();
  for (const s of steps) {
    const key = `${s.step}|${s.groupCode ?? ""}`;
    const start = ms(s.at);
    const pending = s.status === "Pending";
    const dur = durationOf(s);
    const bx = toX(start);
    const bar: Bar = {
      step: s.step,
      groupCode: s.groupCode,
      status: s.status,
      attempt: s.attempt,
      actor: stepActor(s.step, s.providerKind),
      providerKind: s.providerKind && s.providerKind !== "none" ? s.providerKind : null,
      errorCode: s.errorCode,
      start,
      durationMs: dur,
      x: bx,
      w: pending ? 0 : Math.max(toX(start + Math.max(dur ?? 0, 0)) - bx, MIN_BAR_PX),
      pending,
      again: false,
    };
    const row = byKey.get(key);
    if (row) {
      row.bars.push(bar);
      if (bar.actor === "ai") row.actor = "ai";
    } else
      byKey.set(key, { key, step: s.step, label: stepLabel(s.step), groupCode: s.groupCode, actor: bar.actor, bars: [bar] });
  }
  const order = (step: string) => (STEP_ORDER.includes(step) ? STEP_ORDER.indexOf(step) : STEP_ORDER.length);
  const rows = [...byKey.values()].sort((a, b) => {
    const sa = Math.min(...a.bars.map((x) => x.start));
    const sb = Math.min(...b.bars.map((x) => x.start));
    return sa - sb || order(a.step) - order(b.step) || (a.groupCode ?? "").localeCompare(b.groupCode ?? "");
  });
  for (const r of rows) {
    r.bars.sort((a, b) => a.start - b.start);
    r.bars.forEach((b, i) => (b.again = i > 0));
  }

  // 병목(OV-04): 끝난 막대 중 가장 오래 걸린 것
  let longest: Layout["longest"] = null;
  let longestBar: Bar | null = null;
  for (const r of rows)
    for (const b of r.bars)
      if (!b.pending && b.durationMs !== null && ["Succeeded", "Failed", "NeedsReview"].includes(b.status) && (!longestBar || b.durationMs > (longestBar.durationMs ?? 0)))
        longestBar = b;
  if (longestBar) {
    longestBar.longest = true;
    longest = {
      step: longestBar.step,
      groupCode: longestBar.groupCode,
      durationMs: longestBar.durationMs!,
      share: workMs > 0 ? Math.min(longestBar.durationMs! / workMs, 1) : 0,
    };
  }

  const decisions: Decision[] = approvals
    .map((a) => ({ x: toX(ms(a.at)), at: ms(a.at), decision: a.decision, actorRole: a.actorRole, groupCode: a.groupCode }))
    .sort((a, b) => a.at - b.at);

  return { rows, gaps, decisions, width: Math.ceil(x), origin, busyMs, workMs, longest };
}

/** "32분", "2시간 5분", "3일 4시간" */
export function formatGap(msValue: number): string {
  const m = Math.round(msValue / 60_000);
  if (m < 60) return `${m}분`;
  const h = Math.floor(m / 60);
  if (h < 24) return m % 60 ? `${h}시간 ${m % 60}분` : `${h}시간`;
  const d = Math.floor(h / 24);
  return h % 24 ? `${d}일 ${h % 24}시간` : `${d}일`;
}
