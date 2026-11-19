"use client";
import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { apiFetch, type ReviewSummary } from "@/lib/api";
import { REPLAY_NOTE } from "@/lib/labels";
import { ConfidenceMeter, CountBadge, SourceLabel } from "@/components/ds/badges";
import { Callout } from "@/components/ds/Callout";
import { CARD, EmptyState, PageHeader } from "@/components/ds/layout";
import { useRole } from "@/components/RoleSwitch";
import { WakeGate } from "@/components/WakeGate";

/** 재현이면 replay 출처를 덧붙인다. */
const kindsOf = (r: ReviewSummary) =>
  r.isReplay && !r.providerKinds.includes("replay") ? [...r.providerKinds, "replay" as const] : r.providerKinds;

function Row({ r }: { r: ReviewSummary }) {
  return (
    <li>
      <Link
        href={`/reviews/${r.id}`}
        className="group flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 transition-colors duration-[120ms] hover:bg-surface-sunken"
      >
        <span className="min-w-0 flex-1 basis-64">
          <span className="block truncate text-ui font-semibold text-ink">{r.lawName}</span>
          <span className="block text-caption text-ink-subtle">{r.isReplay ? REPLAY_NOTE : `${r.promulgationDate} 공포`}</span>
        </span>
        {r.findingCount > 0 && <CountBadge count={r.findingCount} label="크리틱 적발" tone="danger" />}
        <ConfidenceMeter value={r.confidence} />
        <span className="text-caption text-ink-muted tabular-nums">근거 {r.evidenceCount}건</span>
        <span className="flex gap-1">
          {kindsOf(r).map((k) => (
            <SourceLabel key={k} kind={k} tooltip={false} />
          ))}
        </span>
        <ChevronRight aria-hidden className="size-4 text-ink-subtle transition-transform duration-[120ms] group-hover:translate-x-0.5" />
      </Link>
    </li>
  );
}

function Queue({ items }: { items: ReviewSummary[] }) {
  if (items.length === 0)
    return (
      <EmptyState
        pose="empty-queue"
        title="승인을 기다리는 검토가 없어요"
        description="새 개정이 수집되면 실무그룹별 초안이 이곳에 모여요."
      />
    );
  const groups = new Map<string, ReviewSummary[]>();
  for (const r of items) groups.set(r.groupName, [...(groups.get(r.groupName) ?? []), r]);
  // 크리틱 적발이 있는 행을 먼저 보여 준다(DS-11).
  const ordered = (rows: ReviewSummary[]) => [...rows].sort((a, b) => b.findingCount - a.findingCount);
  return (
    <div className="space-y-6">
      {[...groups].map(([name, rows]) => (
        <section key={name} aria-labelledby={`g-${name}`} className={CARD}>
          <h2 id={`g-${name}`} className="flex items-center justify-between border-b border-line px-4 py-3 text-title-sm text-ink">
            {name}
            <span className="text-caption font-normal text-ink-subtle tabular-nums">{rows.length}건</span>
          </h2>
          <ul className="divide-y divide-line">
            {ordered(rows).map((r) => (
              <Row key={r.id} r={r} />
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

export default function Home() {
  const role = useRole();
  return (
    <div className="space-y-5">
      <PageHeader title="승인 대기함" />
      <WakeGate<ReviewSummary[]> key={role} load={() => apiFetch<ReviewSummary[]>("/api/reviews")}>
        {(res) =>
          Array.isArray(res.data) ? (
            <Queue items={res.data} />
          ) : (
            <Callout tone="danger">목록을 불러오지 못했어요.</Callout>
          )
        }
      </WakeGate>
    </div>
  );
}
