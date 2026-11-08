import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fixtureBooks, fixtureChapterText, fixtureStructure } from "@/test/fixtures/bibleStructure";
import { useBibleStructure, useChapterVerses } from "./queries";

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

function stubFetch(body: unknown, status = 200) {
  const fetchMock = vi.fn(async () => new Response(JSON.stringify(body), { status }));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

afterEach(() => vi.unstubAllGlobals());

describe("bible queries", () => {
  it("loads the structure and selects the books", async () => {
    const fetchMock = stubFetch(fixtureStructure);
    const { result } = renderHook(() => useBibleStructure(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(fixtureBooks);
    expect(fetchMock.mock.calls[0]).toBeDefined();
    expect(String((fetchMock.mock.calls[0] as unknown[])[0])).toContain("/meta-bible/structure");
  });

  it("loads chapter verses from the chapterText url", async () => {
    const fetchMock = stubFetch(fixtureChapterText);
    const { result } = renderHook(() => useChapterVerses({ bookId: 43, chapter: 3 }), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(fixtureChapterText.verses);
    expect(String((fetchMock.mock.calls[0] as unknown[])[0])).toContain(
      "/elberfelder/chapterText/bookId/43/chapterId/3",
    );
  });

  it("does not fetch without a reference", () => {
    const fetchMock = stubFetch(fixtureChapterText);
    const { result } = renderHook(() => useChapterVerses(null), { wrapper });
    expect(result.current.fetchStatus).toBe("idle");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("surfaces API errors", async () => {
    stubFetch({}, 500);
    const { result } = renderHook(() => useBibleStructure(), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
