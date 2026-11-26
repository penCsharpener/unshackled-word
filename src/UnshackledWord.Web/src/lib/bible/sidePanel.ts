import { queryOptions, useQuery } from "@tanstack/react-query";
import { useCallback, useState } from "react";
import { BASE_LEVEL, bibleApi } from "./api";
import type { ChapterRef, VerseSimilarVerses } from "./types";

export type SidePanelMode = "tsk" | "original" | "lxx" | "similar" | "off";

export const SIDE_PANEL_KEY = "uw.sidePanel";
export const SIDE_PANEL_MODES: readonly SidePanelMode[] = [
  "tsk",
  "original",
  "lxx",
  "similar",
  "off",
];
const MODES = SIDE_PANEL_MODES;

/** Modes offered in a chapter: without the LXX (roadmap 0028.4f) in New Testament chapters. */
export function sidePanelModes(lxxAvailable: boolean): readonly SidePanelMode[] {
  return lxxAvailable ? MODES : MODES.filter((mode) => mode !== "lxx");
}

/**
 * Next state of the `G` shortcut: TSK → Grundtext → Septuaginta → Ähnliche Verse → off → TSK (notes have their own
 * overlay, `N`); the LXX step is skipped where it is not available.
 */
export function nextSidePanelMode(mode: SidePanelMode, lxxAvailable = true): SidePanelMode {
  const modes = sidePanelModes(lxxAvailable);
  return modes[(modes.indexOf(mode) + 1) % modes.length] ?? "tsk";
}

/**
 * The mode a chapter shows: a stored LXX choice shows the Grundtext in New Testament chapters (the choice itself stays,
 * so the next Old Testament chapter shows the LXX again).
 */
export function effectiveSidePanelMode(mode: SidePanelMode, lxxAvailable: boolean): SidePanelMode {
  return mode === "lxx" && !lxxAvailable ? "original" : mode;
}

/** Stored mode; anything unknown (or no storage, or the former `notes` mode) falls back to TSK. */
export function readSidePanelMode(): SidePanelMode {
  try {
    const raw = localStorage.getItem(SIDE_PANEL_KEY);
    return MODES.find((mode) => mode === raw) ?? "tsk";
  } catch {
    return "tsk";
  }
}

/** The reader's side panel choice, global for all chapters and remembered in this browser. */
export function useSidePanelMode() {
  const [mode, setMode] = useState<SidePanelMode>(readSidePanelMode);
  const update = useCallback((next: SidePanelMode) => {
    try {
      localStorage.setItem(SIDE_PANEL_KEY, next);
    } catch {
      // storage unavailable: the choice only lasts for this page view
    }
    setMode(next);
  }, []);
  return [mode, update] as const;
}

/** "Nur hervorgehobene" in the TSK panel header (roadmap 0024.3f), remembered per browser. */
export const TSK_ONLY_HIGHLIGHTED_KEY = "uw.tsk.onlyHighlighted";

/** Stored "only highlighted" choice; off when nothing (or no storage) is there. */
export function readTskOnlyHighlighted(): boolean {
  try {
    return localStorage.getItem(TSK_ONLY_HIGHLIGHTED_KEY) === "true";
  } catch {
    return false;
  }
}

/** The reader's "only highlighted cross references" choice, global for all chapters. */
export function useTskOnlyHighlighted() {
  const [value, setValue] = useState(readTskOnlyHighlighted);
  const update = useCallback((next: boolean) => {
    try {
      localStorage.setItem(TSK_ONLY_HIGHLIGHTED_KEY, String(next));
    } catch {
      // storage unavailable: the choice only lasts for this page view
    }
    setValue(next);
  }, []);
  return [value, update] as const;
}

export const sidePanelKeys = {
  /** `includeHidden` (curators' curate mode) is a separate cache entry. */
  crossReferences: (bookId: number, chapter: number, includeHidden = false, level = BASE_LEVEL) =>
    ["bible", "chapterCrossReferences", bookId, chapter, includeHidden, level] as const,
  /** Prefix of every cached chapter of cross references, for invalidation after a curation save. */
  crossReferencesAll: ["bible", "chapterCrossReferences"] as const,
  originals: (bookId: number, chapter: number) =>
    ["bible", "chapterOriginals", bookId, chapter] as const,
  /** Prefix of every cached chapter of the originals, for invalidation after a mapping save. */
  originalsAll: ["bible", "chapterOriginals"] as const,
  /** Similar verses of a chapter (roadmap 0027.3f), `limit` hits per verse. */
  similar: (bookId: number, chapter: number, limit = SIMILAR_CHAPTER_LIMIT) =>
    ["bible", "chapterSimilarVerses", bookId, chapter, limit] as const,
  /** Septuagint text of a chapter (roadmap 0028.4f), both Daniel forms in one entry. */
  lxx: (bookId: number, chapter: number) => ["bible", "chapterLxx", bookId, chapter] as const,
  /** All (up to 30) similar verses of one verse, for "Mehr anzeigen". */
  similarOfVerse: (hebRefId: number) => ["bible", "similarVersesOfVerse", hebRefId] as const,
};

/** Hits per verse in the chapter request; the verse request ("mehr") returns up to 30. */
export const SIMILAR_CHAPTER_LIMIT = 10;

/**
 * TSK of a chapter: one request for all verses, changed only by a curation save (which invalidates it). With
 * `includeHidden` (curate mode, Admin/Maintainer) hidden links come back too; switching keeps the other list of the
 * same chapter on screen until the new one arrives.
 */
export function useChapterCrossReferences(
  ref: ChapterRef | null,
  enabled: boolean,
  includeHidden = false,
  level = BASE_LEVEL,
) {
  const bookId = ref?.bookId ?? 0;
  const chapter = ref?.chapter ?? 0;
  return useQuery({
    ...queryOptions({
      queryKey: sidePanelKeys.crossReferences(bookId, chapter, includeHidden, level),
      queryFn: ({ signal }) =>
        bibleApi.chapterCrossReferences(bookId, chapter, signal, includeHidden, level),
      staleTime: Number.POSITIVE_INFINITY,
    }),
    placeholderData: (previous, previousQuery) =>
      previousQuery?.queryKey[2] === bookId && previousQuery.queryKey[3] === chapter
        ? previous
        : undefined,
    enabled: enabled && ref !== null,
  });
}

/** Original words of a chapter. Links change when a mapping is saved, see `useSaveVerseMapping`. */
export function useChapterOriginals(ref: ChapterRef | null, enabled: boolean) {
  return useQuery({
    ...queryOptions({
      queryKey: sidePanelKeys.originals(ref?.bookId ?? 0, ref?.chapter ?? 0),
      queryFn: ({ signal }) =>
        bibleApi.chapterOriginals(ref?.bookId ?? 0, ref?.chapter ?? 0, signal),
      staleTime: Number.POSITIVE_INFINITY,
    }),
    enabled: enabled && ref !== null,
  });
}

/**
 * Similar verses of a chapter (roadmap 0027.3f): one request for all verses, fetched only while the mode is shown.
 * Changes only with a rebuild, so it is never refetched on its own.
 */
export function useChapterSimilarVerses(ref: ChapterRef | null, enabled: boolean) {
  return useQuery({
    ...queryOptions({
      queryKey: sidePanelKeys.similar(ref?.bookId ?? 0, ref?.chapter ?? 0),
      queryFn: ({ signal }) =>
        bibleApi.similarVerses(ref?.bookId ?? 0, ref?.chapter ?? 0, SIMILAR_CHAPTER_LIMIT, signal),
      staleTime: Number.POSITIVE_INFINITY,
    }),
    enabled: enabled && ref !== null,
  });
}

/**
 * Septuagint text of an Old Testament chapter (roadmap 0028.4f): one request for all verses, fetched only while the
 * mode is shown. The text changes only with a reimport, so it is never refetched on its own.
 */
export function useLxxChapter(ref: ChapterRef | null, enabled: boolean) {
  return useQuery({
    ...queryOptions({
      queryKey: sidePanelKeys.lxx(ref?.bookId ?? 0, ref?.chapter ?? 0),
      queryFn: ({ signal }) => bibleApi.lxxChapter(ref?.bookId ?? 0, ref?.chapter ?? 0, signal),
      staleTime: Number.POSITIVE_INFINITY,
    }),
    enabled: enabled && ref !== null,
  });
}

/** Up to 30 similar verses of one verse; the hits repeat the first ten, so the result replaces the list. */
export const similarVersesOfVerseQuery = (hebRefId: number) =>
  queryOptions<VerseSimilarVerses>({
    queryKey: sidePanelKeys.similarOfVerse(hebRefId),
    queryFn: ({ signal }) => bibleApi.similarVersesOfVerse(hebRefId, signal),
    staleTime: Number.POSITIVE_INFINITY,
  });
