import { queryOptions, useQuery } from "@tanstack/react-query";
import { useCallback, useState } from "react";
import { bibleApi } from "./api";
import type { ChapterRef } from "./types";

export type SidePanelMode = "tsk" | "original" | "off";

export const SIDE_PANEL_KEY = "uw.sidePanel";
export const SIDE_PANEL_MODES: readonly SidePanelMode[] = ["tsk", "original", "off"];
const MODES = SIDE_PANEL_MODES;

/** Next state of the `G` shortcut: TSK → Grundtext → off → TSK (notes have their own overlay, `N`). */
export function nextSidePanelMode(mode: SidePanelMode): SidePanelMode {
  return MODES[(MODES.indexOf(mode) + 1) % MODES.length] ?? "tsk";
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
  crossReferences: (bookId: number, chapter: number, includeHidden = false) =>
    ["bible", "chapterCrossReferences", bookId, chapter, includeHidden] as const,
  /** Prefix of every cached chapter of cross references, for invalidation after a curation save. */
  crossReferencesAll: ["bible", "chapterCrossReferences"] as const,
  originals: (bookId: number, chapter: number) =>
    ["bible", "chapterOriginals", bookId, chapter] as const,
  /** Prefix of every cached chapter of the originals, for invalidation after a mapping save. */
  originalsAll: ["bible", "chapterOriginals"] as const,
};

/**
 * TSK of a chapter: one request for all verses, changed only by a curation save (which invalidates it). With
 * `includeHidden` (curate mode, Admin/Maintainer) hidden links come back too; switching keeps the other list of the
 * same chapter on screen until the new one arrives.
 */
export function useChapterCrossReferences(
  ref: ChapterRef | null,
  enabled: boolean,
  includeHidden = false,
) {
  const bookId = ref?.bookId ?? 0;
  const chapter = ref?.chapter ?? 0;
  return useQuery({
    ...queryOptions({
      queryKey: sidePanelKeys.crossReferences(bookId, chapter, includeHidden),
      queryFn: ({ signal }) =>
        bibleApi.chapterCrossReferences(bookId, chapter, signal, includeHidden),
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
