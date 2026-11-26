import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { isGrammarQuery } from "./grammarSearch";
import {
  markedSearch,
  readSearchState,
  resetSearchState,
  SEARCH_MARK_KEY,
  SEARCH_QUERY_KEY,
  SEARCH_TAB_KEY,
  setSearchMark,
  setSearchOpen,
  setSearchQuery,
  setSearchTab,
  useSearchState,
} from "./searchState";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("search state persistence", () => {
  it("defaults to an empty Verse-tab search without marking", () => {
    expect(readSearchState()).toEqual({ query: "", tab: "verses", mark: false, open: false });
  });

  it("stores query, tab and marking and reads them back after a reload", () => {
    setSearchQuery("agape Kasus:nom");
    setSearchTab("originals");
    setSearchMark(true);
    setSearchOpen(true);
    expect(localStorage.getItem(SEARCH_QUERY_KEY)).toBe("agape Kasus:nom");
    expect(localStorage.getItem(SEARCH_TAB_KEY)).toBe("originals");
    expect(localStorage.getItem(SEARCH_MARK_KEY)).toBe("true");
    resetSearchState();
    const { result } = renderHook(() => useSearchState());
    // the overlay starts closed after a reload
    expect(result.current).toEqual({
      query: "agape Kasus:nom",
      tab: "originals",
      mark: true,
      open: false,
    });
  });

  it("removes the stored query when the field is cleared and marking when it is off", () => {
    setSearchQuery("agape");
    setSearchMark(true);
    setSearchQuery("");
    setSearchMark(false);
    expect(localStorage.getItem(SEARCH_QUERY_KEY)).toBeNull();
    expect(localStorage.getItem(SEARCH_MARK_KEY)).toBeNull();
  });

  it("ignores an unknown stored tab", () => {
    localStorage.setItem(SEARCH_TAB_KEY, "notes");
    expect(readSearchState().tab).toBe("verses");
  });

  it("works for this page view when the storage throws", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("denied");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("denied");
    });
    vi.spyOn(Storage.prototype, "removeItem").mockImplementation(() => {
      throw new Error("denied");
    });
    resetSearchState();
    const { result } = renderHook(() => useSearchState());
    expect(result.current.query).toBe("");
    act(() => {
      setSearchQuery("agape");
      setSearchMark(true);
    });
    expect(result.current.query).toBe("agape");
    expect(result.current.mark).toBe(true);
  });

  it("notifies every subscriber", () => {
    const first = renderHook(() => useSearchState());
    const second = renderHook(() => useSearchState());
    act(() => setSearchOpen(true));
    expect(first.result.current.open).toBe(true);
    expect(second.result.current.open).toBe(true);
  });
});

describe("markedSearch", () => {
  const marked = (query: string, tab: "verses" | "originals", bothColumns = false, mark = true) =>
    markedSearch({ query, tab, mark }, isGrammarQuery, bothColumns);

  it("uses the grammar search for a grammar query from either tab, normalized", () => {
    expect(marked("  agape   Kasus:nom ", "verses")).toEqual({
      engine: "grammar",
      query: "agape Kasus:nom",
    });
    expect(marked("agape Kasus:nom", "originals")?.engine).toBe("grammar");
    expect(marked("agape Kasus:nom", "verses", true)?.engine).toBe("grammar");
  });

  it("uses the grammar search for a word-only query typed in the Grundtext tab below lg", () => {
    expect(marked("agape", "originals")).toEqual({ engine: "grammar", query: "agape" });
  });

  it("uses the verse search for a word query typed in the Verse tab", () => {
    expect(marked("Gott Welt geliebt", "verses")).toEqual({
      engine: "verses",
      query: "Gott Welt geliebt",
    });
  });

  it("uses the verse search for any word query when both columns are shown", () => {
    expect(marked("agape", "originals", true)?.engine).toBe("verses");
    expect(marked("Gott Welt", "originals", true)?.engine).toBe("verses");
  });

  it("marks nothing for a too short query or with marking off", () => {
    expect(marked("a", "originals")).toBeNull();
    expect(marked("Gott", "verses", false, false)).toBeNull();
  });
});
