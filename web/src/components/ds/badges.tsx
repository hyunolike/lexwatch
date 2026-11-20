"use client";
import { Cloud, Cpu, History, Wrench, Zap, type LucideIcon } from "lucide-react";
import { Meter } from "@base-ui/react/meter";
import { Tooltip } from "@/components/ui/Floating";
import { cx } from "@/lib/cx";
import { statusLabel, type Domain, type Tone } from "@/lib/labels";

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

const SOURCES: Record<string, { label: string; icon: LucideIcon; cls: string; help: string }> = {
  nvidia: {
    label: "NVIDIA",
    icon: Cpu,
    cls: "bg-source-nvidia-bg text-source-nvidia",
    help: "NVIDIA Nemotron이 실제로 처리했어요.",
  },
  cloudflare: {
    label: "Cloudflare",
    icon: Cloud,
    cls: "bg-source-cloudflare-bg text-source-cloudflare",
    help: "Cloudflare Workers AI의 공개 모델(gpt-oss)이 처리했어요.",
  },
  groq: {
    label: "Groq",
    icon: Zap,
    cls: "bg-source-groq-bg text-source-groq",
    help: "Cloudflare 무료 한도가 끝나 Groq의 공개 모델(gpt-oss)이 대신 처리했어요.",
  },
  fallback: {
    label: "대체 구현",
    icon: Wrench,
    cls: "bg-source-fallback-bg text-source-fallback",
    help: "모델 대신 규칙 기반 대체 구현이 만들었어요. 품질 판정이 아니에요.",
  },
  replay: {
    label: "재현",
    icon: History,
    cls: "border border-dashed border-line-strong bg-source-replay-bg text-source-replay",
    help: "녹화한 예시를 다시 보여 주는 데모 데이터예요.",
  },
};

/**
 * 출처 라벨(DS-03). 누가 만들었는지를 알리는 분류이고 품질 판정이 아니다.
 * none이나 빈 값은 그리지 않는다. 모르는 값은 원문을 중립 색으로.
 */
export function SourceLabel({ kind, tooltip = true }: { kind: string | null | undefined; tooltip?: boolean }) {
  if (!kind || kind === "none") return null;
  const s = SOURCES[kind];
  if (!s)
    return <span className="rounded-sm bg-neutral-bg px-2 py-0.5 text-micro text-neutral">{kind}</span>;
  const Icon = s.icon;
  const cls = cx("inline-flex items-center gap-1 rounded-sm px-2 py-0.5 text-micro", s.cls);
  const content = (
    <>
      <Icon aria-hidden className="size-3.5" />
      {s.label}
      <span className="sr-only">: {s.help}</span>
    </>
  );
  // 링크 안처럼 포커스를 받으면 안 되는 곳에서는 툴팁 없이 title로만 설명한다.
  if (!tooltip)
    return (
      <span className={cls} title={s.help}>
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
      {s.help}
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
