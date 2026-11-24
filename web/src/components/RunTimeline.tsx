"use client";
import { ListChecks, Sparkles, UserCheck, type LucideIcon } from "lucide-react";
import { formatDuration, type TimelineItem, type TrailActor } from "@/lib/api";
import { groupLabel, statusLabel, stepLabel } from "@/lib/labels";
import { GAP_PX, formatGap, layoutTimeline, type Bar } from "@/lib/runTimeline";
import { cx } from "@/lib/cx";

const ROW_H = 36;
const AXIS_H = 28;
const LABEL_W = 176;

const ACTOR: Record<TrailActor, { icon: LucideIcon; label: string; bar: string; badge: string }> = {
  ai: { icon: Sparkles, label: "AI", bar: "bg-info", badge: "border-info/40 bg-info-bg text-info" },
  rule: { icon: ListChecks, label: "규칙", bar: "bg-ink-muted", badge: "border-line-strong bg-surface-sunken text-ink-muted" },
  human: { icon: UserCheck, label: "사람", bar: "bg-success", badge: "border-success/40 bg-success-bg text-success" },
};

const DECISIONS: Record<string, string> = { approve: "승인", reject: "반려", confirm: "파트너 최종 승인", return: "파트너가 돌려보냄" };
const ROLES: Record<string, string> = { lawyer: "변호사", partner: "파트너", admin: "관리자" };
const FAILED_BG = "repeating-linear-gradient(135deg, var(--color-danger) 0 4px, var(--color-danger-bg) 4px 7px)";

const clock = (t: number) =>
  new Date(t).toLocaleTimeString("ko-KR", { timeZone: "Asia/Seoul", hour: "2-digit", minute: "2-digit", second: "2-digit" });

function stateText(b: Bar) {
  if (b.pending) return "대기";
  if (b.status === "Running") return "진행 중";
  return statusLabel("step", b.status).label;
}

/** 막대의 접근 이름: "분석 금융, 23.8초, 성공, 시도 2" */
export function barName(b: Bar): string {
  const dur = b.pending || b.durationMs === null ? "" : `, ${formatDuration(b.durationMs)}`;
  return `${stepLabel(b.step)}${b.groupCode ? ` ${groupLabel(b.groupCode)}` : ""}${dur}, ${stateText(b)}${b.attempt > 1 ? `, 시도 ${b.attempt}` : ""}`;
}

function ActorBadge({ actor }: { actor: TrailActor }) {
  const a = ACTOR[actor];
  const Icon = a.icon;
  return (
    <span className={cx("inline-flex h-[20px] shrink-0 items-center gap-0.5 rounded-sm border px-1 text-micro font-semibold", a.badge)}>
      <Icon aria-hidden className="size-3" />
      {a.label}
    </span>
  );
}

function BarMark({ b }: { b: Bar }) {
  const failed = b.status === "Failed" || b.status === "RetryScheduled";
  return (
    <span
      className={cx(
        "absolute top-1/2 -translate-y-1/2 rounded-sm",
        b.pending ? "h-3 w-3 rotate-45 border-2 border-dashed border-ink-subtle bg-surface" : cx("h-4", ACTOR[b.actor].bar),
        b.status === "NeedsReview" && "ring-2 ring-warning",
        b.status === "Running" && "animate-pulse",
        b.longest && "outline-2 outline-offset-1 outline-ink",
      )}
      style={{ left: b.x, width: b.pending ? undefined : b.w, backgroundImage: failed ? FAILED_BG : undefined }}
    />
  );
}

/** 막대 오른쪽 글: 소요 시간, 상태, 시도. 터치에서도 보이도록 툴팁 대신 늘 보인다. */
function barNote(b: Bar): string {
  const parts: string[] = [];
  if (b.pending) parts.push("대기");
  else if (b.durationMs !== null) parts.push(formatDuration(b.durationMs));
  if (b.status === "Running") parts.push("진행 중");
  if (b.status === "Failed") parts.push(b.errorCode ? `실패 ${b.errorCode}` : "실패");
  if (b.status === "RetryScheduled") parts.push("재시도 대기");
  if (b.status === "NeedsReview") parts.push("확인 필요");
  if (b.attempt > 1) parts.push(`시도 ${b.attempt}`);
  if (b.again) parts.push("다시 시도한 기록");
  if (b.longest) parts.push("가장 오래 걸림");
  return parts.join(" · ");
}

/**
 * 실행 타임라인(ops-visibility OV-02~07, Temporal 타임라인과 Langfuse 워터폴 참고).
 * 단계 기록을 시작 시각과 소요 시간으로 가로 막대로 그리고, 60초 넘는 빈 시간은 접는다.
 * 막대는 버튼이라 키보드로 돌며 툴팁과 접근 이름으로 같은 내용을 읽는다. 글로 된 범례와 병목 문장을 함께 둔다.
 */
export function RunTimeline({ items }: { items: TimelineItem[] }) {
  const l = layoutTimeline(items);
  if (l.rows.length === 0) return <p className="text-ui text-ink-subtle">그릴 단계 기록이 없어요.</p>;
  const plotW = l.width + 24;
  const lines = l.rows.length + (l.decisions.length ? 1 : 0);
  const longestBar = l.rows.flatMap((r) => r.bars).find((b) => b.longest);

  return (
    <div className="space-y-3">
      {longestBar && l.longest && (
        <p className="text-label text-ink-muted">
          가장 오래 걸린 단계: {barName(longestBar)} (단계가 실제로 돈 시간의 {Math.round(l.longest.share * 100)}%)
        </p>
      )}
      <ul className="flex flex-wrap gap-x-4 gap-y-1 text-caption text-ink-muted" aria-label="범례">
        <li className="inline-flex items-center gap-1"><span aria-hidden className="h-2.5 w-4 rounded-sm bg-info" /> AI 단계</li>
        <li className="inline-flex items-center gap-1"><span aria-hidden className="h-2.5 w-4 rounded-sm bg-ink-muted" /> 규칙 단계</li>
        <li className="inline-flex items-center gap-1"><span aria-hidden className="h-2.5 w-4 rounded-sm" style={{ backgroundImage: FAILED_BG }} /> 실패(빗금)</li>
        <li className="inline-flex items-center gap-1"><span aria-hidden className="size-2.5 rotate-45 bg-success" /> 사람 결정</li>
        <li>⋯ 1분 넘게 쉰 시간은 접어서 보여요</li>
      </ul>

      <p className="sr-only">그래프의 내용은 목록 탭에 글로 모두 있어요.</p>
      <div className="overflow-x-auto rounded-lg border border-line bg-surface" aria-hidden>
        <div className="relative" style={{ width: LABEL_W + plotW, height: AXIS_H + lines * ROW_H }}>
          <div className="absolute top-0 border-b border-line text-micro text-ink-subtle" style={{ left: LABEL_W, width: plotW, height: AXIS_H }}>
            <span className="absolute left-0 top-1.5">{clock(l.origin)} 시작</span>
          </div>
          {l.gaps.map((g, i) => (
            <div key={i} className="absolute" style={{ left: LABEL_W + g.x, top: 0, width: GAP_PX, height: AXIS_H + lines * ROW_H }}>
              <span className="absolute left-1/2 top-1.5 -translate-x-1/2 whitespace-nowrap rounded-sm bg-surface px-1 text-micro text-ink-subtle">
                ⋯ {formatGap(g.ms)}
              </span>
              <span
                aria-hidden
                className="absolute inset-x-0 bottom-0"
                style={{ top: AXIS_H, backgroundImage: "repeating-linear-gradient(90deg, transparent 0 5px, var(--color-line) 5px 6px)" }}
              />
            </div>
          ))}
          {l.rows.map((r, i) => (
            <div key={r.key} className="absolute left-0 border-b border-line/60" style={{ top: AXIS_H + i * ROW_H, height: ROW_H, width: LABEL_W + plotW }}>
              <div className="absolute inset-y-0 left-0 flex items-center gap-1.5 bg-surface px-2" style={{ width: LABEL_W }}>
                <ActorBadge actor={r.actor} />
                <span className="truncate text-caption text-ink">
                  {r.label}
                  {r.groupCode && <span className="text-ink-subtle"> {groupLabel(r.groupCode)}</span>}
                </span>
              </div>
              <div className="absolute inset-y-0" style={{ left: LABEL_W, width: plotW }}>
                {r.bars.map((b, j) => (
                  <BarMark key={j} b={b} />
                ))}
                {r.bars.map((b, j) => (
                  <span
                    key={`t${j}`}
                    className="absolute top-1/2 -translate-y-1/2 whitespace-nowrap pl-1.5 text-micro text-ink-muted"
                    style={{ left: b.x + (b.pending ? 12 : b.w) }}
                  >
                    {barNote(b)}
                  </span>
                ))}
              </div>
            </div>
          ))}
          {l.decisions.length > 0 && (
            <div className="absolute left-0" style={{ top: AXIS_H + l.rows.length * ROW_H, height: ROW_H, width: LABEL_W + plotW }}>
              <div className="absolute inset-y-0 left-0 flex items-center gap-1.5 px-2" style={{ width: LABEL_W }}>
                <ActorBadge actor="human" />
                <span className="text-caption text-ink">결정</span>
              </div>
              <ul className="absolute inset-y-0" style={{ left: LABEL_W, width: plotW }}>
                {l.decisions.map((d, i) => (
                  <li key={i} className="absolute top-1/2 flex -translate-y-1/2 items-center gap-1" style={{ left: d.x - 5 }}>
                    <span aria-hidden className="size-2.5 rotate-45 bg-success" />
                    <span className="whitespace-nowrap text-micro text-ink">
                      {ROLES[d.actorRole] ?? d.actorRole} {DECISIONS[d.decision] ?? d.decision}
                      {d.groupCode ? ` ${groupLabel(d.groupCode)}` : ""}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
