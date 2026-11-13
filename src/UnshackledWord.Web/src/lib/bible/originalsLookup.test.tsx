import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { isLookupQuery, useOriginalsLookup } from "./strongsStats";
import type { OriginalsLookupResponse } from "./types";

/** Contract fixture of `GET api/elberfelder/search/originals` (camelCase, nulls omitted). */
const contract: OriginalsLookupResponse = {
  statsRefreshedAt: "2026-11-12T10:30:00Z",
  tokens: [
    {
      token: "ging",
      lemmas: [
        {
          lemma: "gehen",
          word: "gehen",
          pos: "VERB",
          occurrences: 1500,
          lemmaOccurrences: 1500,
          highFrequency: false,
          originals: [
            {
              strongs: "H1980",
              language: "hebrew",
              lemma: "הָלַךְ",
              gloss: "to go",
              count: 900,
              share: 0.6,
              totalCount: 1500,
            },
          ],
        },
      ],
    },
    {
      token: "und",
      lemmas: [
        {
          lemma: "und",
          word: "und",
          occurrences: 30000,
          lemmaOccurrences: 30000,
          highFrequency: true,
          originals: [],
        },
      ],
    },
  ],
};

const wrapper = ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    {children}
  </QueryClientProvider>
);

afterEach(() => vi.unstubAllGlobals());

describe("useOriginalsLookup", () => {
  it("fetches the contract with top and includeFrequent", async () => {
    const fetchMock = vi.fn(async (_url: string) => new Response(JSON.stringify(contract)));
    vi.stubGlobal("fetch", fetchMock);
    const { result } = renderHook(
      () => useOriginalsLookup(" ging und ", { top: 3, includeFrequent: true }),
      { wrapper },
    );

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(String(fetchMock.mock.calls[0]?.[0])).toMatch(
      /\/elberfelder\/search\/originals\?q=ging\+und&top=3&includeFrequent=true$/,
    );
    expect(result.current.data?.tokens[0]?.lemmas[0]?.originals[0]?.strongs).toBe("H1980");
    expect(result.current.data?.tokens[1]?.lemmas[0]?.highFrequency).toBe(true);
  });

  it("stays idle for invalid queries and does not retry a 503", async () => {
    const fetchMock = vi.fn(async () => new Response("not ready", { status: 503 }));
    vi.stubGlobal("fetch", fetchMock);
    const idle = renderHook(() => useOriginalsLookup("   "), { wrapper });
    expect(idle.result.current.fetchStatus).toBe("idle");
    expect(isLookupQuery("a b c d e f g h i")).toBe(false);
    expect(isLookupQuery("x".repeat(201))).toBe(false);

    const { result } = renderHook(() => useOriginalsLookup("ging"), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
