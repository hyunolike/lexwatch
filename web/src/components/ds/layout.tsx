import type { ReactNode } from "react";
import { cx } from "@/lib/cx";

export type MascotPose = "avatar" | "empty-queue" | "empty-inbox" | "waking" | "error" | "master";

/**
 * 마스코트 "렉스"(DS-15). 장식이라 alt="" + aria-hidden. 이미 최적화한 WebP라 next/image를 거치지 않는다.
 * 금지 위치: 승인과 반려 버튼 주변, 확인 대화상자, 초안 본문, 신구조문, 실행 타임라인.
 */
export function Mascot({ pose, size, className }: { pose: MascotPose; size: number; className?: string }) {
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={`/mascot/lex-${pose}.webp`}
      alt=""
      aria-hidden
      width={size}
      height={size}
      draggable={false}
      className={cx("select-none", className)}
    />
  );
}

/** 빈 화면: 마스코트 위, 제목과 설명 아래. 렉스는 여유 있는 순간에만 작게(DS2-14, 최대 120px). */
export function EmptyState({
  pose,
  title,
  description,
  action,
}: {
  pose: Exclude<MascotPose, "avatar" | "master">;
  title: string;
  description?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center px-4 py-10 text-center">
      <Mascot pose={pose} size={120} />
      <p className="mt-4 font-serif text-title-md text-ink">{title}</p>
      {description && <div className="mt-1 max-w-md text-ui text-ink-muted">{description}</div>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

/** 항목명과 값 두 열(GOV.UK Summary list 패턴). */
export function SummaryList({ rows }: { rows: { term: string; value: ReactNode }[] }) {
  return (
    <dl className="grid grid-cols-[max-content_1fr] gap-x-6 gap-y-2 text-ui">
      {rows.map((r) => (
        <div key={r.term} className="contents">
          <dt className="text-ink-subtle">{r.term}</dt>
          <dd className="text-ink">{r.value}</dd>
        </div>
      ))}
    </dl>
  );
}

/** 카드: 변형이 없어 클래스 조합으로 둔다. */
export const CARD = "rounded-lg border border-line bg-surface shadow-card";

/** 페이지 머리: 제목과 오른쪽 보조 요소. */
export function PageHeader({ title, children }: { title: ReactNode; children?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h1 className="font-serif text-title-lg text-ink">{title}</h1>
      {children}
    </div>
  );
}
