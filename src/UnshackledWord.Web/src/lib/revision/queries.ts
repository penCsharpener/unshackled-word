import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { bibleKeys } from "@/lib/bible/queries";
import type { VerseMappingLink } from "@/lib/bible/types";
import { revisionApi } from "./api";
import type {
  BulkReplaceSearchRequest,
  RevisionVerseRef,
  SaveRevisionLevelRequest,
  SaveRevisionRequest,
} from "./types";

export const revisionKeys = {
  levels: ["revision", "levels"] as const,
  verse: (ref: RevisionVerseRef) =>
    ["revision", "verse", ref.bookId, ref.chapter, ref.verse] as const,
  history: (ref: RevisionVerseRef, level: number) =>
    ["revision", "history", ref.bookId, ref.chapter, ref.verse, level] as const,
  levelDetails: ["revision", "levelDetails"] as const,
  mapping: (revisionVerseId: number) => ["revision", "mapping", revisionVerseId] as const,
  /** Every cached mapping; the words of a verse get new ids on each text save. */
  mappings: ["revision", "mapping"] as const,
};

export function useRevisionLevels(enabled = true) {
  return useQuery(
    queryOptions({
      queryKey: revisionKeys.levels,
      queryFn: ({ signal }) => revisionApi.levels(signal),
      staleTime: Number.POSITIVE_INFINITY,
      enabled,
    }),
  );
}

/** Base text and active variants of one verse; never kept after the editor closes, so every opening is current. */
export function useRevisionVerse(ref: RevisionVerseRef | null) {
  const key = ref ?? { bookId: 0, chapter: 0, verse: 0 };
  return useQuery(
    queryOptions({
      queryKey: revisionKeys.verse(key),
      queryFn: ({ signal }) => revisionApi.verse(key, signal),
      staleTime: 0,
      gcTime: 0,
      refetchOnWindowFocus: false,
      enabled: ref !== null,
    }),
  );
}

export function useSaveRevision(ref: RevisionVerseRef) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveRevisionRequest) => revisionApi.saveVerse(ref, body),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: revisionKeys.verse(ref) }),
        // a text save keeps the verse id but rewrites its words (new ids, carried mapping): an open mapping is stale
        queryClient.invalidateQueries({ queryKey: revisionKeys.mappings }),
        queryClient.invalidateQueries({ queryKey: ["revision", "history"] }),
        // the reader shows the new wording at once
        ...bibleKeys.chapters.map((queryKey) =>
          queryClient.invalidateQueries({ queryKey: [...queryKey, ref.bookId, ref.chapter] }),
        ),
      ]),
  });
}

/** All versions of the verse at one level (writers only); fresh on every opening. */
export function useRevisionHistory(
  ref: RevisionVerseRef,
  level: number | undefined,
  enabled: boolean,
) {
  const id = level ?? 0;
  return useQuery(
    queryOptions({
      queryKey: revisionKeys.history(ref, id),
      queryFn: ({ signal }) => revisionApi.history(ref, id, signal),
      staleTime: 0,
      gcTime: 0,
      refetchOnWindowFocus: false,
      enabled: enabled && level !== undefined,
    }),
  );
}

/** Withdraws the variant of the verse at its level (the row is deleted); the reader falls back to the next variant towards Elb1871. */
export function useWithdrawRevision(ref: RevisionVerseRef) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (revisionVerseId: number) => revisionApi.withdraw(revisionVerseId),
    onSuccess: () => {
      // the verse row and its words are gone: its mapping must not be served from the cache
      queryClient.removeQueries({ queryKey: revisionKeys.mappings });
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: revisionKeys.verse(ref) }),
        queryClient.invalidateQueries({ queryKey: ["revision", "history"] }),
        ...bibleKeys.chapters.map((queryKey) =>
          queryClient.invalidateQueries({ queryKey: [...queryKey, ref.bookId, ref.chapter] }),
        ),
      ]);
    },
  });
}

export function useRevisionLevelDetails(enabled = true) {
  return useQuery(
    queryOptions({
      queryKey: revisionKeys.levelDetails,
      queryFn: ({ signal }) => revisionApi.levelDetails(signal),
      staleTime: 0,
      enabled,
    }),
  );
}

/** Creates (no `currentLevel`) or updates a preset; the slider presets and the maintainer list reload. */
export function useSaveRevisionLevel() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      currentLevel,
      body,
    }: {
      currentLevel?: number;
      body: SaveRevisionLevelRequest;
    }) =>
      currentLevel === undefined
        ? revisionApi.createLevel(body)
        : revisionApi.updateLevel(currentLevel, body),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: revisionKeys.levels }),
        queryClient.invalidateQueries({ queryKey: revisionKeys.levelDetails }),
      ]),
  });
}

/** Mapping of one variant; fresh on every opening because a text save elsewhere rewrites its words (the verse id stays). */
export function useRevisionMapping(revisionVerseId: number | null) {
  const id = revisionVerseId ?? 0;
  return useQuery(
    queryOptions({
      queryKey: revisionKeys.mapping(id),
      queryFn: ({ signal }) => revisionApi.verseMapping(id, signal),
      staleTime: 0,
      gcTime: 0,
      refetchOnWindowFocus: false,
      enabled: revisionVerseId !== null,
    }),
  );
}

export function useSaveRevisionMapping() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, links }: { id: number; links: VerseMappingLink[] }) =>
      revisionApi.saveVerseMapping(id, links),
    onSuccess: (data, { id }) => {
      queryClient.setQueryData(revisionKeys.mapping(id), data);
      // chapter words carry the original words of every German word
      return Promise.all([
        queryClient.invalidateQueries({
          queryKey: revisionKeys.verse({
            bookId: data.bibleBookId,
            chapter: data.chapter,
            verse: data.verse,
          }),
        }),
        ...bibleKeys.chapters.map((queryKey) =>
          queryClient.invalidateQueries({
            queryKey: [...queryKey, data.bibleBookId, data.chapter],
          }),
        ),
      ]);
    },
  });
}

/** Searches a German word in the source revision (roadmap 0013); a mutation because it is a POST, nothing is written. */
export function useBulkReplaceSearch() {
  return useMutation({
    mutationFn: (body: BulkReplaceSearchRequest) => revisionApi.bulkReplaceSearch(body),
  });
}
