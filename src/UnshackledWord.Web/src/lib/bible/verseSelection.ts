/** Highest verse number `?v=` accepts; the longest chapter (Psalm 119) has 176 verses. */
const MAX_VERSE = 999;
const SPAN = /^(\d+)(?:\s*-\s*(\d+))?$/;

/**
 * Verses named by the reader's `?v=` parameter: `1-5,7,9-10` → 1, 2, 3, 4, 5, 7, 9, 10.
 * This is the app's own deep-link format for one chapter, not a reference parser. Parts that do not
 * parse, start below 1, end above 999 or run backwards (`x`, `0`, `5-3`) are skipped one by one.
 */
export function parseVerseSelection(value: string | null | undefined): Set<number> {
  const verses = new Set<number>();
  if (!value) return verses;
  for (const part of value.split(",")) {
    const match = SPAN.exec(part.trim());
    if (!match) continue;
    const start = Number(match[1]);
    const end = match[2] === undefined ? start : Number(match[2]);
    if (start < 1 || end > MAX_VERSE || start > end) continue;
    for (let verse = start; verse <= end; verse++) verses.add(verse);
  }
  return verses;
}
