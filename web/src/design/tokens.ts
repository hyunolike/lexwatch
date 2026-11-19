/**
 * tokens.css를 읽어 색 값을 얻고 대비를 계산한다. 대비 테스트와 /design-system 화면이 함께 쓴다.
 * 설계: docs/features/design-system/design.md 2.1, 2.2절
 */

/** `--color-name: #hex;` 선언을 { name: "#hex" }로. 16진수가 아닌 값(initial 등)은 건너뛴다. */
export function parseColorTokens(css: string): Record<string, string> {
  const out: Record<string, string> = {};
  for (const m of css.matchAll(/--color-([a-z0-9-]+)\s*:\s*(#[0-9a-fA-F]{6})\s*;/g)) out[m[1]] = m[2].toLowerCase();
  return out;
}

function channels(hex: string): [number, number, number] {
  const h = hex.replace("#", "");
  return [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16) / 255) as [number, number, number];
}

function luminance(hex: string): number {
  const [r, g, b] = channels(hex).map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** WCAG 대비. 1~21. */
export function contrastRatio(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

/** 색상각(0~360), 채도와 명도(0~1). */
export function hsv(hex: string): { h: number; s: number; v: number } {
  const [r, g, b] = channels(hex);
  const max = Math.max(r, g, b);
  const d = max - Math.min(r, g, b);
  let h = 0;
  if (d !== 0) {
    if (max === r) h = ((g - b) / d) % 6;
    else if (max === g) h = (b - r) / d + 2;
    else h = (r - g) / d + 4;
    h *= 60;
    if (h < 0) h += 360;
  }
  return { h, s: max === 0 ? 0 : d / max, v: max };
}

export type ContrastPair = { fg: string; bg: string; min: number };

const pairs = (fgs: string[], bgs: string[], min: number): ContrastPair[] =>
  fgs.flatMap((fg) => bgs.map((bg) => ({ fg, bg, min })));

const BACKGROUNDS = ["canvas", "surface", "surface-sunken"];
const TONES = ["success", "danger", "warning", "info", "neutral"];
export const SOURCE_TOKENS = ["source-nvidia", "source-cloudflare", "source-fallback", "source-replay"];

/** design.md 2.2절 대비 검사 쌍. 글자 4.5, UI 경계와 포커스 링 3.0. */
export const CONTRAST_PAIRS: ContrastPair[] = [
  ...pairs(["ink", "ink-muted", "ink-subtle"], BACKGROUNDS, 4.5),
  ...pairs(["brand"], ["canvas", "surface"], 4.5),
  ...pairs(["on-brand"], ["brand", "danger"], 4.5),
  ...TONES.map((t) => ({ fg: t, bg: `${t}-bg`, min: 4.5 })),
  ...SOURCE_TOKENS.map((t) => ({ fg: t, bg: `${t}-bg`, min: 4.5 })),
  ...["diff-del", "diff-ins"].map((t) => ({ fg: t, bg: `${t}-bg`, min: 4.5 })),
  ...pairs(["line-strong"], BACKGROUNDS, 3),
  ...pairs(["focus"], [...BACKGROUNDS, "brand"], 3),
];
