export const MAX_DONUT_SLOTS = 8;

export function formatNumber(value: number, language: string): string {
  return new Intl.NumberFormat(language).format(value);
}

/** `part / total` as a percent with at most one decimal, e.g. "20,2 %". */
export function formatPercent(part: number, total: number, language: string): string {
  const ratio = total > 0 ? part / total : 0;
  return new Intl.NumberFormat(language, { style: "percent", maximumFractionDigits: 1 }).format(
    ratio,
  );
}

export interface DonutSegment {
  /** Visible arc length in units of 100 (full circle). */
  length: number;
  /** Start of the arc in units of 100. */
  start: number;
}

/**
 * Arcs of a ring of circumference 100. A gap is cut off the end of every arc when there is more
 * than one, so neighbouring slices do not touch.
 */
export function donutSegments(values: readonly number[], gap: number): DonutSegment[] {
  const total = values.reduce((sum, v) => sum + v, 0);
  if (total <= 0) return values.map(() => ({ length: 0, start: 0 }));
  const useGap = values.length > 1 ? gap : 0;
  let start = 0;
  return values.map((value) => {
    const share = (value / total) * 100;
    const segment = { start, length: Math.max(share - useGap, 0.4) };
    start += share;
    return segment;
  });
}
