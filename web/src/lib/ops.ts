/** 운영 현황 화면 계산(ops-visibility OV-17). 순수 함수, Vitest. */

/** 스파크라인 점. 값이 모두 0이면 바닥선. 폭과 높이 안에 맞춘다. */
export function sparkPoints(values: number[], width = 96, height = 28, pad = 2): string {
  if (values.length === 0) return "";
  const max = Math.max(...values, 0);
  const step = values.length > 1 ? (width - pad * 2) / (values.length - 1) : 0;
  return values
    .map((v, i) => {
      const x = pad + i * step;
      const y = max === 0 ? height - pad : height - pad - (v / max) * (height - pad * 2);
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(" ");
}

/** 0.043 → "4.3%", null → "—" */
export function pct(rate: number | null, digits = 1): string {
  if (rate === null || Number.isNaN(rate)) return "—";
  const v = rate * 100;
  return `${Number.isInteger(v) ? v : v.toFixed(digits)}%`;
}

/** 시간(h) 문구. 1시간 미만은 분. */
export function hoursText(h: number | null): string {
  if (h === null) return "—";
  if (h < 1) return `${Math.round(h * 60)}분`;
  if (h < 48) return `${Number.isInteger(h) ? h : h.toFixed(1)}시간`;
  return `${(h / 24).toFixed(1)}일`;
}

/** 100% 누적 막대 조각. 0인 항목은 뺀다. 합이 0이면 빈 배열. */
export function stackParts(entries: [string, number][]): { key: string; value: number; share: number }[] {
  const total = entries.reduce((s, [, v]) => s + v, 0);
  if (total === 0) return [];
  return entries.filter(([, v]) => v > 0).map(([key, value]) => ({ key, value, share: value / total }));
}

export function maxOf(values: (number | null)[]): number {
  return values.reduce<number>((m, v) => (v !== null && v > m ? v : m), 0);
}
