import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { revisionKeys, useSaveRevision, useWithdrawRevision } from "./queries";

const ref = { bookId: 43, chapter: 3, verse: 16 };

function setup() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return { client, wrapper };
}

afterEach(() => vi.unstubAllGlobals());

describe("revision mutations and the stable verse id", () => {
  it("reloads cached mappings after a text save (the verse id stays, the word ids change)", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(
        async () =>
          new Response(JSON.stringify({ revisionVerseId: 10, level: 200, version: 2, carry: {} })),
      ),
    );
    const { client, wrapper } = setup();
    client.setQueryData(revisionKeys.mapping(10), {
      revisionVerseId: 10,
      germanWords: [{ id: 1 }],
    });
    const invalidate = vi.spyOn(client, "invalidateQueries");

    const { result } = renderHook(() => useSaveRevision(ref), { wrapper });
    result.current.mutate({ level: 200, text: "Neu", expectedVersion: 1, basedOnLevel: 200 });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: revisionKeys.mappings });
    expect(client.getQueryState(revisionKeys.mapping(10))?.isInvalidated).toBe(true);
  });

  it("drops cached mappings after a withdraw (the row and its words are deleted)", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(null, { status: 204 })),
    );
    const { client, wrapper } = setup();
    client.setQueryData(revisionKeys.mapping(10), { revisionVerseId: 10 });

    const { result } = renderHook(() => useWithdrawRevision(ref), { wrapper });
    result.current.mutate(10);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(client.getQueryData(revisionKeys.mapping(10))).toBeUndefined();
  });
});
