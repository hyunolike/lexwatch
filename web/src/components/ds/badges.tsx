"use client";
import { CircleHelp, History, Sparkles, UserRound, Wrench, type LucideIcon } from "lucide-react";
import { cva } from "class-variance-authority";
import { Meter } from "@base-ui/react/meter";
import { Tooltip } from "@/components/ui/Floating";
import { cx } from "@/lib/cx";
import { provenanceOf, statusLabel, type Domain, type ProvKind, type Tone } from "@/lib/labels";

const TONE: Record<Tone, { fg: string; bg: string; dot: string }> = {
  success: { fg: "text-success", bg: "bg-success-bg", dot: "bg-success" },
  danger: { fg: "text-danger", bg: "bg-danger-bg", dot: "bg-danger" },
  warning: { fg: "text-warning", bg: "bg-warning-bg", dot: "bg-warning" },
  info: { fg: "text-info", bg: "bg-info-bg", dot: "bg-info" },
  neutral: { fg: "text-neutral", bg: "bg-neutral-bg", dot: "bg-neutral" },
};

/** 상태 표시: 색 점 + 한국어 라벨. 색만으로 뜻을 전하지 않는다(DS-05). */
export function StatusLozenge({ domain, value }: { domain: Domain; value: string }) {
  const s = statusLabel(domain, value);
  const t = TONE[s.tone];
  return (
    <span className={cx("inline-flex items-center gap-1.5 rounded-sm px-2 py-0.5 text-micro", t.bg, t.fg)}>
      <span aria-hidden className={cx("size-1.5 rounded-full", t.dot)} />
      {s.label}
    </span>
  );
}

const PROV_ICON: Record<ProvKind, LucideIcon> = { ai: Sparkles, rule: Wrench, human: UserRound, replay: History, neutral: CircleHelp };

/** 출처 변형 표(DS2-05, DS2-15). 네 종류만 색이 있고 상태 색 계열을 쓰지 않는다. 재현은 점선 테두리. */
export const provenanceVariants = cva("inline-flex items-center gap-1 rounded-sm px-2 py-0.5 text-micro", {
  variants: {
    kind: {
      ai: "bg-prov-ai-bg text-prov-ai",
      rule: "bg-prov-rule-bg text-prov-rule",
      human: "bg-prov-human-bg text-prov-human",
      replay: "border border-dashed border-line-strong bg-prov-replay-bg text-prov-replay",
      neutral: "bg-neutral-bg text-neutral",
    } satisfies Record<ProvKind, string>,
  },
});

/**
 * 출처 라벨(DS2-05). 누가 만들었는지를 알리는 분류이고 품질 판정이 아니다.
 * kind는 API의 공급자 종류, actor와 role은 AI 흔적과 승인 기록. 판정은 provenanceOf 한 곳에서 한다.
 */
export function SourceLabel({
  kind,
  name,
  actor,
  role,
  tooltip = true,
}: {
  kind?: string | null;
  name?: string | null;
  actor?: string | null;
  role?: string | null;
  tooltip?: boolean;
}) {
  const p = provenanceOf({ providerKind: kind, providerName: name, actor, role });
  if (!p) return null;
  const Icon = PROV_ICON[p.kind];
  const cls = provenanceVariants({ kind: p.kind });
  const content = (
    <>
      <Icon aria-hidden className="size-3.5" />
      {p.label}
      <span className="sr-only">: {p.help}</span>
    </>
  );
  // 링크 안처럼 포커스를 받으면 안 되는 곳에서는 툴팁 없이 title로만 설명한다.
  if (!tooltip)
    return (
      <span className={cls} title={p.help}>
        {content}
      </span>
    );
  return (
    <Tooltip
      trigger={
        <span tabIndex={0} className={cx(cls, "cursor-help")}>
          {content}
        </span>
      }
    >
      {p.help}
    </Tooltip>
  );
}

/** 개수 배지. 부르는 쪽이 tone을 정한다. 크리틱 적발 수만 danger로 쓴다. */
export function CountBadge({ count, label, tone = "neutral" }: { count: number; label: string; tone?: "neutral" | "danger" }) {
  const danger = tone === "danger" && count > 0;
  return (
    <span
      className={cx(
        "inline-flex items-center gap-1 rounded-sm px-2 py-0.5 text-micro tabular-nums",
        danger ? "bg-danger-bg text-danger" : "bg-neutral-bg text-neutral",
      )}
    >
      {label} {count}
    </span>
  );
}

/** 실무그룹 관련성. 초안 정확도와 구분하며 0~1 밖은 잘라 낸다. */
export function ConfidenceMeter({ value }: { value: number | null }) {
  if (value == null) return <span className="text-caption text-ink-subtle">그룹 관련성 —</span>;
  const v = Math.min(1, Math.max(0, value));
  const pct = Math.round(v * 100);
  const filled = Math.round(v * 5);
  return (
    <Meter.Root value={pct} aria-label="그룹 관련성" className="inline-flex items-center gap-2">
      <Meter.Label className="text-caption text-ink-subtle">그룹 관련성</Meter.Label>
      <span aria-hidden className="flex gap-0.5">
        {Array.from({ length: 5 }, (_, i) => (
          <span key={i} className={cx("h-2.5 w-1.5 rounded-[1px]", i < filled ? "bg-brand" : "bg-line")} />
        ))}
      </span>
      <Meter.Value className="text-label tabular-nums text-ink">{() => `${pct}%`}</Meter.Value>
    </Meter.Root>
  );
}
