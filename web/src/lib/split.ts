/** 분할 화면 비율(design.md 3.4). 초안 칸의 백분율. 저장하지 않는다. */
export const SPLIT = { initial: 58, min: 40, max: 70, step: 2, bigStep: 10 } as const;

export const clampRatio = (r: number) => Math.min(SPLIT.max, Math.max(SPLIT.min, Math.round(r)));

/** 포인터 위치(왼쪽 끝에서 px)를 비율로. */
export const ratioFromPointer = (x: number, total: number) => (total <= 0 ? SPLIT.initial : clampRatio((x / total) * 100));

/** 분리선 키보드 조작. 다루지 않는 키는 null. */
export function ratioFromKey(ratio: number, key: string, shift: boolean): number | null {
  const step = shift ? SPLIT.bigStep : SPLIT.step;
  switch (key) {
    case "ArrowLeft":
      return clampRatio(ratio - step);
    case "ArrowRight":
      return clampRatio(ratio + step);
    case "Home":
      return SPLIT.min;
    case "End":
      return SPLIT.max;
    default:
      return null;
  }
}
