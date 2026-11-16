"use client";
import { formatDuration, stepLabel, type TimelineItem } from "@/lib/api";
import { ProviderBadge } from "@/components/ProviderBadge";

function StatusPill({ status }: { status: string }) {
  const cls =
    status === "Failed" ? "bg-red-100 text-red-800" : status === "Succeeded" ? "bg-green-100 text-green-800" : "bg-slate-200 text-slate-700";
  return <span className={`rounded px-2 py-0.5 text-xs ${cls}`}>{status}</span>;
}

export function Timeline({
  items,
}: {
  items: TimelineItem[];
}) {
  if (items.length === 0) return <p className="text-slate-500">기록된 단계가 없어요.</p>;
  return (
    <ol className="space-y-1">
      {items.map((it, i) => {
        const group = it.groupCode ? <span className="rounded bg-slate-100 px-1.5 text-xs">{it.groupCode}</span> : null;
        if (it.kind === "step") {
          return (
            <li key={i} className="flex flex-wrap items-center gap-3 rounded border bg-white p-2">
              <span className="font-medium">{stepLabel(it.step)}</span>
              {group}
              <span className="text-sm text-slate-500">시도 {it.attempt}</span>
              <span className="text-sm text-slate-500">{formatDuration(it.durationMs)}</span>
              <StatusPill status={it.status} />
              <ProviderBadge kind={it.providerKind} />
              {it.errorCode && (
                <span className="text-sm text-red-700" title={it.errorMessage ?? undefined}>
                  {it.errorCode}
                </span>
              )}
            </li>
          );
        }
        if (it.kind === "tool_call")
          return (
            <li key={i} className="ml-8 rounded border-l-4 border-slate-300 bg-white p-2 text-sm">
              <span className="font-mono">{it.tool}</span> <span className="text-slate-500">({stepLabel(it.step)} · 시도 {it.attempt})</span>
              <p className="text-slate-600">{it.resultSummary}</p>
              {it.evidenceIds.length > 0 && <p className="text-xs text-slate-500">근거: {it.evidenceIds.join(", ")}</p>}
            </li>
          );
        if (it.kind === "finding")
          return (
            <li key={i} className="ml-8 rounded border-l-4 border-orange-300 bg-white p-2 text-sm">
              <span className="font-semibold">{it.tier}단</span> <span className="font-mono">{it.ruleId}</span> {group}
              <p className="text-slate-600">{it.message}</p>
            </li>
          );
        if (it.kind === "approval")
          return (
            <li key={i} className="rounded border bg-white p-2 text-sm">
              <span className="font-medium">결정: {it.decision}</span> {group} <span className="text-slate-500">({it.actorRole})</span>
              {it.comment && <p className="text-slate-600">{it.comment}</p>}
            </li>
          );
        return (
          <li key={i} className="rounded border bg-white p-2 text-sm">
            <span className="font-medium">알림 {it.status}</span> {group}
            <p className="text-slate-600">{it.message}</p>
          </li>
        );
      })}
    </ol>
  );
}
