import { queryOptions, useQuery } from "@tanstack/react-query";
import { lemmaApi } from "./api";

export const lemmaKeys = {
  valueMaps: ["lemmas", "valueMaps"] as const,
  chapter: (bookId: number, chapter: number) => ["lemmas", "chapter", bookId, chapter] as const,
};

export function useLemmaValueMaps(enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: lemmaKeys.valueMaps,
      queryFn: ({ signal }) => lemmaApi.valueMaps(signal),
      staleTime: Number.POSITIVE_INFINITY,
      enabled,
    }),
  );
}

export function useLemmaChapter(bookId: number | undefined, chapter: number | undefined) {
  return useQuery(
    queryOptions({
      queryKey: lemmaKeys.chapter(bookId ?? 0, chapter ?? 0),
      queryFn: ({ signal }) => lemmaApi.chapter(bookId as number, chapter as number, signal),
      // Edited data: always re-read on entering the page so a stale copy never hides other edits.
      staleTime: 0,
      gcTime: 0,
      refetchOnWindowFocus: false,
      enabled: bookId !== undefined && chapter !== undefined,
    }),
  );
}
