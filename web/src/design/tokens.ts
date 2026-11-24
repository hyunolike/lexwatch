/**
 * tokens.css를 읽어 두 테마의 색 값을 얻고 대비를 계산한다. 대비 테스트와 /design-system 화면이 함께 쓴다.
 * 설계: docs/features/design-system-v2/design.md 2.5절
 */

export type Theme = "light" | "dark";
export type ThemeTokens = Record<Theme, Record<string, string>>;

/** 선택자 블록 하나의 `--lw-name: #hex;` 선언을 { name: "#hex" }로. 16진수가 아닌 값(그림자 등)은 건너뛴다. */
function readBlock(css: string, selector: RegExp): Record<string, string> {
  const m = selector.exec(css);
  if (!m) return {};
  const start = m.index + m[0].length;
  const body = css.slice(start, css.indexOf("}", start));
  const out: Record<string, string> = {};
  for (const d of body.matchAll(/--lw-([a-z0-9-]+)\s*:\s*(#[0-9a-fA-F]{6})\s*;/g)) out[d[1]] = d[2].toLowerCase();
  return out;
}

/** `@theme inline`의 별칭(`--color-neutral: var(--lw-ink-muted)`)처럼 이름이 다른 연결. */
function aliases(css: string): Record<string, string> {
  const out: Record<string, string> = {};
  for (const m of css.matchAll(/--color-([a-z0-9-]+)\s*:\s*var\(--lw-([a-z0-9-]+)\)\s*;/g)) if (m[1] !== m[2]) out[m[1]] = m[2];
  return out;
}

/** 밝은 테마(:root)와 어두운 테마([data-theme="dark"])의 색. 별칭도 풀어 넣는다. */
export function parseThemes(css: string): ThemeTokens {
  const light = readBlock(css, /(?:^|\n)\s*:root\s*\{/);
  const dark = readBlock(css, /(?:^|\n)\s*\[data-theme="dark"\]\s*\{/);
  for (const [name, target] of Object.entries(aliases(css))) {
    if (light[target]) light[name] = light[target];
    if (dark[target]) dark[name] = dark[target];
  }
  return { light, dark };
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

const SURFACES = ["canvas", "surface", "surface-raised", "surface-sunken"];
const STATUS = ["success", "danger", "warning", "info"];
export const PROV_TOKENS = ["prov-ai", "prov-rule", "prov-human", "prov-replay"];

/**
 * design.md 2.5절 대비 검사 쌍. 글자 4.5, UI 경계와 포커스 링 3.0.
 * 포커스 링은 늘 2px 띄워 링 옆이 바탕이므로 링과 버튼 면의 쌍은 검사하지 않는다.
 */
export const CONTRAST_PAIRS: ContrastPair[] = [
  ...pairs(["ink", "ink-muted", "ink-subtle", "brand-fg"], SURFACES, 4.5),
  { fg: "brand-fg", bg: "brand-subtle", min: 4.5 },
  ...pairs(["on-brand"], ["brand", "brand-hover"], 4.5),
  { fg: "on-danger", bg: "danger", min: 4.5 },
  ...STATUS.flatMap((t) => [
    { fg: t, bg: `${t}-bg`, min: 4.5 },
    { fg: t, bg: "surface", min: 4.5 },
    { fg: "ink", bg: `${t}-bg`, min: 4.5 },
    { fg: "ink-muted", bg: `${t}-bg`, min: 4.5 },
  ]),
  { fg: "neutral", bg: "neutral-bg", min: 4.5 },
  { fg: "canvas", bg: "ink", min: 4.5 },
  ...PROV_TOKENS.flatMap((t) => [
    { fg: t, bg: `${t}-bg`, min: 4.5 },
    { fg: t, bg: "surface", min: 4.5 },
  ]),
  ...["diff-del", "diff-ins"].map((t) => ({ fg: t, bg: `${t}-bg`, min: 4.5 })),
  ...pairs(["line-strong", "focus"], SURFACES, 3),
  ...pairs(["brand"], ["canvas", "surface"], 3),
];
