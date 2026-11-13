import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fixtureVerseSearch } from "@/test/fixtures/verseSearch";
import { isVerseSearchQuery, originalsQueryOf, useVerseSearch } from "./verseSearch";

const wrapper = ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    {children}
  </QueryClientProvider>
);

afterEach(() => vi.unstubAllGlobals());

describe("originalsQueryOf", () => {
  it("drops quotes, exclusions and the alternative marker", () => {
    expect(originalsQueryOf('"ging hin" -nicht oder Gott or Welt')).toBe("ging hin Gott Welt");
    expect(originalsQueryOf("  ")).toBe("");
  });
});

describe("isVerseSearchQuery", () => {
  it("accepts up to 8 words of up to 50 characters", () => {
    expect(isVerseSearchQuery("ich bin")).toBe(true);
    expect(isVerseSearchQuery("a b c d e f g h i")).toBe(false);
    expect(isVerseSearchQuery("x".repeat(51))).toBe(false);
    expect(isVerseSearchQuery("")).toBe(false);
  });
});

describe("useVerseSearch", () => {
  it("fetches page 1 with the book filter and pages on demand", async () => {
    const fetchMock = vi.fn(
      async (url: string) =>
        new Response(
          JSON.stringify({
            ...fixtureVerseSearch,
            total: 25,
            page: new URL(url, "http://x").searchParams.get("page") === "2" ? 2 : 1,
          }),
        ),
    );
    vi.stubGlobal("fetch", fetchMock);
    const { result } = renderHook(() => useVerseSearch(" ging ", 40), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(String(fetchMock.mock.calls[0]?.[0])).toBe(
      "/api/elberfelder/search/verses?q=ging&page=1&pageSize=20&bookId=40",
    );
    expect(result.current.hasNextPage).toBe(true);
    await result.current.fetchNextPage();
    await waitFor(() => expect(result.current.data?.pages).toHaveLength(2));
    expect(result.current.hasNextPage).toBe(false);
  });

  it("stays idle for an empty or invalid query", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    renderHook(() => useVerseSearch("", null), { wrapper });
    renderHook(() => useVerseSearch("a b c d e f g h i", null), { wrapper });
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
