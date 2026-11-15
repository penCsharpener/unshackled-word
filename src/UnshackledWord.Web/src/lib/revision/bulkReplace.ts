import type { BulkReplaceRow } from "./types";

export const BULK_REPLACE_KEY = "uw.bulkReplace";

export type QueueStatus = "pending" | "saved" | "reviewed" | "skipped";

/** One verse of the review queue: all checked hits of that verse. */
export interface QueueEntry {
  hebRefId: number;
  /** found forms (as written, with punctuation) of the checked hits */
  forms: string[];
  /** 1-based hit numbers of the checked hits among the matches of the verse */
  occurrences: number[];
  strongs: string[];
  articleBefore?: string;
  status: QueueStatus;
}

export interface BulkReplaceFilter {
  word: string;
  sameLemma: boolean;
  source: string;
  target: string;
  suggestion: string;
}

export interface StoredBulkReplace {
  filter: BulkReplaceFilter;
  queue: QueueEntry[];
}

export function splitRef(hebRefId: number): { bookId: number; chapter: number; verse: number } {
  return {
    bookId: Math.floor(hebRefId / 1_000_000),
    chapter: Math.floor(hebRefId / 1_000) % 1_000,
    verse: hebRefId % 1_000,
  };
}

const EDGE = /^([^\p{L}\p{N}]*)(.*?)([^\p{L}\p{N}]*)$/u;

/** Letters of a token without the punctuation around it, lower case: `Weibe,` -> `weibe`. */
export function plainForm(token: string): string {
  return (EDGE.exec(token)?.[2] ?? token).toLocaleLowerCase("de");
}

/** One queue entry per verse; rows without a checked flag are not part of it. */
export function buildQueue(rows: readonly BulkReplaceRow[]): QueueEntry[] {
  const byVerse = new Map<number, QueueEntry>();
  for (const row of rows) {
    const entry = byVerse.get(row.hebRefId) ?? {
      hebRefId: row.hebRefId,
      forms: [],
      occurrences: [],
      strongs: [],
      status: "pending" as const,
    };
    entry.forms.push(row.found);
    entry.occurrences.push(row.occurrence);
    for (const strongs of row.strongs)
      if (!entry.strongs.includes(strongs)) entry.strongs.push(strongs);
    entry.articleBefore ??= row.articleBefore;
    byVerse.set(row.hebRefId, entry);
  }
  return [...byVerse.values()].sort((a, b) => a.hebRefId - b.hebRefId);
}

/** A hit without an article before it can be reviewed in quick mode. */
export const isQuick = (row: Pick<BulkReplaceRow, "articleBefore">) => !row.articleBefore;

/**
 * Suggestion for the review text: replaces the checked hits (numbered among all matches of the found forms in `text`),
 * keeps punctuation around the token and carries an initial capital over to the replacement.
 */
export function applySuggestion(
  text: string,
  forms: readonly string[],
  occurrences: readonly number[],
  replacement: string,
): string {
  const wanted = new Set(forms.map(plainForm));
  const replace = replacement.trim();
  if (replace === "" || wanted.size === 0) return text;
  let match = 0;
  return text.replace(/\S+/g, (token) => {
    const parts = EDGE.exec(token);
    const core = parts?.[2] ?? "";
    if (!wanted.has(core.toLocaleLowerCase("de"))) return token;
    match++;
    if (!occurrences.includes(match)) return token;
    const first = core.charAt(0);
    const capital = first !== first.toLocaleLowerCase("de");
    const word = capital ? replace.charAt(0).toLocaleUpperCase("de") + replace.slice(1) : replace;
    return `${parts?.[1] ?? ""}${word}${parts?.[3] ?? ""}`;
  });
}

export function readStored(): StoredBulkReplace | undefined {
  try {
    const raw = localStorage.getItem(BULK_REPLACE_KEY);
    if (!raw) return undefined;
    const value = JSON.parse(raw) as StoredBulkReplace;
    return Array.isArray(value.queue) && value.filter ? value : undefined;
  } catch {
    return undefined;
  }
}

export function writeStored(value: StoredBulkReplace | undefined): void {
  try {
    if (value) localStorage.setItem(BULK_REPLACE_KEY, JSON.stringify(value));
    else localStorage.removeItem(BULK_REPLACE_KEY);
  } catch {
    // storage unavailable: the queue only lasts for this page view
  }
}

/** Verses still to do after a reload. */
export const openCount = (queue: readonly QueueEntry[]) =>
  queue.filter((entry) => entry.status === "pending" || entry.status === "saved").length;
