import { queryOptions, useQuery } from "@tanstack/react-query";
import { useCallback, useState } from "react";
import { bibleApi } from "./api";
import type { ChapterRef } from "./types";

export type SidePanelMode = "tsk" | "original" | "off";

export const SIDE_PANEL_KEY = "uw.sidePanel";
const MODES: readonly SidePanelMode[] = ["tsk", "original", "off"];

/** Next state of the `G` shortcut: TSK → Grundtext → off → TSK. */
export function nextSidePanelMode(mode: SidePanelMode): SidePanelMode {
  return MODES[(MODES.indexOf(mode) + 1) % MODES.length] ?? "tsk";
}

/** Stored mode; anything unknown (or no storage) falls back to TSK. */
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

export const sidePanelKeys = {
  crossReferences: (bookId: number, chapter: number) =>
    ["bible", "chapterCrossReferences", bookId, chapter] as const,
  originals: (bookId: number, chapter: number) =>
    ["bible", "chapterOriginals", bookId, chapter] as const,
  /** Prefix of every cached chapter of the originals, for invalidation after a mapping save. */
  originalsAll: ["bible", "chapterOriginals"] as const,
};

/** TSK is static data: fetched once per chapter, one request for all verses. */
export function useChapterCrossReferences(ref: ChapterRef | null, enabled: boolean) {
  return useQuery({
    ...queryOptions({
      queryKey: sidePanelKeys.crossReferences(ref?.bookId ?? 0, ref?.chapter ?? 0),
      queryFn: ({ signal }) =>
        bibleApi.chapterCrossReferences(ref?.bookId ?? 0, ref?.chapter ?? 0, signal),
      staleTime: Number.POSITIVE_INFINITY,
    }),
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
