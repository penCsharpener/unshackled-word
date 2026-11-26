import { keepPreviousData, useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { ApiError, api } from "@/lib/api";
import { retryUnlessRateLimited } from "@/lib/queryClient";
import { BASE_LEVEL, bibleApi } from "./api";
import { isLookupQuery } from "./strongsStats";

export const VERSE_SEARCH_PAGE_SIZE = 20;

export const verseSearchKeys = {
  verses: (q: string, bookId: number | null, pageSize: number, level: number) =>
    ["bible", "verseSearch", q, bookId ?? 0, pageSize, level] as const,
  chapter: (q: string, bookId: number, chapter: number, level: number) =>
    ["bible", "verseSearch", "chapter", q, bookId, chapter, level] as const,
  neighbours: (q: string, bookId: number, chapter: number, level: number) =>
    ["bible", "verseSearch", "neighbours", q, bookId, chapter, level] as const,
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
 * (`isPlaceholderData`). Idle for an invalid query or until `ready` (the reader level is known); no retry
 * on 400 (the query itself is wrong). `level` is the reader's revision level: revised wording is searched too.
 */
export function useVerseSearch(
  q: string,
  bookId: number | null = null,
  pageSize = VERSE_SEARCH_PAGE_SIZE,
  level = BASE_LEVEL,
  ready = true,
) {
  const trimmed = q.trim();
  return useInfiniteQuery({
    queryKey: verseSearchKeys.verses(trimmed, bookId, pageSize, level),
    queryFn: ({ pageParam, signal }) =>
      bibleApi.searchVerses(trimmed, { page: pageParam, pageSize, bookId, level }, signal),
    initialPageParam: 1,
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.total ? last.page + 1 : undefined,
    enabled: ready && isVerseSearchQuery(trimmed),
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
    retry: (count, error) =>
      !(error instanceof ApiError && error.status === 400) && retryUnlessRateLimited(count, error),
  });
}

/** Matched words of one verse (roadmap 0029.10). */
export interface VerseChapterMatch {
  verse: number;
  /** True: `positions` refer to the revision variant (`position` of the shown words); else to Elb1871 (`basePosition`). */
  revised?: boolean;
  positions: number[];
}

export interface SearchVersesChapterResponse {
  verses: VerseChapterMatch[];
  /** Matched words in the chapter. */
  total: number;
}

export interface VerseChapterRef {
  bibleBookId: number;
  chapter: number;
}

export interface SearchVersesNeighboursResponse {
  previous?: VerseChapterRef;
  next?: VerseChapterRef;
}

const noRetryOn400 = (count: number, error: unknown) =>
  !(error instanceof ApiError && error.status === 400) && retryUnlessRateLimited(count, error);

/**
 * Matches of a verse query in one chapter at the reader level (reader marks, roadmap 0029.10f); idle for an empty or
 * invalid query.
 */
export function useVerseChapterMatches(q: string, bookId: number, chapter: number, level: number) {
  return useQuery({
    queryKey: verseSearchKeys.chapter(q, bookId, chapter, level),
    queryFn: ({ signal }) =>
      api.get<SearchVersesChapterResponse>(
        `/elberfelder/search/verses/chapter/${bookId}/${chapter}?${new URLSearchParams({ q, level: String(level) })}`,
        signal,
      ),
    enabled: isVerseSearchQuery(q),
    staleTime: 5 * 60_000,
    retry: noRetryOn400,
  });
}

/** The previous and next chapter with a hit of a verse query at the reader level; idle for an empty or invalid query. */
export function useVerseNeighbours(q: string, bookId: number, chapter: number, level: number) {
  return useQuery({
    queryKey: verseSearchKeys.neighbours(q, bookId, chapter, level),
    queryFn: ({ signal }) =>
      api.get<SearchVersesNeighboursResponse>(
        `/elberfelder/search/verses/neighbours?${new URLSearchParams({
          q,
          level: String(level),
          bibleBookId: String(bookId),
          chapter: String(chapter),
        })}`,
        signal,
      ),
    enabled: isVerseSearchQuery(q),
    staleTime: 5 * 60_000,
    retry: noRetryOn400,
  });
}
