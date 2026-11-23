"use client";
import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { apiFetch, type RunSummary } from "@/lib/api";
import { groupLabel } from "@/lib/labels";
import { SourceLabel, StatusLozenge } from "@/components/ds/badges";
import { Callout } from "@/components/ds/Callout";
import { CARD, PageHeader } from "@/components/ds/layout";
import { useRole } from "@/components/RoleSwitch";
import { WakeGate } from "@/components/WakeGate";

export default function RunsPage() {
  const role = useRole();
  return (
    <div className="space-y-5">
      <PageHeader title="실행 이력" />
      <WakeGate<RunSummary[]> key={role} label="자동 처리 기록" load={() => apiFetch<RunSummary[]>("/api/runs?limit=50")}>
        {(res) =>
          !Array.isArray(res.data) ? (
            <Callout tone="danger">목록을 불러오지 못했어요.</Callout>
          ) : res.data.length === 0 ? (
            <p className="text-ui text-ink-subtle">실행 기록이 없어요.</p>
          ) : (
            <ul className={`${CARD} divide-y divide-line`}>
              {res.data.map((r) => (
                <li key={r.id}>
                  <Link
                    href={`/runs/${r.id}`}
                    className="group flex flex-wrap items-center gap-x-3 gap-y-2 px-4 py-3 transition-colors duration-[120ms] hover:bg-surface-sunken"
                  >
                    <span className="min-w-0 flex-1 basis-60">
                      <span className="block truncate text-ui font-semibold text-ink">{r.lawName}</span>
                      <span className="block text-caption text-ink-subtle tabular-nums">
                        {new Date(r.createdAt).toLocaleString("ko-KR")}
                      </span>
                    </span>
                    <StatusLozenge domain="run" value={r.status} />
                    {r.isReplay && <SourceLabel kind="replay" tooltip={false} />}
                    <span className="flex flex-wrap gap-1">
                      {r.groupStatuses.map((g) => (
                        <span key={g.groupCode} className="inline-flex items-center gap-1 text-caption text-ink-muted">
                          {groupLabel(g.groupCode)}
                          <StatusLozenge domain="review" value={g.status} />
                        </span>
                      ))}
                    </span>
                    <ChevronRight aria-hidden className="size-4 text-ink-subtle" />
                  </Link>
                </li>
              ))}
            </ul>
          )
        }
      </WakeGate>
    </div>
  );
}
