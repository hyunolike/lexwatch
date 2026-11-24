import { cx } from "@/lib/cx";
import { BILL_STEPS } from "@/lib/radar";

/** 법안 진행 걸음 표시(bill-radar BR-08). 현재 칸까지 채운다. 색만으로 뜻을 전하지 않도록 현재 칸 이름을 글로 함께 둔다. */
export function BillStepper({ step, label }: { step: number; label: string }) {
  return (
    <div className="space-y-1">
      <ol aria-label={`진행 단계: ${label}`} className="flex items-center gap-1">
        {BILL_STEPS.map((name, i) => (
          <li key={name} aria-current={i === step ? "step" : undefined} className="flex min-w-0 flex-1 flex-col gap-1">
            <span aria-hidden className={cx("h-1.5 rounded-full", i <= step ? "bg-brand" : "bg-line")} />
            <span className={cx("truncate text-micro", i === step ? "text-ink" : "text-ink-subtle")}>{name}</span>
          </li>
        ))}
      </ol>
    </div>
  );
}
