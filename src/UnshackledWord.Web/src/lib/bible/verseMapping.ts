import {
  type QueryClient,
  queryOptions,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { bibleApi } from "./api";
import { bibleKeys } from "./queries";
import { sidePanelKeys } from "./sidePanel";
import type { GetVerseMappingResponse, GetWordsOfChapterResponse, VerseMappingLink } from "./types";
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

/**
 * Refreshes what shows a verse's mapping after it changed (own save or a merged agent proposal): the chapter words
 * (hover highlight), the side panel originals and the cached word details (popup). Optionally stores the fresh mapping.
 */
export async function refreshAfterMappingChange(
  queryClient: QueryClient,
  ref: VerseRef,
  fresh?: GetVerseMappingResponse,
) {
  if (fresh) queryClient.setQueryData(verseMappingKey(ref), fresh);
  const chapterKey = bibleKeys.chapterWordsOf(ref.bookId, ref.chapter);
  const chapterWordIds = new Set(
    queryClient
      .getQueriesData<GetWordsOfChapterResponse>({ queryKey: chapterKey })
      .flatMap(([, cached]) => cached?.words.map((w) => w.id) ?? []),
  );
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: chapterKey }),
    queryClient.invalidateQueries({ queryKey: sidePanelKeys.originalsAll }),
    queryClient.invalidateQueries({
      queryKey: wordDetailKeys.all,
      // chapter not cached: refresh every cached detail rather than miss one
      predicate: (query) =>
        chapterWordIds.size === 0 || chapterWordIds.has(query.queryKey[2] as number),
    }),
  ]);
}

/** PUT of the whole verse mapping; `clearParts` confirms that compound parts the verse has today may be removed. */
export function useSaveVerseMapping() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      ref,
      links,
      clearParts,
    }: {
      ref: VerseRef;
      links: VerseMappingLink[];
      clearParts?: boolean;
    }) => bibleApi.saveVerseMapping(ref.bookId, ref.chapter, ref.verse, links, clearParts),
    onSuccess: (response, { ref }) => refreshAfterMappingChange(queryClient, ref, response),
  });
}
