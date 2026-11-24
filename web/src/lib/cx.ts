import { extendTailwindMerge } from "tailwind-merge";

/**
 * LexWatch 토큰 이름을 tailwind-merge에 알린다. 알리지 않으면 `text-ui`(글자 크기)를 색으로 보고
 * `text-ink`와 충돌한 것으로 여겨 지운다(design-system-v2 design.md 1.1, research 3.1).
 * 이 목록은 tokens.css와 맞아야 한다. cx.test.ts가 tokens.css에서 이름을 읽어 확인한다.
 */
export const TEXT_SIZES = ["display", "title-lg", "title-md", "title-sm", "reading", "ui", "label", "caption", "micro"] as const;
export const SHADOWS = ["card", "pop", "dialog"] as const;
export const RADII = ["sm", "md", "lg"] as const;

const merge = extendTailwindMerge({
  extend: {
    classGroups: {
      "font-size": [{ text: [...TEXT_SIZES] }],
      shadow: [{ shadow: [...SHADOWS] }],
      rounded: [{ rounded: [...RADII] }],
    },
  },
});

/** 클래스 이름을 이어 붙이고 같은 속성끼리 충돌하면 뒤의 것만 남긴다. 거짓 값은 건너뛴다. */
export const cx = (...parts: (string | false | null | undefined)[]) => merge(parts.filter(Boolean).join(" "));
