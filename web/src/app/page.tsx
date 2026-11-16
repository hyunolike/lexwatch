"use client";
import Link from "next/link";
import { apiFetch, type ReviewSummary } from "@/lib/api";
import { ProviderBadge } from "@/components/ProviderBadge";
import { RoleSwitch, useRole } from "@/components/RoleSwitch";
import { WakeGate } from "@/components/WakeGate";

function Inbox({ items }: { items: ReviewSummary[] }) {
  const groups = new Map<string, ReviewSummary[]>();
  for (const r of items) groups.set(r.groupName, [...(groups.get(r.groupName) ?? []), r]);
  if (items.length === 0) return <p className="text-slate-500">승인 대기 중인 검토가 없어요.</p>;
  return (
    <div className="space-y-6">
      {[...groups].map(([name, rows]) => (
        <section key={name}>
          <h2 className="mb-2 font-semibold">{name}</h2>
          <ul className="space-y-2">
            {rows.map((r) => {
              const kinds = r.isReplay && !r.providerKinds.includes("replay") ? [...r.providerKinds, "replay" as const] : r.providerKinds;
              return (
                <li key={r.id}>
                  <Link href={`/reviews/${r.id}`} className="flex flex-wrap items-center gap-3 rounded border bg-white p-3 hover:bg-slate-50">
                    <span className="font-medium">{r.lawName}</span>
                    <span className="text-sm text-slate-500">{r.promulgationDate} 공포</span>
                    <span className="text-sm">신뢰도 {r.confidence == null ? "—" : Math.round(r.confidence * 100) + "%"}</span>
                    <span className="text-sm">근거 {r.evidenceCount}건</span>
                    <span className="text-sm">크리틱 {r.findingCount}건</span>
                    {kinds.map((k) => <ProviderBadge key={k} kind={k} />)}
                  </Link>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}

export default function Home() {
  const role = useRole();
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold">승인 대기함</h1>
        <RoleSwitch />
      </div>
      <WakeGate<ReviewSummary[]> key={role} load={() => apiFetch<ReviewSummary[]>("/api/reviews")}>
        {(res) => (Array.isArray(res.data) ? <Inbox items={res.data} /> : <p>목록을 불러오지 못했어요.</p>)}
      </WakeGate>
    </div>
  );
}
