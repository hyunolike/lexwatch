"use client";
import { useState } from "react";
import { apiFetch, formatDuration, type ApiError, type OpsSummary } from "@/lib/api";
import { stepLabel } from "@/lib/labels";
import { hoursText, maxOf, pct, stackParts } from "@/lib/ops";
import { Callout } from "@/components/ds/Callout";
import { CARD, PageHeader } from "@/components/ds/layout";
import { Sparkline } from "@/components/ds/Sparkline";
import { useRole } from "@/components/RoleSwitch";
import { WakeGate } from "@/components/WakeGate";
import { cx } from "@/lib/cx";
import { OpsSkeleton } from "@/components/ds/Skeleton";

const DAYS = [7, 14, 30] as const;
/** 공급자는 모두 AI라 색으로 나누지 않고 이름과 건수로 구분한다(design.md 1.1). */
const PROVIDER: Record<string, string> = { nvidia: "NVIDIA", cloudflare: "Cloudflare", groq: "Groq" };
const DECISION: Record<string, string> = { approve: "승인", reject: "반려", confirm: "파트너 최종 승인", return: "파트너가 돌려보냄" };

function Card({ label, value, sub, series }: { label: string; value: string; sub?: string; series?: number[] }) {
  return (
    <div className={`${CARD} flex items-end justify-between gap-3 p-4`}>
      <div className="min-w-0">
        <p className="text-caption text-ink-muted">{label}</p>
        <p className="mt-1 text-title-lg text-ink tabular-nums">{value}</p>
        {sub && <p className="mt-0.5 text-caption text-ink-subtle">{sub}</p>}
      </div>
      {series && series.length > 1 && <Sparkline values={series} />}
    </div>
  );
}

/** 같은 축의 가로 막대 하나. 값은 글로도 둔다. */
function HBar({ value, max, tone = "bg-ink-muted" }: { value: number | null; max: number; tone?: string }) {
  const w = value === null || max === 0 ? 0 : Math.max((value / max) * 100, value > 0 ? 2 : 0);
  return (
    <span aria-hidden className="block h-2 w-full rounded-sm bg-surface-sunken">
      <span className={cx("block h-2 rounded-sm", tone)} style={{ width: `${w}%` }} />
    </span>
  );
}

function Summary({ s }: { s: OpsSummary }) {
  const steps = s.steps.filter((x) => x.outcomes > 0 || x.samples > 0);
  const maxP = maxOf(steps.map((x) => x.p90Ms ?? x.p50Ms));
  const providers = stackParts(Object.entries(s.ai.byProvider));
  const peak = Math.max(...s.runs.daily, 0);
  const chatByModel = Object.keys(PROVIDER).reduce((n, k) => n + (s.ai.chat.byProvider[k] ?? 0), 0);
  return (
    <div className="space-y-6">
      <section aria-label="요약" className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Card label={`최근 ${s.days}일 실행`} value={`${s.runs.total}건`} sub={`하루 최대 ${peak}건, 관련 없음 ${s.runs.byStatus.ClosedIrrelevant ?? 0}`} series={s.runs.daily} />
        <Card
          label="단계 최종 실패율"
          value={pct(s.stepTotals.failureRate)}
          sub={`끝난 단계 ${s.stepTotals.outcomes}개 중 실패 ${s.stepTotals.failed}개`}
        />
        <Card
          label="AI 처리(최종 공급자 기준)"
          value={`${s.ai.total}건`}
          sub={`단계 ${s.ai.total - chatByModel}건, 채팅 ${chatByModel}건, 규칙 대체 ${s.ai.ruleFallback}건 별도`}
          series={s.ai.daily}
        />
        <Card
          label="적발이 있는 초안"
          value={pct(s.critic.flaggedRate)}
          sub={`검사가 끝난 초안 ${s.critic.drafts}개 중 ${s.critic.flagged}개`}
        />
      </section>

      <section aria-labelledby="ops-steps" className="space-y-2">
        <h2 id="ops-steps" className="text-title-sm text-ink">단계별 신뢰성과 소요 시간</h2>
        <p className="text-caption text-ink-muted">
          실패율은 다시 시도한 끝의 최종 결과 기준이에요. 소요 시간 막대는 모든 단계가 같은 축이고, 표본이 5개보다 적으면 90번째 백분위를 숨겨요.
        </p>
        {steps.length === 0 ? (
          <p className="text-ui text-ink-subtle">이 기간에 끝난 단계가 없어요.</p>
        ) : (
          <ul className={`${CARD} divide-y divide-line`}>
            {steps.map((x) => (
              <li key={x.step} className="grid grid-cols-1 gap-2 px-4 py-3 md:grid-cols-[10rem_1fr_1fr] md:items-center md:gap-4">
                <p className="text-label text-ink">{stepLabel(x.step)}</p>
                <div className="space-y-1">
                  <p className="text-caption text-ink-muted">
                    실패 {pct(x.failureRate)} <span className="text-ink-subtle">({x.failed}/{x.outcomes})</span>
                    {x.autoRetries > 0 && `, 자동 재시도 ${x.autoRetries}회`}
                    {x.extraRecords > 0 && `, 다시 시도한 기록 ${x.extraRecords}개`}
                  </p>
                  <HBar value={x.failureRate} max={1} tone="bg-danger" />
                </div>
                <div className="space-y-1">
                  <p className="text-caption text-ink-muted">
                    중앙값 {x.p50Ms === null ? "—" : formatDuration(x.p50Ms)}
                    {x.p90Ms !== null ? `, 90% ${formatDuration(x.p90Ms)}` : x.samples > 0 ? ", 90%는 표본 부족" : ""}
                    <span className="text-ink-subtle"> (n={x.samples})</span>
                  </p>
                  <HBar value={x.p90Ms ?? x.p50Ms} max={maxP} />
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <section aria-labelledby="ops-ai" className="space-y-2">
          <h2 id="ops-ai" className="text-title-sm text-ink">AI 공급자</h2>
          <p className="text-caption text-ink-muted">
            끝난 AI 단계의 최종 공급자예요. 대체 경로에서 실패한 앞선 시도와 에이전트 안의 여러 호출은 세지 않아요.
          </p>
          <div className={`${CARD} space-y-3 p-4`}>
            {providers.length === 0 ? (
              <p className="text-ui text-ink-subtle">이 기간에 모델이 처리한 단계가 없어요.</p>
            ) : (
              <ul className="space-y-2">
                {providers.map((p) => (
                  <li key={p.key} className="grid grid-cols-[6.5rem_1fr_auto] items-center gap-3 text-caption text-ink">
                    <span>AI {PROVIDER[p.key] ?? p.key}</span>
                    <HBar value={p.share} max={1} tone="bg-prov-ai" />
                    <span className="tabular-nums text-ink-muted">
                      {p.value}건 ({pct(p.share, 0)})
                    </span>
                  </li>
                ))}
              </ul>
            )}
            <p className="text-caption text-ink-muted">
              규칙 대체 {s.ai.ruleFallback}건, 실패 {s.ai.failed}건. 검토 보조 채팅 {s.ai.chat.total}건(답변 {s.ai.chat.byOutcome.answered ?? 0}, 거절{" "}
              {s.ai.chat.byOutcome.refused ?? 0}, 실패 {s.ai.chat.byOutcome.failed ?? 0}).
            </p>
          </div>
        </section>

        <section aria-labelledby="ops-human" className="space-y-2">
          <h2 id="ops-human" className="text-title-sm text-ink">사람 결정</h2>
          <div className={`${CARD} space-y-2 p-4 text-label text-ink`}>
            <p>
              지금 결정을 기다리는 검토 <span className="font-semibold tabular-nums">{s.decisions.waiting}건</span>
            </p>
            <p className="text-ink-muted">
              {Object.entries(s.decisions.byKind)
                .filter(([, n]) => n > 0)
                .map(([k, n]) => `${DECISION[k] ?? k} ${n}`)
                .join(", ") || "이 기간의 결정이 없어요"}
            </p>
            <p className="text-caption text-ink-muted">
              검사가 끝난 뒤 첫 변호사 결정까지 중앙값 {hoursText(s.decisions.medianHoursToFirstDecision)}
              <span className="text-ink-subtle"> (n={s.decisions.firstDecisionSamples})</span>. 공개 데모라 방문자의 클릭이 섞여 있어요.
            </p>
            <p className="text-caption text-ink-muted">
              법안 수집 마지막 성공 {s.bills.lastSuccessAt ? new Date(s.bills.lastSuccessAt).toLocaleString("ko-KR") : "없음"}
              {s.bills.failedRuns > 0 && `, 이 기간 실패 ${s.bills.failedRuns}회`}
            </p>
          </div>
        </section>
      </div>
    </div>
  );
}

/** 운영 현황(관리자). 실행, 단계 신뢰성, AI 공급자, 사람 결정을 한 화면에서 본다(ops-visibility OV-17). */
export default function OpsPage() {
  const role = useRole();
  const [days, setDays] = useState<(typeof DAYS)[number]>(14);
  return (
    <div className="space-y-5">
      <PageHeader title="운영 현황">
        {role === "admin" && (
          <div role="group" aria-label="기간" className="inline-flex rounded-md border border-line bg-surface-sunken p-0.5">
            {DAYS.map((d) => (
              <button
                key={d}
                type="button"
                aria-pressed={days === d}
                onClick={() => setDays(d)}
                className={cx("h-8 rounded px-3 text-label", days === d ? "bg-surface text-ink shadow-sm" : "text-ink-muted hover:text-ink")}
              >
                {d}일
              </button>
            ))}
          </div>
        )}
      </PageHeader>
      <p className="max-w-reading text-ui text-ink-muted">
        재현 예시를 뺀 실제 실행만 세요. 숫자는 날짜(한국 시간)별로 모으고, 모든 비율 옆에 분모를 적어요.
      </p>
      {role !== "admin" ? (
        <Callout tone="info">관리자로 보기에서 볼 수 있어요.</Callout>
      ) : (
        <WakeGate<OpsSummary | ApiError> key={`${role}-${days}`} label="운영 현황" fallback={<OpsSkeleton />} load={() => apiFetch<OpsSummary | ApiError>(`/api/ops/summary?days=${days}`)}>
          {(res) => (res.status === 200 ? <Summary s={res.data as OpsSummary} /> : <Callout tone="danger">운영 현황을 불러오지 못했어요.</Callout>)}
        </WakeGate>
      )}
    </div>
  );
}
