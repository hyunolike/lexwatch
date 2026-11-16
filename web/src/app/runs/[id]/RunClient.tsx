"use client";
import { useState } from "react";
import { apiFetch, hasRetryableStep, retryRun, type RunDetail } from "@/lib/api";
import { ProviderBadge } from "@/components/ProviderBadge";
import { RoleSwitch, useRole } from "@/components/RoleSwitch";
import { Timeline } from "@/components/Timeline";
import { WakeGate } from "@/components/WakeGate";

function Detail({ data, reload }: { data: RunDetail; reload: () => void }) {
  const role = useRole();
  const [retrying, setRetrying] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const onRetry = async () => {
    setRetrying(true);
    setNotice(null);
    const out = await retryRun(data.run.id);
    setRetrying(false);
    if (out.kind === "ok") return reload();
    setNotice(out.kind === "waking" ? "서버가 깨어나는 중이에요. 잠시 뒤에 다시 시도해 주세요." : out.message);
  };
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold">{data.lawChange.lawName}</h2>
        <span className="text-sm">{data.run.status}</span>
        {data.run.isReplay && <ProviderBadge kind="replay" />}
        <span className="text-sm text-slate-500">{data.lawChange.promulgationDate} 공포</span>
      </div>
      {role === "admin" && hasRetryableStep(data.timeline, data.groups ?? []) && (
        <button onClick={onRetry} disabled={retrying} className="rounded border bg-white px-3 py-1 text-sm disabled:opacity-50">
          실패한 단계 다시 실행
        </button>
      )}
      {notice && <p role="alert" className="rounded border border-orange-300 bg-orange-50 p-2 text-sm">{notice}</p>}
      <Timeline items={data.timeline} />
    </div>
  );
}

export function RunClient({ id }: { id: string }) {
  const role = useRole();
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold">실행 타임라인</h1>
        <RoleSwitch />
      </div>
      <WakeGate<RunDetail> key={role} load={() => apiFetch<RunDetail>(`/api/runs/${id}`)}>
        {(res, reload) =>
          res.status === 401 || res.status === 403 ? (
            <p>이 화면을 보려면 변호사 또는 관리자 역할이 필요해요. 위에서 역할을 골라 주세요.</p>
          ) : res.status === 404 ? (
            <p>실행을 찾을 수 없어요.</p>
          ) : res.data && Array.isArray(res.data.timeline) ? (
            <Detail data={res.data} reload={reload} />
          ) : (
            <p>실행 정보를 불러오지 못했어요.</p>
          )
        }
      </WakeGate>
    </div>
  );
}
