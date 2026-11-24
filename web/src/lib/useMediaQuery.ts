"use client";
import { useSyncExternalStore } from "react";

/** CSS 미디어 쿼리를 구독한다. 서버와 첫 하이드레이션에서는 false(좁은 배치)로 그려 결과가 같다. */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (cb) => {
      const mq = window.matchMedia(query);
      mq.addEventListener("change", cb);
      return () => mq.removeEventListener("change", cb);
    },
    () => window.matchMedia(query).matches,
    () => false,
  );
}

/** 검토 상세가 초안과 근거를 나란히 놓는 폭(DS2-07). Tailwind xl과 같다. */
export const SPLIT_QUERY = "(min-width: 80rem)";
