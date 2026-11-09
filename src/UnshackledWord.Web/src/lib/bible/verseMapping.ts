import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { bibleApi } from "./api";
import { bibleKeys } from "./queries";
import type { GetWordsOfChapterResponse, VerseMappingLink } from "./types";
import { wordDetailKeys } from "./wordDetail";

export interface VerseRef {
  bookId: number;
  chapter: number;
  verse: number;
}

export const verseMappingKey = (ref: VerseRef) =>
  ["bible", "verseMapping", ref.bookId, ref.chapter, ref.verse] as const;

/** Never kept after the editor closes (gcTime 0), so every opening loads the current mapping. */
export const verseMappingQuery = (ref: VerseRef) =>
  queryOptions({
    queryKey: verseMappingKey(ref),
    queryFn: ({ signal }) => bibleApi.verseMapping(ref.bookId, ref.chapter, ref.verse, signal),
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: 0,
  });

/** Mapping of the verse being edited; disabled while `ref` is null. */
export function useVerseMapping(ref: VerseRef | null) {
  return useQuery({
    ...verseMappingQuery(ref ?? { bookId: 0, chapter: 0, verse: 0 }),
    enabled: ref !== null,
  });
}

/** PUT of the whole verse mapping; refreshes the chapter words (hover highlight) and the word details (popup). */
export function useSaveVerseMapping() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ ref, links }: { ref: VerseRef; links: VerseMappingLink[] }) =>
      bibleApi.saveVerseMapping(ref.bookId, ref.chapter, ref.verse, links),
    onSuccess: async (response, { ref }) => {
      queryClient.setQueryData(verseMappingKey(ref), response);
      const chapterKey = bibleKeys.chapterWords(ref.bookId, ref.chapter);
      const cached = queryClient.getQueryData<GetWordsOfChapterResponse>(chapterKey);
      const chapterWordIds = new Set(cached?.words.map((w) => w.id));
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: chapterKey }),
        queryClient.invalidateQueries({
          queryKey: wordDetailKeys.all,
          // chapter not cached: refresh every cached detail rather than miss one
          predicate: (query) =>
            chapterWordIds.size === 0 || chapterWordIds.has(query.queryKey[2] as number),
        }),
      ]);
    },
  });
}
