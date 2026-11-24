import { sparkPoints } from "@/lib/ops";

/** 작은 추세선(Stephen Few). 장식이라 aria-hidden이고, 같은 내용은 옆 글로 전한다. */
export function Sparkline({ values, width = 96, height = 28 }: { values: number[]; width?: number; height?: number }) {
  return (
    <svg aria-hidden width={width} height={height} viewBox={`0 0 ${width} ${height}`} className="shrink-0 text-info">
      <polyline points={sparkPoints(values, width, height)} fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" />
    </svg>
  );
}
