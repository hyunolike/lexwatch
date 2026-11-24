"use client";
import Link from "next/link";
import { apiFetch, formatDuration, maskedSummary, type AiUsage, type ApiError } from "@/lib/api";
import { SourceLabel, StatusLozenge } from "@/components/ds/badges";
import { Callout } from "@/components/ds/Callout";
import { CARD, PageHeader } from "@/components/ds/layout";
import { useRole } from "@/components/RoleSwitch";
import { WakeGate } from "@/components/WakeGate";

function Stat({ label, value, hint }: { label: string; value: number; hint?: string }) {
  return (
    <div className={`${CARD} p-4`}>
      <p className="text-caption text-ink-muted">{label}</p>
      <p className="mt-1 text-title-lg text-ink tabular-nums">{value}</p>
      {hint && <p className="mt-0.5 text-caption text-ink-subtle">{hint}</p>}
    </div>
  );
}

function Usage({ data }: { data: AiUsage }) {
  const { summary: s, recent, labels } = data;
  const o = (k: string) => s.byOutcome[k] ?? 0;
  const maskedTotal = Object.values(s.masked).reduce((a, b) => a + b, 0);
  return (
    <div className="space-y-5">
      <section aria-label={`최근 ${s.days}일 요약`} className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <Stat label={`최근 ${s.days}일 요청`} value={s.total} />
        <Stat label="답변" value={o("answered")} />
        <Stat label="거절, 실패, 모델 없음" value={o("refused") + o("failed") + o("unavailable")} />
        <Stat label="가린 민감정보" value={maskedTotal} hint={maskedTotal ? maskedSummary(s.masked, labels) : undefined} />
      </section>

      <section aria-labelledby="recent" className="space-y-2">
        <h2 id="recent" className="text-title-sm text-ink">최근 기록</h2>
        {recent.length === 0 ? (
          <p className="text-ui text-ink-subtle">아직 기록이 없어요. 변호사로 보기에서 검토 보조를 쓰면 여기에 남아요.</p>
        ) : (
          <div className={`${CARD} overflow-x-auto`}>
            <table className="w-full min-w-[40rem] text-left text-label">
              <thead className="border-b border-line text-caption text-ink-muted">
                <tr>
                  <th scope="col" className="px-4 py-2 font-medium">시각</th>
                  <th scope="col" className="px-4 py-2 font-medium">결과</th>
                  <th scope="col" className="px-4 py-2 font-medium">모델</th>
                  <th scope="col" className="px-4 py-2 font-medium">소요</th>
                  <th scope="col" className="px-4 py-2 font-medium">가린 정보</th>
                  <th scope="col" className="px-4 py-2 font-medium">검토</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {recent.map((r, i) => (
                  <tr key={i}>
                    <td className="px-4 py-2 text-ink-muted tabular-nums whitespace-nowrap">{new Date(r.at).toLocaleString("ko-KR")}</td>
                    <td className="px-4 py-2"><StatusLozenge domain="usage" value={r.outcome} /></td>
                    <td className="px-4 py-2">{r.providerKind ? <SourceLabel kind={r.providerKind} tooltip={false} /> : <span className="text-ink-subtle">—</span>}</td>
                    <td className="px-4 py-2 text-ink-muted tabular-nums">{formatDuration(r.durationMs)}</td>
                    <td className="px-4 py-2 text-ink">{maskedSummary(r.masked, labels)}</td>
                    <td className="px-4 py-2">
                      {r.groupReviewId ? (
                        <Link href={`/reviews/${r.groupReviewId}`} className="rounded-sm text-ink underline underline-offset-4">열기</Link>
                      ) : (
                        <span className="text-ink-subtle">—</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

/** AI 사용 기록(관리자). 질문과 답변은 저장하지 않고, 언제 무엇을 가렸는지만 남긴다. */
export default function GovernancePage() {
  const role = useRole();
  return (
    <div className="space-y-5">
      <PageHeader title="AI 사용 기록" />
      <p className="max-w-reading text-ui text-ink-muted">
        검토 보조에 들어온 질문은 AI로 보내기 전에 주민등록번호, 연락처, 계좌와 카드 번호, 등록된 고객사 이름을 가려요. 이 화면에는 질문과
        답변 없이 시각, 결과, 쓴 모델, 가린 종류와 건수만 남아요. 사람 이름과 주소는 규칙으로 잡지 못해요.
      </p>
      {role !== "admin" ? (
        <Callout tone="info">관리자로 보기에서 볼 수 있어요.</Callout>
      ) : (
        <WakeGate<AiUsage | ApiError> key={role} label="AI 사용 기록" load={() => apiFetch<AiUsage | ApiError>("/api/governance/ai-usage")}>
          {(res) =>
            res.status === 200 ? <Usage data={res.data as AiUsage} /> : <Callout tone="danger">기록을 불러오지 못했어요.</Callout>
          }
        </WakeGate>
      )}
    </div>
  );
}
