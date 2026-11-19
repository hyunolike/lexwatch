"use client";
import { CheckCheck, Mail, Search, Wrench } from "lucide-react";
import type { ReactNode } from "react";
import { formatDuration, type TimelineItem } from "@/lib/api";
import { groupLabel, statusLabel, stepLabel } from "@/lib/labels";
import { SourceLabel, StatusLozenge } from "@/components/ds/badges";
import { cx } from "@/lib/cx";

function Group({ code }: { code: string | null }) {
  if (!code) return null;
  return <span className="rounded-sm bg-surface-sunken px-1.5 py-0.5 text-micro text-ink-muted">{groupLabel(code)}</span>;
}

function Row({ icon, nested, children }: { icon?: ReactNode; nested?: boolean; children: ReactNode }) {
  return (
    <li className={cx("relative flex gap-3", nested && "ml-6")}>
      <span aria-hidden className="mt-3 flex size-5 shrink-0 items-center justify-center text-ink-subtle">
        {icon ?? <span className="size-2 rounded-full bg-line-strong" />}
      </span>
      <div className="min-w-0 flex-1 rounded-md border border-line bg-surface px-3 py-2">{children}</div>
    </li>
  );
}

/** 실행 타임라인. 단계 상태, 출처, 도구 호출, 크리틱 판정, 승인, 알림을 시간순으로. */
export function Timeline({ items }: { items: TimelineItem[] }) {
  if (items.length === 0) return <p className="text-ui text-ink-subtle">기록된 단계가 없어요.</p>;
  return (
    <ol className="space-y-2">
      {items.map((it, i) => {
        switch (it.kind) {
          case "step":
            return (
              <Row key={i}>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-ui font-semibold text-ink">{stepLabel(it.step)}</span>
                  <Group code={it.groupCode} />
                  <StatusLozenge domain="step" value={it.status} />
                  <SourceLabel kind={it.providerKind} />
                  <span className="ml-auto text-caption text-ink-subtle tabular-nums">
                    시도 {it.attempt} · {formatDuration(it.durationMs)}
                  </span>
                </div>
                {it.errorCode && (
                  <p className="mt-1 text-caption text-danger">
                    <span className="font-mono">{it.errorCode}</span>
                    {it.errorMessage && <span className="text-ink-muted"> — {it.errorMessage}</span>}
                  </p>
                )}
              </Row>
            );
          case "tool_call":
            return (
              <Row key={i} nested icon={<Wrench className="size-3.5" />}>
                <p className="text-ui text-ink">
                  <span className="font-mono text-label">{it.tool}</span>{" "}
                  <span className="text-caption text-ink-subtle">
                    {stepLabel(it.step)} · 시도 {it.attempt}
                  </span>
                </p>
                <p className="text-caption text-ink-muted">{it.resultSummary}</p>
                {it.evidenceIds.length > 0 && (
                  <p className="text-caption text-ink-subtle">근거: {it.evidenceIds.join(", ")}</p>
                )}
              </Row>
            );
          case "finding":
            return (
              <Row key={i} nested icon={<Search className="size-3.5 text-danger" />}>
                <p className="flex flex-wrap items-center gap-2 text-ui text-ink">
                  <span className="font-semibold">크리틱 {it.tier}단</span>
                  <span className="font-mono text-label text-danger">{it.ruleId}</span>
                  <Group code={it.groupCode} />
                </p>
                <p className="text-caption text-ink-muted">{it.message}</p>
              </Row>
            );
          case "approval":
            return (
              <Row key={i} icon={<CheckCheck className="size-3.5" />}>
                <p className="flex flex-wrap items-center gap-2 text-ui text-ink">
                  <span className="font-semibold">결정</span>
                  <StatusLozenge domain="decision" value={it.decision} />
                  <Group code={it.groupCode} />
                  <span className="text-caption text-ink-subtle">{statusLabel("role", it.actorRole).label}</span>
                </p>
                {it.comment && <p className="text-caption text-ink-muted">{it.comment}</p>}
              </Row>
            );
          default:
            return (
              <Row key={i} icon={<Mail className="size-3.5" />}>
                <p className="flex flex-wrap items-center gap-2 text-ui text-ink">
                  <span className="font-semibold">알림</span>
                  <StatusLozenge domain="notification" value={it.status} />
                  <Group code={it.groupCode} />
                </p>
                <p className="text-caption text-ink-muted">{it.message}</p>
              </Row>
            );
        }
      })}
    </ol>
  );
}
