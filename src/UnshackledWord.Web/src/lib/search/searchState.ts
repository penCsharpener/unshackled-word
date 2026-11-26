import { useSyncExternalStore } from "react";
import { SEARCH_MIN_CHARS } from "./useSubmittedQuery";

/** Tab of the search overlay below `lg` (from `lg` both columns are shown). */
export type SearchTab = "verses" | "originals";

export interface SearchState {
  /** The text in the search field, as typed. */
  query: string;
  tab: SearchTab;
  /** "Treffer im Text markieren" (roadmap 0029.2f). */
  mark: boolean;
  /** The overlay is open. Not stored: a reload starts with it closed. */
  open: boolean;
}

export const SEARCH_QUERY_KEY = "uw.search.query";
export const SEARCH_TAB_KEY = "uw.search.tab";
export const SEARCH_MARK_KEY = "uw.search.mark";

function readItem(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeItem(key: string, value: string) {
  try {
    if (value === "") localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch {
    // storage unavailable: the value only lasts for this page view
  }
}

/** The stored query, tab and marking choice; defaults when nothing (or no storage) is there. */
export function readSearchState(): SearchState {
  return {
    query: readItem(SEARCH_QUERY_KEY) ?? "",
    tab: readItem(SEARCH_TAB_KEY) === "originals" ? "originals" : "verses",
    mark: readItem(SEARCH_MARK_KEY) === "true",
    open: false,
  };
}

let state: SearchState | undefined;
const listeners = new Set<() => void>();

const current = () => {
  state ??= readSearchState();
  return state;
};

function update(patch: Partial<SearchState>) {
  const next = { ...current(), ...patch };
  if (patch.query !== undefined) writeItem(SEARCH_QUERY_KEY, patch.query);
  if (patch.tab !== undefined) writeItem(SEARCH_TAB_KEY, patch.tab);
  if (patch.mark !== undefined) writeItem(SEARCH_MARK_KEY, patch.mark ? "true" : "");
  state = next;
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export const setSearchQuery = (query: string) => update({ query });
export const setSearchTab = (tab: SearchTab) => update({ tab });
export const setSearchMark = (mark: boolean) => update({ mark });
export const setSearchOpen = (open: boolean) => update({ open });

/** Drops the in-memory state so the next read comes from storage again (tests, between renders of the app). */
export function resetSearchState() {
  state = undefined;
  for (const listener of listeners) listener();
}

/**
 * The search shared by the overlay and the reader: query and tab of the overlay, the marking switch and whether the
 * overlay is open. Query, tab and marking are kept in localStorage until the field is cleared.
 */
export function useSearchState(): SearchState {
  return useSyncExternalStore(subscribe, current, current);
}

const normalize = (query: string) => query.trim().replace(/\s+/g, " ");

/**
 * The query the reader marks, or "" when nothing is marked: marking on and a query in the Grundtext engine. The engine
 * is the grammar search when the query is a grammar query or was typed in the Grundtext tab (a word-only query such
 * as `agape` marks too); a Verse-tab query marks nothing yet (roadmap 0029.10f).
 */
export function markedGrammarQuery(
  search: Pick<SearchState, "query" | "tab" | "mark">,
  isGrammar: (query: string) => boolean,
): string {
  if (!search.mark) return "";
  const query = normalize(search.query);
  if (query.length < SEARCH_MIN_CHARS) return "";
  return search.tab === "originals" || isGrammar(query) ? query : "";
}
