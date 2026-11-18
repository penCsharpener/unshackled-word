import { useQuery } from "@tanstack/react-query";
import { retryUnlessRateLimited } from "@/lib/queryClient";
import { bibleApi } from "./api";
import type { ParsedReference, ParseReferenceResponse } from "./types";

export const referenceParseKeys = {
  parse: (q: string) => ["referenceParse", q] as const,
};

/**
 * What a query means as scripture references. Runs on the already submitted (debounced) query; the
 * answer for a text never changes, so it is cached for good. Idle for an empty query.
 */
export function useReferenceParse(q: string) {
  const trimmed = q.trim();
  return useQuery({
    queryKey: referenceParseKeys.parse(trimmed),
    queryFn: ({ signal }) => bibleApi.parseReference(trimmed, signal),
    enabled: trimmed !== "",
    staleTime: Number.POSITIVE_INFINITY,
    retry: retryUnlessRateLimited,
  });
}

/** Errors that only say "this is not a reference" (plain words); everything else is a hint to show. */
const NOT_A_REFERENCE = new Set(["unknownBook", "empty"]);

export interface ReferenceView {
  /** References with a chapter (linked as passage cards). */
  passages: ParsedReference[];
  /** Whole books (`Hiob`): a small link row, the full-text search keeps running. */
  books: ParsedReference[];
  /** Hint to show, null for plain text. */
  error: ParseReferenceResponse["error"] | null;
  /** The query is a reference (also a wrong one): the verse full-text search is skipped. */
  skipsFullText: boolean;
}

export function viewOfParse(data: ParseReferenceResponse | undefined): ReferenceView {
  const references = data?.references ?? [];
  const passages = references.filter((r) => !r.bookOnly);
  const error = data?.error && !NOT_A_REFERENCE.has(data.error.code) ? data.error : null;
  return {
    passages,
    books: references.filter((r) => r.bookOnly && r.bookId !== undefined),
    error,
    skipsFullText: passages.length > 0 || error !== null,
  };
}

/** The path of the first reference with a chapter, for Enter. */
export function firstReferencePath(data: ParseReferenceResponse | undefined): string | undefined {
  return data?.references.find((r) => r.path)?.path;
}

/** `Phili 1` with candidate `Philipper` becomes `Philipper 1`: only the leading book word is replaced. */
export function replaceTypedBook(input: string, bookName: string): string {
  const match = /^\s*(?:\d\.?\s*)?\p{L}[\p{L}.]*/u.exec(input);
  return match ? `${bookName}${input.slice(match[0].length)}` : input;
}

const refId = (bookId: number, chapter: number, verse: number) =>
  bookId * 1_000_000 + chapter * 1_000 + verse;

/** Most spans the `passageText` endpoint takes in one request. */
const PASSAGE_SPANS_MAX = 10;

/**
 * The `spans` parameter of `passageText` for a parsed reference (ref-id pairs, Hebrew counting), or
 * undefined when there is no text to fetch. Whole-chapter spans (verse 0) are left out: a card shows
 * text for verses, not for chapters.
 */
export function passageSpansParam(reference: ParsedReference): string | undefined {
  const { bookId, spans } = reference;
  if (bookId === undefined || !spans) return undefined;
  const parts = spans
    .filter((s) => s.startVerse > 0)
    .slice(0, PASSAGE_SPANS_MAX)
    .map((s) => {
      const start = refId(bookId, s.startChapter, s.startVerse);
      const end = refId(bookId, s.endChapter, s.endVerse);
      return end === start ? String(start) : `${start}-${end}`;
    });
  return parts.length > 0 ? parts.join(",") : undefined;
}
