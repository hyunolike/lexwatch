import { CircleCheck, Circle, TriangleAlert, type LucideIcon } from "lucide-react";
import { cva } from "class-variance-authority";
import { CHECK_LABEL, type CheckState } from "@/lib/checkState";

const ICON: Record<CheckState, LucideIcon> = { passed: CircleCheck, flagged: TriangleAlert, unchecked: Circle };

/** 상태 색을 쓴다. 출처 라벨(SourceLabel)과 모양도 다르다: 배경 없는 아이콘과 글자(DS2-06). */
const checkVariants = cva("inline-flex items-center gap-1 text-caption font-semibold", {
  variants: {
    state: {
      passed: "text-success",
      flagged: "text-danger",
      unchecked: "text-ink-subtle",
    } satisfies Record<CheckState, string>,
  },
});

/** 문장별 검증 상태. 기호는 장식이고 글자가 뜻을 말한다. */
export function CheckMark({ state }: { state: CheckState }) {
  const Icon = ICON[state];
  return (
    <span className={checkVariants({ state })}>
      <Icon aria-hidden className="size-3.5" />
      {CHECK_LABEL[state]}
    </span>
  );
}
