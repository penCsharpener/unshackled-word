import { queryOptions, useQuery } from "@tanstack/react-query";
import { bibleApi } from "./api";
import type { ChapterRef } from "./types";

export const bibleKeys = {
  structure: ["bible", "structure"] as const,
  chapterText: (bookId: number, chapter: number) =>
    ["bible", "chapterText", bookId, chapter] as const,
};

export const structureQuery = () =>
  queryOptions({
    queryKey: bibleKeys.structure,
    queryFn: ({ signal }) => bibleApi.structure(signal),
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: Number.POSITIVE_INFINITY,
    select: (response) => response.books,
  });

export const chapterTextQuery = (bookId: number, chapter: number) =>
  queryOptions({
    queryKey: bibleKeys.chapterText(bookId, chapter),
    queryFn: ({ signal }) => bibleApi.chapterText(bookId, chapter, signal),
    staleTime: Number.POSITIVE_INFINITY,
  });

export function useBibleStructure() {
  return useQuery(structureQuery());
}

export function useChapterVerses(ref: ChapterRef | null) {
  return useQuery({
    ...chapterTextQuery(ref?.bookId ?? 0, ref?.chapter ?? 0),
    enabled: ref !== null,
    select: (response) => response.verses,
  });
}
