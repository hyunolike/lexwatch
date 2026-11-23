"use client";
import Link from "next/link";
import { useState } from "react";
import { ArrowLeft, RotateCcw } from "lucide-react";
import { apiFetch, hasRetryableStep, retryRun, type RunDetail } from "@/lib/api";
import { REPLAY_NOTE } from "@/lib/labels";
import { SourceLabel, StatusLozenge } from "@/components/ds/badges";
import { Callout } from "@/components/ds/Callout";
import { Button } from "@/components/ui/Button";
import { useRole } from "@/components/RoleSwitch";
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
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-title-md text-ink">{data.lawChange.lawName}</h1>
        <StatusLozenge domain="run" value={data.run.status} />
        {data.run.isReplay && <SourceLabel kind="replay" />}
        {!data.run.isReplay && <span className="text-caption text-ink-subtle">{data.lawChange.promulgationDate} 공포</span>}
      </div>
      {data.run.isReplay && <Callout tone="info">{REPLAY_NOTE}</Callout>}
      {role === "admin" && !data.run.isReplay && hasRetryableStep(data.timeline, data.groups ?? []) && (
        <Button loading={retrying} onClick={onRetry}>
          <RotateCcw aria-hidden className="size-4" /> 실패한 단계 다시 실행
        </Button>
      )}
      {notice && (
        <Callout tone="warning" live>
          {notice}
        </Callout>
      )}
      <Timeline items={data.timeline} />
    </div>
  );
}

export function RunClient({ id }: { id: string }) {
  const role = useRole();
  return (
    <div className="space-y-4">
      <Link href="/runs" className="inline-flex items-center gap-1 rounded-md text-label text-ink-muted hover:text-ink">
        <ArrowLeft aria-hidden className="size-4" /> 실행 이력
      </Link>
      <WakeGate<RunDetail> key={role} label="자동 처리 상세" load={() => apiFetch<RunDetail>(`/api/runs/${id}`)}>
        {(res, reload) =>
          res.status === 401 || res.status === 403 ? (
            <Callout tone="warning">이 화면을 보려면 변호사 또는 관리자 역할이 필요해요. 위에서 역할을 골라 주세요.</Callout>
          ) : res.status === 404 ? (
            <Callout tone="warning">실행을 찾을 수 없어요.</Callout>
          ) : res.data && Array.isArray(res.data.timeline) ? (
            <Detail data={res.data} reload={reload} />
          ) : (
            <Callout tone="danger">실행 정보를 불러오지 못했어요.</Callout>
          )
        }
      </WakeGate>
    </div>
  );
}
