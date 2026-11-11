import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { GetStrongsOccurrencesResponse } from "@/lib/bible/types";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { fixtureSummary, fixtureWordStats } from "@/test/fixtures/strongsStats";
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

function stubFetch(occurrences: Response, summary?: Response) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes("/meta-bible/structure"))
      return new Response(JSON.stringify(fixtureStructure));
    if (url.includes("/strongsSummary/")) {
      return summary?.clone() ?? new Response(JSON.stringify(fixtureSummary));
    }
    if (url.includes("/wordStats")) return new Response(JSON.stringify(fixtureWordStats));
    return occurrences.clone();
  });
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
    const list = screen.getByRole("list", { name: "Vorkommen" });
    expect(link).toHaveAttribute("href", "/bible/40/24#v12");
    expect(within(list).getByText("ἀγάπη")).toHaveAttribute("lang", "el");
    expect(within(list).getByText("Die")).toHaveAttribute("lang", "de");
    expect(within(list).getByText("wird erkalten.")).toBeInTheDocument();
    expect(within(list).getByText("Liebe").tagName).toBe("MARK");
    expect(within(list).getByText("Vielen").tagName).toBe("MARK");
    expect(within(list).getByText("der").tagName).toBe("SPAN");
    expect(within(list).getByText(",")).toHaveClass("kwic-punct");
    expect(within(list).getAllByRole("listitem")).toHaveLength(2);
    expect(screen.getAllByText("116 Vorkommen").length).toBeGreaterThan(0);
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

  it("shows the stats header above the concordance", async () => {
    stubFetch(new Response(JSON.stringify(page1)));
    renderAt("/bible/strongs/G26");

    await screen.findByText("agapē", { exact: false });
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
      "Strong G26 – ἀγάπη – agapē",
    );
    expect(screen.getByRole("heading", { level: 2, name: "Alle Stellen" })).toBeInTheDocument();
    expect(
      await screen.findByRole("heading", { name: "Vorkommen im Neuen Testament" }),
    ).toBeVisible();
    expect(screen.getByRole("heading", { name: "Gebildet aus" })).toBeInTheDocument();
  });

  it("filters the concordance by book and rendering and clears the filters", async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch(new Response(JSON.stringify(page1)));
    renderAt("/bible/strongs/G26?page=2");

    const columns = await screen.findByRole("list", { name: "Neues Testament: Vorkommen je Buch" });
    await user.click(within(columns).getByRole("button", { name: "Matthäus: 80" }));
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([url]) =>
          String(url).endsWith("/strongsOccurrences/strongs/G26?page=1&pageSize=50&book=40"),
        ),
      ).toBe(true),
    );
    const bookChip = await screen.findByRole("button", { name: "Filter entfernen: Matthäus" });

    await user.click(await screen.findByRole("button", { name: "Liebe" }));
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([url]) => String(url).includes("book=40&rendering=liebe")),
      ).toBe(true),
    );
    expect(screen.getByRole("button", { name: "Alle Filter entfernen" })).toBeInTheDocument();

    await user.click(bookChip);
    await user.click(screen.getByRole("button", { name: "Filter entfernen: liebe" }));
    await waitFor(() =>
      expect(screen.queryByRole("group", { name: "Aktive Filter" })).not.toBeInTheDocument(),
    );
  });

  it("applies filters from the query string", async () => {
    const fetchMock = stubFetch(new Response(JSON.stringify(page1)));
    renderAt("/bible/strongs/G26?book=40&rendering=Liebe&page=2");

    expect(await screen.findByRole("button", { name: "Filter entfernen: Matthäus" })).toBeVisible();
    expect(
      fetchMock.mock.calls.some(([url]) =>
        String(url).endsWith(
          "/strongsOccurrences/strongs/G26?page=2&pageSize=50&book=40&rendering=liebe",
        ),
      ),
    ).toBe(true);
    expect(screen.getByRole("link", { name: "Weiter ›" })).toHaveAttribute(
      "href",
      expect.stringContaining("book=40"),
    );
  });

  it("keeps the concordance when the stats fail and offers a retry", async () => {
    stubFetch(new Response(JSON.stringify(page1)), new Response("", { status: 500 }));
    renderAt("/bible/strongs/G26");

    expect(
      await screen.findByText("Die Statistik konnte nicht geladen werden."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Erneut versuchen" })).toBeInTheDocument();
    expect(await screen.findByRole("link", { name: "Matthäus 24,12" })).toBeInTheDocument();
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
