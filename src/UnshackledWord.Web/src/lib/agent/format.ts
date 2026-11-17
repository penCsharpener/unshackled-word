import type { AgentRun } from "./types";

/** Splits a BBCCCVVV verse id (e.g. 41001008 = Mark 1:8). */
export function decodeRefId(refId: number): { book: number; chapter: number; verse: number } {
  return {
    book: Math.floor(refId / 1_000_000),
    chapter: Math.floor(refId / 1000) % 1000,
    verse: refId % 1000,
  };
}

/** `Markus 1,1` / `Markus 1,1–8` / `Markus 1,1 – 2,5` for the verses of a run (German comma, like the reader). */
export function formatRunRange(run: Pick<AgentRun, "fromRefId" | "toRefId">, book: string): string {
  const from = decodeRefId(run.fromRefId);
  const to = decodeRefId(run.toRefId);
  const start = `${book} ${from.chapter},${from.verse}`;
  if (run.fromRefId === run.toRefId) return start;
  if (from.chapter === to.chapter) return `${start}–${to.verse}`;
  return `${start} – ${to.chapter},${to.verse}`;
}

export const formatTokens = (tokens: number, language: string): string =>
  new Intl.NumberFormat(language).format(tokens);

/** USD with cents precision, or four decimals below one cent (cheap local models). */
export function formatCost(usd: number, language: string): string {
  const digits = usd > 0 && usd < 0.01 ? 4 : 2;
  return new Intl.NumberFormat(language, {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  }).format(usd);
}

/**
 * Verses a lemma run skipped because they had no work, read from the run's status note
 * (`AgentRunExecutor.SkippedNote`: "… 12 verse(s) without work skipped."); 0 when the note does not say.
 */
export function skippedVersesFromNote(note: string | undefined): number {
  const match = note?.match(/(\d+) verse\(s\) without work skipped/);
  return match ? Number(match[1]) : 0;
}
