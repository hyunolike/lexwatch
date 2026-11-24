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
import { FirstVisitGuide } from "@/components/FirstVisitGuide";
import { splitForRole } from "@/lib/reviewPresentation";
import { inboxCache } from "@/lib/inboxCache";
import { cycle } from "@/lib/shortcuts";
import { useShortcuts } from "@/components/Shortcuts";
import { useEffect } from "react";
import { SkeletonRows } from "@/components/ds/Skeleton";

/** 재현이면 replay 출처를 덧붙인다. */
const kindsOf = (r: ReviewSummary) =>
  r.isReplay && !r.providerKinds.includes("replay") ? [...r.providerKinds, "replay" as const] : r.providerKinds;

function Row({ r }: { r: ReviewSummary }) {
  return (
    <li>
      <Link
        data-review-row
        href={`/reviews/${r.id}`}
        className="group grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-3 px-4 py-4 transition-colors duration-[120ms] hover:bg-surface-sunken md:grid-cols-[minmax(0,1fr)_6rem_11rem_4rem_7rem_1rem]"
      >
        <span className="col-span-2 min-w-0 md:col-span-1">
          <span className="block text-ui font-semibold break-words text-ink">{r.lawName}</span>
          <span className="block text-caption text-ink-subtle">{r.isReplay ? REPLAY_NOTE : `${r.promulgationDate} 공포`}</span>
        </span>
        <span className="w-fit"><CountBadge count={r.findingCount} label="확인 필요" tone="danger" /></span>
        <span className="justify-self-end md:justify-self-start"><ConfidenceMeter value={r.confidence} /></span>
        <span className="text-caption text-ink-muted tabular-nums">근거 {r.evidenceCount}건</span>
        <span className="flex flex-wrap justify-end gap-1 md:justify-start">
          {kindsOf(r).map((k) => (
            <SourceLabel key={k} kind={k} tooltip={false} />
          ))}
        </span>
        <ChevronRight aria-hidden className="hidden size-4 text-ink-subtle transition-transform duration-[120ms] group-hover:translate-x-0.5 md:block" />
      </Link>
    </li>
  );
}

/** j/k로 행 링크 사이를 실제 포커스로 옮긴다(DS2-09). Enter는 링크의 기본 동작이다. */
function useRowKeys() {
  const move = (dir: 1 | -1) => () => {
    const rows = [...document.querySelectorAll<HTMLAnchorElement>("[data-review-row]")];
    const next = cycle(rows, rows.find((r) => r === document.activeElement) ?? null, dir);
    next?.focus();
    next?.scrollIntoView({ block: "nearest" });
  };
  useShortcuts({ next: move(1), prev: move(-1) });
}

function Queue({ items, role }: { items: ReviewSummary[]; role: string }) {
  useRowKeys();
  // 명령 팔레트의 검토 검색이 이 결과를 쓴다(DS2-10).
  useEffect(() => inboxCache.set(items), [items]);
  if (items.length === 0)
    return (
      <EmptyState
        pose="empty-queue"
        title="승인을 기다리는 검토가 없어요"
        description="새 개정이 수집되면 실무그룹별 초안이 이곳에 모여요."
      />
    );
  const { mine, rest } = splitForRole(items, role);
  const groups = new Map<string, ReviewSummary[]>();
  if (mine.length > 0) groups.set("파트너 결정 대기", mine);
  for (const r of rest) groups.set(r.groupName, [...(groups.get(r.groupName) ?? []), r]);
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
      <div className="space-y-2">
        <PageHeader title="승인 대기함" />
        <p className="max-w-reading text-ui text-ink-muted">확인이 필요한 초안을 먼저 살펴보고, 근거를 대조한 뒤 승인해 주세요.</p>
        <p className="max-w-reading text-caption text-ink-subtle">그룹 관련성은 법령과 실무그룹의 관련 정도예요. 초안의 정확도를 뜻하지 않아요.</p>
      </div>
      <FirstVisitGuide />
      <WakeGate<ReviewSummary[]> key={role} label="승인 대기함" fallback={<div className="space-y-6"><SkeletonRows rows={3} /><SkeletonRows rows={3} /></div>} load={() => apiFetch<ReviewSummary[]>("/api/reviews")}>
        {(res) =>
          Array.isArray(res.data) ? (
            <Queue items={res.data} role={role} />
          ) : (
            <Callout tone="danger">목록을 불러오지 못했어요.</Callout>
          )
        }
      </WakeGate>
    </div>
  );
}
