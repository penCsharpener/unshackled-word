import { queryOptions, useQuery } from "@tanstack/react-query";
import { BASE_LEVEL, bibleApi } from "./api";
import type { ChapterRef } from "./types";
import { groupWordsByVerse } from "./words";

export const bibleKeys = {
  structure: ["bible", "structure"] as const,
  greekEditions: ["bible", "greekEditions"] as const,
  chapterText: (bookId: number, chapter: number, level = BASE_LEVEL) =>
    ["bible", "chapterText", bookId, chapter, level] as const,
  chapterWords: (bookId: number, chapter: number, level = BASE_LEVEL) =>
    ["bible", "chapterWords", bookId, chapter, level] as const,
  verseCrossReferenceTexts: (bookId: number, chapter: number, verse: number, level = BASE_LEVEL) =>
    ["bible", "crossReferenceTexts", bookId, chapter, verse, level] as const,
  /** Prefix of every level of one chapter. */
  chapterWordsOf: (bookId: number, chapter: number) =>
    ["bible", "chapterWords", bookId, chapter] as const,
  /** Prefix of all cached chapters (text and words, every level). */
  chapters: [
    ["bible", "chapterText"],
    ["bible", "chapterWords"],
  ] as const,
};

export const structureQuery = () =>
  queryOptions({
    queryKey: bibleKeys.structure,
    queryFn: ({ signal }) => bibleApi.structure(signal),
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: Number.POSITIVE_INFINITY,
    select: (response) => response.books,
  });

export const chapterTextQuery = (bookId: number, chapter: number, level = BASE_LEVEL) =>
  queryOptions({
    queryKey: bibleKeys.chapterText(bookId, chapter, level),
    queryFn: ({ signal }) => bibleApi.chapterText(bookId, chapter, level, signal),
    staleTime: Number.POSITIVE_INFINITY,
  });

export function useBibleStructure() {
  return useQuery(structureQuery());
}

export function useChapterVerses(ref: ChapterRef | null, level = BASE_LEVEL) {
  return useQuery({
    ...chapterTextQuery(ref?.bookId ?? 0, ref?.chapter ?? 0, level),
    enabled: ref !== null,
    select: (response) => response.verses,
  });
}

export const chapterWordsQuery = (bookId: number, chapter: number, level = BASE_LEVEL) =>
  queryOptions({
    queryKey: bibleKeys.chapterWords(bookId, chapter, level),
    queryFn: ({ signal }) => bibleApi.chapterWords(bookId, chapter, level, signal),
    staleTime: Number.POSITIVE_INFINITY,
    select: (response) => groupWordsByVerse(response.words, response.revisedVerses),
  });

export function useChapterWords(ref: ChapterRef | null, level = BASE_LEVEL) {
  return useQuery({
    ...chapterWordsQuery(ref?.bookId ?? 0, ref?.chapter ?? 0, level),
    enabled: ref !== null,
  });
}

/** Text of reference spans (`66002001-66002005,66002007`), as asked for by a reference card or preview. */
export const passageTextQuery = (spans: string, level = BASE_LEVEL) =>
  queryOptions({
    queryKey: ["bible", "passageText", spans, level] as const,
    queryFn: ({ signal }) => bibleApi.passageText(spans, level, signal),
    staleTime: Number.POSITIVE_INFINITY,
  });

/** Texts of all TSK targets of one verse: one request, cached for the page, so every link of the verse shows its text on hover. */
export const verseCrossReferenceTextsQuery = (
  bookId: number,
  chapter: number,
  verse: number,
  level = BASE_LEVEL,
) =>
  queryOptions({
    queryKey: bibleKeys.verseCrossReferenceTexts(bookId, chapter, verse, level),
    queryFn: ({ signal }) =>
      bibleApi.verseCrossReferenceTexts(bookId, chapter, verse, level, signal),
    staleTime: Number.POSITIVE_INFINITY,
  });

/** Catalog of the Greek text editions (roadmap 0025); changes only with a migration, so it is never refetched. */
export const greekEditionsQuery = () =>
  queryOptions({
    queryKey: bibleKeys.greekEditions,
    queryFn: ({ signal }) => bibleApi.greekEditions(signal),
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: Number.POSITIVE_INFINITY,
    select: (response) => response.editions,
  });

export function useGreekEditions(enabled = true) {
  return useQuery({ ...greekEditionsQuery(), enabled });
}
