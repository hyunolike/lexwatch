import { cx } from "@/lib/cx";
import { CARD } from "./layout";

/**
 * 불러오는 동안 실제 배치와 같은 자리를 잡는 회색 막대(DS2-13). 장식이라 aria-hidden이고,
 * 감싸는 WakeGate가 aria-busy와 이름을 단다. 움직임 줄이기 설정에서는 반짝이지 않는다.
 */
export function Skeleton({ className }: { className?: string }) {
  return <span aria-hidden className={cx("block animate-pulse rounded-sm bg-surface-sunken motion-reduce:animate-none", className)} />;
}

/** 카드 안의 행 n줄. 대기함, 레이더, 운영 현황이 쓴다. */
export function SkeletonRows({ rows = 3, title = true }: { rows?: number; title?: boolean }) {
  return (
    <div className={cx(CARD, "overflow-hidden")}>
      {title && (
        <div className="border-b border-line px-4 py-3">
          <Skeleton className="h-4 w-24" />
        </div>
      )}
      <div className="divide-y divide-line">
        {Array.from({ length: rows }, (_, i) => (
          <div key={i} className="flex items-center gap-4 px-4 py-4">
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-2/3" />
              <Skeleton className="h-3 w-1/4" />
            </div>
            <Skeleton className="hidden h-5 w-20 md:block" />
            <Skeleton className="hidden h-5 w-16 md:block" />
          </div>
        ))}
      </div>
    </div>
  );
}

/** 검토 상세: 제목 줄, 문장 카드 셋, 1280px 이상은 오른쪽 패널. */
export function ReviewSkeleton() {
  return (
    <div className="space-y-5">
      <Skeleton className="h-8 w-1/2" />
      <Skeleton className="h-16 w-full" />
      <div className="grid gap-8 xl:grid-cols-[58%_1fr]">
        <div className="space-y-3">
          {[0, 1, 2].map((i) => (
            <div key={i} className={cx(CARD, "space-y-2 p-6")}>
              <Skeleton className="h-3 w-12" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-11/12" />
              <Skeleton className="h-4 w-3/5" />
            </div>
          ))}
        </div>
        <div className="hidden space-y-3 xl:block">
          <Skeleton className="h-4 w-1/3" />
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-40 w-full" />
        </div>
      </div>
    </div>
  );
}

/** 운영 현황: 숫자 타일 넷과 막대 여섯 줄. */
export function OpsSkeleton() {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <div key={i} className={cx(CARD, "space-y-2 p-4")}>
            <Skeleton className="h-3 w-20" />
            <Skeleton className="h-7 w-16" />
          </div>
        ))}
      </div>
      <SkeletonRows rows={6} />
    </div>
  );
}
