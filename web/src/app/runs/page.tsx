"use client";
import Link from "next/link";
import { apiFetch, type RunSummary } from "@/lib/api";
import { ProviderBadge } from "@/components/ProviderBadge";
import { RoleSwitch, useRole } from "@/components/RoleSwitch";
import { WakeGate } from "@/components/WakeGate";

export default function RunsPage() {
  const role = useRole();
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold">실행 이력</h1>
        <RoleSwitch />
      </div>
      <WakeGate<RunSummary[]> key={role} load={() => apiFetch<RunSummary[]>("/api/runs?limit=50")}>
        {(res) =>
          !Array.isArray(res.data) ? (
            <p>목록을 불러오지 못했어요.</p>
          ) : res.data.length === 0 ? (
            <p className="text-slate-500">실행 기록이 없어요.</p>
          ) : (
            <ul className="space-y-2">
              {res.data.map((r) => (
                <li key={r.id}>
                  <Link href={`/runs/${r.id}`} className="flex flex-wrap items-center gap-3 rounded border bg-white p-3 hover:bg-slate-50">
                    <span className="font-medium">{r.lawName}</span>
                    <span className="text-sm">{r.status}</span>
                    <span className="text-sm text-slate-500">{new Date(r.createdAt).toLocaleString("ko-KR")}</span>
                    {r.isReplay && <ProviderBadge kind="replay" />}
                    {r.groupStatuses.map((g) => (
                      <span key={g.groupCode} className="rounded bg-slate-100 px-1.5 text-xs">{g.groupCode} {g.status}</span>
                    ))}
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
