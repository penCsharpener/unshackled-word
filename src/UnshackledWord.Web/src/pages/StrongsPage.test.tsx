import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { GetStrongsOccurrencesResponse } from "@/lib/bible/types";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { StrongsPage } from "./StrongsPage";

const page1: GetStrongsOccurrencesResponse = {
  strongs: "G0026",
  language: "greek",
  page: 1,
  pageSize: 50,
  totalItems: 116,
  totalPages: 3,
  items: [
    {
      bibleBookId: 40,
      chapter: 24,
      verse: 12,
      original: "ἀγάπη",
      before: [{ text: "Die", matched: false }],
      keyword: [
        { text: "Liebe", matched: true },
        { text: "der", matched: false },
        { text: "Vielen", matched: true },
      ],
      keywordSuffix: ",",
      after: [
        { text: "wird", matched: false },
        { text: "erkalten.", matched: false },
      ],
    },
    { bibleBookId: 66, chapter: 2, verse: 4, before: [], keyword: [], after: [] },
  ],
};

function stubFetch(occurrences: Response) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) =>
    String(input).includes("/meta-bible/structure")
      ? new Response(JSON.stringify(fixtureStructure))
      : occurrences.clone(),
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [{ path: "/bible/strongs/:strongs", element: <StrongsPage /> }],
    {
      initialEntries: [path],
    },
  );
  return render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("StrongsPage", () => {
  it("lists the verses with links to the chapter and paging", async () => {
    const fetchMock = stubFetch(new Response(JSON.stringify(page1)));
    renderAt("/bible/strongs/G26");

    const link = await screen.findByRole("link", { name: "Matthäus 24,12" });
    expect(link).toHaveAttribute("href", "/bible/40/24#v12");
    expect(screen.getByText("ἀγάπη")).toHaveAttribute("lang", "el");
    expect(screen.getByText("Die")).toHaveAttribute("lang", "de");
    expect(screen.getByText("wird erkalten.")).toBeInTheDocument();
    expect(screen.getByText("Liebe").tagName).toBe("MARK");
    expect(screen.getByText("Vielen").tagName).toBe("MARK");
    expect(screen.getByText("der").tagName).toBe("SPAN");
    expect(screen.getByText(",")).toHaveClass("kwic-punct");
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
    expect(screen.getByText("116 Vorkommen", { exact: false })).toBeInTheDocument();
    expect(screen.getByText("Seite 1 von 3")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Weiter ›" })).toHaveAttribute(
      "href",
      "/bible/strongs/G26?page=2",
    );
    expect(screen.queryByRole("link", { name: "‹ Zurück" })).not.toBeInTheDocument();
    expect(
      fetchMock.mock.calls.some(([url]) =>
        String(url).endsWith("/elberfelder/strongsOccurrences/strongs/G26?page=1&pageSize=50"),
      ),
    ).toBe(true);
  });

  it("keeps the German context left-to-right above a Hebrew original", async () => {
    stubFetch(
      new Response(
        JSON.stringify({
          ...page1,
          language: "hebrew",
          items: [{ ...page1.items[0], original: "בָּרָא" }],
        }),
      ),
    );
    renderAt("/bible/strongs/H1254");

    const original = await screen.findByText("בָּרָא");
    expect(original).toHaveAttribute("dir", "rtl");
    expect(screen.getByText("Die")).toHaveAttribute("lang", "de");
  });

  it("reads the page from the query string", async () => {
    const fetchMock = stubFetch(new Response(JSON.stringify({ ...page1, page: 2 })));
    renderAt("/bible/strongs/G26?page=2");

    await screen.findByText("Seite 2 von 3");
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes("page=2"))).toBe(true);
    expect(screen.getByRole("link", { name: "‹ Zurück" })).toBeInTheDocument();
  });

  it("shows an empty state", async () => {
    stubFetch(new Response(JSON.stringify({ ...page1, totalItems: 0, totalPages: 0, items: [] })));
    renderAt("/bible/strongs/G26");
    expect(await screen.findByText("Keine Stellen gefunden.")).toBeInTheDocument();
  });

  it("shows an error with retry", async () => {
    stubFetch(new Response("", { status: 500 }));
    renderAt("/bible/strongs/G26");
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Die Stellen konnten nicht geladen werden.",
    );
    expect(screen.getByRole("button", { name: "Erneut versuchen" })).toBeInTheDocument();
  });

  it("rejects an invalid Strong's number without calling the API", async () => {
    const fetchMock = stubFetch(new Response("{}"));
    renderAt("/bible/strongs/xyz");
    expect(await screen.findByText("Das ist keine gültige Strong-Nummer.")).toBeInTheDocument();
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([url]) => String(url).includes("strongsOccurrences"))).toBe(
        false,
      ),
    );
  });
});
