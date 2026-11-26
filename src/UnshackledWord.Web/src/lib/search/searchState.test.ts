import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { isGrammarQuery } from "./grammarSearch";
import {
  markedGrammarQuery,
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

describe("markedGrammarQuery", () => {
  const marked = (query: string, tab: "verses" | "originals", mark = true) =>
    markedGrammarQuery({ query, tab, mark }, isGrammarQuery);

  it("marks a grammar query from either tab, normalized", () => {
    expect(marked("  agape   Kasus:nom ", "verses")).toBe("agape Kasus:nom");
    expect(marked("agape Kasus:nom", "originals")).toBe("agape Kasus:nom");
  });

  it("marks a word-only query typed in the Grundtext tab", () => {
    expect(marked("agape", "originals")).toBe("agape");
  });

  it("marks nothing for a Verse-tab word query, a too short query or with marking off", () => {
    expect(marked("Gott Welt", "verses")).toBe("");
    expect(marked("a", "originals")).toBe("");
    expect(marked("agape Kasus:nom", "originals", false)).toBe("");
  });
});
