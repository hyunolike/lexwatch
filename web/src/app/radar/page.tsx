"use client";
import { useState } from "react";
import Link from "next/link";
import { ExternalLink } from "lucide-react";
import { apiFetch, safeHttpUrl, type ApiError, type Radar, type RadarBill } from "@/lib/api";
import { groupLabel, statusLabel } from "@/lib/labels";
import { RADAR_FULL_LIMIT, billBadges, billsOfGroup, formatDay, stageStep, tabCount, truncationNote } from "@/lib/radar";
import { Button } from "@/components/ui/Button";
import { StatusLozenge } from "@/components/ds/badges";
import { Callout } from "@/components/ds/Callout";
import { CARD, PageHeader } from "@/components/ds/layout";
import { BillStepper } from "@/components/BillStepper";
import { useRole } from "@/components/RoleSwitch";
import { Tabs } from "@/components/ui/Tabs";
import { WakeGate } from "@/components/WakeGate";
import { cx } from "@/lib/cx";

/** 탭 순서: 노동, 개인정보, 금융(BR-08). 모르는 그룹은 뒤에. */
const GROUP_ORDER = ["labor", "privacy", "finance"];
const groupOrder = (code: string) => (GROUP_ORDER.includes(code) ? GROUP_ORDER.indexOf(code) : GROUP_ORDER.length);

const BADGE_TEXT = { new: "새 발의", changed: "단계 바뀜" } as const;

function BillCard({ bill, now }: { bill: RadarBill; now: number }) {
  const step = stageStep(bill.stage);
  const closed = bill.stage === "Closed";
  const badges = billBadges(bill, now);
  const link = safeHttpUrl(bill.detailUrl);
  return (
    <li className={cx("space-y-3 px-4 py-4", closed && "opacity-60")}>
      <div className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1">
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-1.5">
            {bill.groupCodes.map((g) => (
              <span key={g} className="rounded-sm bg-surface-sunken px-1.5 py-0.5 text-micro text-ink-muted">
                {groupLabel(g)}
              </span>
            ))}
            {badges.map((b) => (
              <span key={b} className="rounded-sm bg-info-bg px-1.5 py-0.5 text-micro text-info">
                {BADGE_TEXT[b]}
              </span>
            ))}
          </div>
          <p className="break-keep text-ui text-ink">{bill.billName}</p>
          <p className="text-caption text-ink-muted">
            의안 {bill.billNo}, {formatDay(bill.proposedOn)} 발의, {bill.proposer}
            {bill.committee ? `, ${bill.committee}` : ""}
          </p>
        </div>
        <StatusLozenge domain="billStage" value={bill.stage} />
      </div>

      {step !== null && !closed && <BillStepper step={step} label={statusLabel("billStage", bill.stage).label} />}

      {(bill.stage === "Merged" || closed || bill.stage === "Passed") && bill.procResult && (
        <p className="text-caption text-ink-muted">
          {bill.stage === "Merged" ? "위원회 대안에 내용이 반영됐어요" : `처리 결과: ${bill.procResult}`}
          {bill.procOn ? ` (본회의 ${formatDay(bill.procOn)})` : ""}
        </p>
      )}

      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-caption">
        {link && (
          <a href={link} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 rounded-sm text-ink underline underline-offset-4">
            의안정보시스템 <ExternalLink aria-hidden className="size-3.5" />
          </a>
        )}
        {bill.promulgated && (
          <span className="text-ink-muted">
            공포 후보 {formatDay(bill.promulgated.promulgationDate)}
            {bill.promulgated.reviewId && (
              <>
                {", "}
                <Link href={`/reviews/${bill.promulgated.reviewId}`} className="rounded-sm text-ink underline underline-offset-4">
                  검토 보기
                </Link>
              </>
            )}
          </span>
        )}
      </div>
    </li>
  );
}

function BillList({ bills, watch, now }: { bills: RadarBill[]; watch: string[]; now: number }) {
  if (bills.length === 0)
    return (
      <p className="text-ui text-ink-subtle">
        아직 모인 법안이 없어요. 지켜보는 법률: {watch.length ? watch.join(", ") : "없음"}
      </p>
    );
  return (
    <ul className={`${CARD} divide-y divide-line`}>
      {bills.map((b) => (
        <BillCard key={b.id} bill={b} now={now} />
      ))}
    </ul>
  );
}

function RadarView({ data, onShowAll }: { data: Radar; onShowAll: (() => void) | null }) {
  // 렌더마다 시계를 읽지 않도록 처음 그릴 때 한 번만 읽는다.
  const [now] = useState(() => Date.now());
  const tabs = [
    { value: "all", title: "전체", laws: [...new Set(data.watch.flatMap((w) => w.lawNames))] },
    ...[...data.watch]
      .sort((a, b) => groupOrder(a.groupCode) - groupOrder(b.groupCode))
      .map((w) => ({ value: w.groupCode, title: w.groupName, laws: w.lawNames })),
  ];
  return (
    <div className="space-y-4">
      <div className="space-y-1 text-caption text-ink-muted">
        <p>
          국회 의원 발의 법률안
          {data.lastCollectedAt ? `, 마지막 수집 ${new Date(data.lastCollectedAt).toLocaleString("ko-KR")}` : ", 아직 수집 전"}
        </p>
        <p>입법예고: 연결 준비 중(이용 승인 대기)</p>
      </div>
      {data.sources.assembly === "failing" && (
        <Callout tone="warning">최근 수집이 실패했거나 이틀 넘게 성공하지 못했어요. 목록이 최신이 아닐 수 있어요.</Callout>
      )}
      {truncationNote(data.totals) && (
        <Callout tone="info">
          <span>{truncationNote(data.totals)} </span>
          {onShowAll && data.totals && data.totals.limit < RADAR_FULL_LIMIT && (
            <Button variant="secondary" size="sm" onClick={onShowAll}>
              전부 보기
            </Button>
          )}
        </Callout>
      )}
      <Tabs
        label="실무 그룹"
        tabs={tabs.map((t) => {
          const list = billsOfGroup(data.bills, t.value);
          return {
            value: t.value,
            title: (
              <span>
                {t.title} <span className="tabular-nums text-ink-subtle">{tabCount(data.totals, t.value, list.length)}</span>
              </span>
            ),
            panel: <BillList bills={list} watch={t.laws} now={now} />,
          };
        })}
      />
    </div>
  );
}

/** 공포 전 법안 레이더(bill-radar). AI 분석 없이 진행 단계만 모아 보고, 공포되면 기존 검토 흐름이 이어받는다. */
export default function RadarPage() {
  const role = useRole();
  const [limit, setLimit] = useState<number | null>(null);
  return (
    <div className="space-y-5">
      <PageHeader title="법안 레이더" />
      <p className="max-w-reading text-ui text-ink-muted">
        실무 그룹이 지켜보는 법률의 개정안이 국회에서 어디까지 왔는지 모아 봐요. 아직 확정되지 않은 내용이라 AI 분석과 승인 없이 참고용으로만
        보여 주고, 공포되면 승인 대기함의 검토로 이어져요.
      </p>
      <WakeGate<Radar | ApiError>
        key={`${role}-${limit ?? "default"}`}
        label="법안 레이더"
        load={() => apiFetch<Radar | ApiError>(limit ? `/api/radar?limit=${limit}` : "/api/radar")}
      >
        {(res) =>
          res.status === 200 ? (
            <RadarView data={res.data as Radar} onShowAll={limit ? null : () => setLimit(RADAR_FULL_LIMIT)} />
          ) : (
            <Callout tone="danger">레이더를 불러오지 못했어요.</Callout>
          )
        }
      </WakeGate>
    </div>
  );
}
