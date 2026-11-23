"use client";
import { apiFetch, type InboxItem } from "@/lib/api";
import { REPLAY_NOTE, statusLabel } from "@/lib/labels";
import { SourceLabel, StatusLozenge } from "@/components/ds/badges";
import { Callout } from "@/components/ds/Callout";
import { CARD, EmptyState, PageHeader } from "@/components/ds/layout";
import { useRole } from "@/components/RoleSwitch";
import { WakeGate } from "@/components/WakeGate";

export default function InboxPage() {
  const role = useRole();
  return (
    <div className="space-y-5">
      <PageHeader title="알림함" />
      <p className="text-ui text-ink-muted">승인된 검토의 데모 알림이에요. 실제 발송은 없어요.</p>
      <WakeGate<InboxItem[]> key={role} label="알림함" load={() => apiFetch<InboxItem[]>("/api/inbox")}>
        {(res) =>
          !Array.isArray(res.data) ? (
            <Callout tone="danger">알림을 불러오지 못했어요.</Callout>
          ) : res.data.length === 0 ? (
            <EmptyState
              pose="empty-inbox"
              title="아직 발송된 알림이 없어요"
              description="변호사가 검토를 승인하면 해당 실무그룹 알림함으로 안내문이 와요."
            />
          ) : (
            <ul className="space-y-3">
              {res.data.map((n) => (
                <li key={n.id} className={`${CARD} space-y-2 p-4`}>
                  <div className="flex flex-wrap items-center gap-2 text-caption text-ink-subtle">
                    <StatusLozenge domain="notification" value={n.status} />
                    <span>{statusLabel("channel", n.channel).label}</span>
                    <span className="tabular-nums">{n.sentAt ? new Date(n.sentAt).toLocaleString("ko-KR") : "—"}</span>
                    {n.isReplay && <SourceLabel kind="replay" />}
                  </div>
                  {n.isReplay && <Callout tone="info">{REPLAY_NOTE}</Callout>}
                  <p className="text-reading max-w-[40em] whitespace-pre-wrap text-ink">{n.body}</p>
                </li>
              ))}
            </ul>
          )
        }
      </WakeGate>
    </div>
  );
}
