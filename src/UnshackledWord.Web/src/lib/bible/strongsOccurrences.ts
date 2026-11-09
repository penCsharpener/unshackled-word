import { keepPreviousData, queryOptions, useQuery } from "@tanstack/react-query";
import { bibleApi } from "./api";

export const STRONGS_PAGE_SIZE = 50;

export const strongsOccurrencesKeys = {
  page: (strongs: string, page: number) => ["bible", "strongsOccurrences", strongs, page] as const,
};

export const strongsOccurrencesQuery = (strongs: string, page: number) =>
  queryOptions({
    queryKey: strongsOccurrencesKeys.page(strongs, page),
    queryFn: ({ signal }) => bibleApi.strongsOccurrences(strongs, page, STRONGS_PAGE_SIZE, signal),
    staleTime: Number.POSITIVE_INFINITY,
    placeholderData: keepPreviousData,
  });

/** One page of the verses where a (normalized, non-empty) Strong's number occurs. */
export function useStrongsOccurrences(strongs: string | undefined, page: number) {
  return useQuery({
    ...strongsOccurrencesQuery(strongs ?? "", page),
    enabled: strongs !== undefined,
  });
}

/** `?page=` as a positive integer; anything else is page 1. */
export function parsePage(value: string | null): number {
  const n = Number(value);
  return Number.isInteger(n) && n >= 1 ? n : 1;
}
