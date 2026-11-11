import { keepPreviousData, queryOptions, useQuery } from "@tanstack/react-query";
import { bibleApi } from "./api";
import type { StrongsOccurrenceFilters } from "./types";

export const STRONGS_PAGE_SIZE = 50;

export const strongsOccurrencesKeys = {
  page: (strongs: string, page: number, filters: StrongsOccurrenceFilters = {}) =>
    [
      "bible",
      "strongsOccurrences",
      strongs,
      page,
      filters.book ?? null,
      filters.rendering ?? null,
    ] as const,
};

export const strongsOccurrencesQuery = (
  strongs: string,
  page: number,
  filters: StrongsOccurrenceFilters = {},
) =>
  queryOptions({
    queryKey: strongsOccurrencesKeys.page(strongs, page, filters),
    queryFn: ({ signal }) =>
      bibleApi.strongsOccurrences(strongs, page, STRONGS_PAGE_SIZE, filters, signal),
    staleTime: Number.POSITIVE_INFINITY,
    placeholderData: keepPreviousData,
  });

/** One page of the verses where a (normalized, non-empty) Strong's number occurs, optionally for one book and/or German rendering. */
export function useStrongsOccurrences(
  strongs: string | undefined,
  page: number,
  filters: StrongsOccurrenceFilters = {},
) {
  return useQuery({
    ...strongsOccurrencesQuery(strongs ?? "", page, filters),
    enabled: strongs !== undefined,
  });
}

/** `?page=` as a positive integer; anything else is page 1. */
export function parsePage(value: string | null): number {
  const n = Number(value);
  return Number.isInteger(n) && n >= 1 ? n : 1;
}

/** `?book=` as an Elberfelder book id 1-66; anything else is no filter. */
export function parseBookFilter(value: string | null): number | undefined {
  if (value === null || !/^\d{1,2}$/.test(value)) return undefined;
  const n = Number(value);
  return n >= 1 && n <= 66 ? n : undefined;
}

/** `?rendering=` as a German key of 1-50 characters; anything else is no filter. */
export function parseRenderingFilter(value: string | null): string | undefined {
  const key = value?.trim().toLowerCase();
  return key && key.length <= 50 ? key : undefined;
}
