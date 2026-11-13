import { keepPreviousData, useInfiniteQuery } from "@tanstack/react-query";
import { ApiError } from "@/lib/api";
import { bibleApi } from "./api";
import { isLookupQuery } from "./strongsStats";

export const VERSE_SEARCH_PAGE_SIZE = 20;

export const verseSearchKeys = {
  verses: (q: string, bookId: number | null, pageSize: number) =>
    ["bible", "verseSearch", q, bookId ?? 0, pageSize] as const,
};

/** The server accepts at most 50 characters per word. */
const MAX_WORD_LENGTH = 50;

/** A query the verse search endpoint accepts (1-200 characters, at most 8 words of at most 50 characters). */
export function isVerseSearchQuery(q: string): boolean {
  const trimmed = q.trim();
  return (
    isLookupQuery(trimmed) && trimmed.split(/\s+/).every((word) => word.length <= MAX_WORD_LENGTH)
  );
}

/**
 * Words of a search query for the per-word originals lookup: operators are dropped (quotes, excluded
 * `-words`, the alternative marker `oder` / `or`).
 */
export function originalsQueryOf(q: string): string {
  return q
    .replace(/["„“”]/g, " ")
    .split(/\s+/)
    .filter((word) => word !== "" && !word.startsWith("-") && !/^(oder|or)$/i.test(word))
    .join(" ");
}

/**
 * Ranked verse hits for a query, one page per fetch (`fetchNextPage` = "load more"). The request is
 * aborted when the query changes and the previous result stays while the next one loads
 * (`isPlaceholderData`). Idle for an invalid query; no retry on 400 (the query itself is wrong).
 */
export function useVerseSearch(
  q: string,
  bookId: number | null = null,
  pageSize = VERSE_SEARCH_PAGE_SIZE,
) {
  const trimmed = q.trim();
  return useInfiniteQuery({
    queryKey: verseSearchKeys.verses(trimmed, bookId, pageSize),
    queryFn: ({ pageParam, signal }) =>
      bibleApi.searchVerses(trimmed, { page: pageParam, pageSize, bookId }, signal),
    initialPageParam: 1,
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.total ? last.page + 1 : undefined,
    enabled: isVerseSearchQuery(trimmed),
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
    retry: (count, error) => !(error instanceof ApiError && error.status === 400) && count < 1,
  });
}
