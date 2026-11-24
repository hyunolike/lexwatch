import { CircleAlert, CircleCheck, Info, TriangleAlert, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { cva } from "class-variance-authority";
import { cx } from "@/lib/cx";

type Tone = "info" | "success" | "warning" | "danger";

const ICON: Record<Tone, LucideIcon> = { info: Info, success: CircleCheck, warning: TriangleAlert, danger: CircleAlert };

/** 안내 상자 변형 표(DS2-15). 본문 글자는 ink, 아이콘만 상태 색이다(대비 쌍 ink/*-bg). */
export const calloutVariants = cva("flex gap-2.5 rounded-md border px-3 py-2.5 text-ui", {
  variants: {
    tone: {
      info: "border-info/30 bg-info-bg text-info",
      success: "border-success/30 bg-success-bg text-success",
      warning: "border-warning/30 bg-warning-bg text-warning",
      danger: "border-danger/30 bg-danger-bg text-danger",
    } satisfies Record<Tone, string>,
  },
});

/**
 * 안내 상자. live면 나타날 때 화면 읽기 프로그램이 읽는다(결정 결과, 오류).
 * 처음부터 있는 정적 안내(크리틱 적발)는 live 없이 둔다.
 */
export function Callout({
  tone,
  title,
  live = false,
  children,
  className,
}: {
  tone: Tone;
  title?: string;
  live?: boolean;
  children?: ReactNode;
  className?: string;
}) {
  const Icon = ICON[tone];
  return (
    <div
      role={live ? "status" : undefined}
      aria-live={live ? "polite" : undefined}
      className={cx(calloutVariants({ tone }), className)}
    >
      <Icon aria-hidden className="mt-0.5 size-4 shrink-0" />
      <div className="min-w-0 text-ink">
        {title && <p className="font-semibold">{title}</p>}
        {children}
      </div>
    </div>
  );
}
