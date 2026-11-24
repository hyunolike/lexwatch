"use client";
import { useRef, useState, type ReactNode } from "react";
import { ratioFromKey, ratioFromPointer, SPLIT } from "@/lib/split";

/**
 * 두 칸 분할(DS2-07, design.md 3.4). 1280px 미만에서는 위아래로 쌓는다(CSS로만 전환해 서버 결과와 같다).
 * 오른쪽 칸은 헤더 아래에 붙어(sticky) 자기 안에서 스크롤한다. 왼쪽은 페이지와 함께 흐른다.
 * 분리선은 끌거나 화살표, Home, End로 움직인다. 비율은 저장하지 않는다.
 */
export function SplitPane({ left, right, label }: { left: ReactNode; right: ReactNode; label: string }) {
  const [ratio, setRatio] = useState<number>(SPLIT.initial);
  const box = useRef<HTMLDivElement>(null);
  const move = (clientX: number) => {
    const r = box.current?.getBoundingClientRect();
    if (r) setRatio(ratioFromPointer(clientX - r.left, r.width));
  };
  return (
    <div
      ref={box}
      className="grid grid-cols-1 gap-8 xl:gap-0 xl:grid-cols-[var(--lw-split)_12px_minmax(0,1fr)]"
      style={{ ["--lw-split" as string]: `${ratio}%` }}
    >
      <div className="min-w-0 xl:pr-4">
        {left}
      </div>
      <div
        role="separator"
        aria-orientation="vertical"
        aria-label={label}
        aria-valuemin={SPLIT.min}
        aria-valuemax={SPLIT.max}
        aria-valuenow={ratio}
        tabIndex={0}
        onPointerDown={(e) => {
          e.currentTarget.setPointerCapture(e.pointerId);
          move(e.clientX);
        }}
        onPointerMove={(e) => e.currentTarget.hasPointerCapture(e.pointerId) && move(e.clientX)}
        onKeyDown={(e) => {
          const next = ratioFromKey(ratio, e.key, e.shiftKey);
          if (next !== null) {
            e.preventDefault();
            setRatio(next);
          }
        }}
        className="group relative hidden cursor-col-resize touch-none justify-center rounded-sm xl:flex"
      >
        <span aria-hidden className="h-full w-px bg-line transition-colors duration-[120ms] group-hover:bg-line-strong group-focus-visible:bg-focus" />
      </div>
      <div className="min-w-0 xl:sticky xl:top-[calc(var(--lw-header-h)+1rem)] xl:max-h-[calc(100dvh-var(--lw-header-h)-8rem)] xl:self-start xl:overflow-y-auto xl:pl-4">
        {right}
      </div>
    </div>
  );
}
