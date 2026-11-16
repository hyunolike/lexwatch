"use client";
import { apiFetch, type InboxItem } from "@/lib/api";
import { RoleSwitch, useRole } from "@/components/RoleSwitch";
import { ProviderBadge } from "@/components/ProviderBadge";
import { ReplayNote } from "@/components/ReplayNote";
import { WakeGate } from "@/components/WakeGate";

export default function InboxPage() {
  const role = useRole();
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold">알림함</h1>
        <RoleSwitch />
      </div>
      <p className="text-sm text-slate-500">승인된 검토의 데모 알림이에요. 실제 발송은 없어요.</p>
      <WakeGate<InboxItem[]> key={role} load={() => apiFetch<InboxItem[]>("/api/inbox")}>
        {(res) =>
          !Array.isArray(res.data) ? (
            <p>알림을 불러오지 못했어요.</p>
          ) : res.data.length === 0 ? (
            <p className="text-slate-500">아직 발송된 알림이 없어요.</p>
          ) : (
            <ul className="space-y-2">
              {res.data.map((n) => (
                <li key={n.id} className="rounded border bg-white p-3">
                  <div className="flex gap-3 text-sm text-slate-500">
                    <span>{n.sentAt ? new Date(n.sentAt).toLocaleString("ko-KR") : "—"}</span>
                    <span>{n.status}</span>
                    {n.isReplay && <ProviderBadge kind="replay" />}
                  </div>
                  {n.isReplay && <ReplayNote />}
                  <p className="mt-1 whitespace-pre-wrap">{n.body}</p>
                </li>
              ))}
            </ul>
          )
        }
      </WakeGate>
    </div>
  );
}
