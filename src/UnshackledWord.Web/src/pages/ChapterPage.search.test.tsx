import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { BibleStructureBook, GetWordsOfChapterResponse } from "@/lib/bible/types";
import {
  resetSearchState,
  SEARCH_MARK_KEY,
  SEARCH_QUERY_KEY,
  SEARCH_TAB_KEY,
  useSearchState,
} from "@/lib/search/searchState";
import { fixtureBooks, fixtureChapterWords } from "@/test/fixtures/bibleStructure";
import { ChapterPage } from "./ChapterPage";

const john: BibleStructureBook = {
  id: 43,
  testament: "nt",
  nameDe: "Johannes",
  nameEn: "John",
  abbreviationDe: "Joh",
  abbreviationEn: "John",
  versesPerChapter: Array<number>(21).fill(20),
};
const books = [...fixtureBooks, john];

const levels = [
  { level: 200, code: "elb1871", name: "Elb 1871", isBase: true, isDefault: false },
  { level: 300, code: "revision", name: "Revision", isBase: false, isDefault: true },
];

/** Verse 2 revised: "Jener" is variant-only, "zu" keeps its base position 3. */
const revisedResponse: GetWordsOfChapterResponse = {
  ...fixtureChapterWords,
  words: [
    ...fixtureChapterWords.words.filter((w) => w.verse !== 2),
    { id: 0, verse: 2, positionInVerse: 1, wordInContext: "Jener", revisionWordId: 501 },
    {
      id: 7,
      verse: 2,
      positionInVerse: 2,
      wordInContext: "zu",
      revisionWordId: 503,
      basePositionInVerse: 3,
    },
  ],
  revisedVerses: [{ verse: 2, level: 300, revisionVerseId: 9 }],
};

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status });

interface Stub {
  chapter: (url: URL) => Response;
  neighbours: (url: URL) => Response;
}

function stubFetch(overrides: Partial<Stub> = {}) {
  const handlers: Stub = {
    chapter: () =>
      json({
        verses: [
          { verse: 1, germanPositions: [4], originalWordIds: [] },
          { verse: 2, germanPositions: [1, 3], originalWordIds: [] },
        ],
        total: 2,
      }),
    neighbours: () => json({ next: { bibleBookId: 43, chapter: 17 } }),
    ...overrides,
  };
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = new URL(String(input), "http://localhost");
    const path = url.pathname.replace(/^\/api/, "");
    if (path === "/meta-bible/structure") return json({ books });
    if (path.startsWith("/auth/me")) return new Response("", { status: 401 });
    if (path === "/revision/levels") return json(levels);
    if (path.includes("chapterWords")) {
      return json(url.search.includes("level=300") ? revisedResponse : fixtureChapterWords);
    }
    if (path.startsWith("/elberfelder/search/grammar/chapter/")) return handlers.chapter(url);
    if (path === "/elberfelder/search/grammar/neighbours") return handlers.neighbours(url);
    return new Response("{}", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const grammarCalls = (fetchMock: ReturnType<typeof stubFetch>) =>
  fetchMock.mock.calls
    .map(([url]) => String(url))
    .filter((url) => url.includes("/search/grammar/"));

function OpenProbe() {
  return <p data-testid="overlay-open">{String(useSearchState().open)}</p>;
}

function renderPage(entry = "/bible/43/3") {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      {
        path: "/bible/:bookId/:chapter",
        element: (
          <>
            <ChapterPage />
            <OpenProbe />
          </>
        ),
      },
    ],
    { initialEntries: [entry] },
  );
  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
  return router;
}

function storeSearch(query: string, mark: boolean, tab: "verses" | "originals" = "originals") {
  localStorage.setItem(SEARCH_QUERY_KEY, query);
  localStorage.setItem(SEARCH_TAB_KEY, tab);
  if (mark) localStorage.setItem(SEARCH_MARK_KEY, "true");
  resetSearchState();
}

const wordButton = (text: string) =>
  screen.getByRole("button", { name: `Grundtext zu „${text}“ anzeigen` });

beforeEach(async () => {
  localStorage.clear();
  localStorage.setItem("uw.readerLevel", "200");
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  await i18n.changeLanguage("de");
});
afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("ChapterPage search marks", () => {
  it("marks the German words of the chapter matches and shows the chip", async () => {
    storeSearch("agape Kasus:nom", true, "verses");
    const fetchMock = stubFetch();
    renderPage();
    const bar = await screen.findByTestId("search-mark-bar");
    expect(await within(bar).findByText("2 Treffer")).toBeInTheDocument();
    expect(within(bar).getByText("agape Kasus:nom")).toBeInTheDocument();
    await waitFor(() => expect(wordButton("Mensch").className).toContain("bg-mark-search"));
    expect(wordButton("Es").className).not.toContain("bg-mark-search");
    expect(wordButton("Dieser").className).toContain("bg-mark-search");
    const chapterCall = grammarCalls(fetchMock).find((url) => url.includes("/chapter/43/3?"));
    expect(new URL(chapterCall as string, "http://localhost").searchParams.get("q")).toBe(
      "agape Kasus:nom",
    );
  });

  it("goes to the next chapter with a hit and fetches its matches", async () => {
    storeSearch("agape Kasus:nom", true);
    const fetchMock = stubFetch();
    const router = renderPage();
    const bar = await screen.findByTestId("search-mark-bar");
    const previous = within(bar).getByRole("button", {
      name: "Kein vorheriges Kapitel mit Treffer",
    });
    expect(previous).toBeDisabled();
    const next = await within(bar).findByRole("button", {
      name: "Nächstes Kapitel mit Treffer: Johannes 17",
    });
    await userEvent.click(next);
    expect(router.state.location.pathname).toBe("/bible/43/17");
    await waitFor(() =>
      expect(grammarCalls(fetchMock).some((url) => url.includes("/chapter/43/17?"))).toBe(true),
    );
  });

  it("says so in a chapter without hits and keeps the arrows", async () => {
    storeSearch("agape", true);
    stubFetch({ chapter: () => json({ verses: [], total: 0 }) });
    renderPage();
    const bar = await screen.findByTestId("search-mark-bar");
    expect(await within(bar).findByText("Keine Treffer in diesem Kapitel")).toBeInTheDocument();
    expect(
      await within(bar).findByRole("button", { name: "Nächstes Kapitel mit Treffer: Johannes 17" }),
    ).toBeEnabled();
  });

  it("shows an incomplete query on a 400 and opens the overlay on click", async () => {
    storeSearch("agape Kasus:", true);
    stubFetch({
      chapter: () => json({ status: 400, code: "emptyValue", token: "Kasus:" }, 400),
      neighbours: () => json({ status: 400, code: "emptyValue" }, 400),
    });
    renderPage();
    const bar = await screen.findByTestId("search-mark-bar");
    const open = await within(bar).findByRole("button", { name: "Suche unvollständig" });
    expect(screen.getByTestId("overlay-open")).toHaveTextContent("false");
    await userEvent.click(open);
    expect(screen.getByTestId("overlay-open")).toHaveTextContent("true");
    expect(wordButton("Mensch").className).not.toContain("bg-mark-search");
  });

  it("turns marking off with × and keeps the query", async () => {
    storeSearch("agape Kasus:nom", true);
    stubFetch();
    renderPage();
    const bar = await screen.findByTestId("search-mark-bar");
    await waitFor(() => expect(wordButton("Mensch").className).toContain("bg-mark-search"));
    await userEvent.click(within(bar).getByRole("button", { name: "Markierung ausschalten" }));
    expect(screen.queryByTestId("search-mark-bar")).not.toBeInTheDocument();
    expect(wordButton("Mensch").className).not.toContain("bg-mark-search");
    expect(localStorage.getItem(SEARCH_MARK_KEY)).toBeNull();
    expect(localStorage.getItem(SEARCH_QUERY_KEY)).toBe("agape Kasus:nom");
  });

  it("fetches nothing while marking is off or for a Verse-tab query", async () => {
    storeSearch("agape Kasus:nom", false);
    const fetchMock = stubFetch();
    renderPage();
    await screen.findByText("Mensch");
    expect(screen.queryByTestId("search-mark-bar")).not.toBeInTheDocument();
    expect(grammarCalls(fetchMock)).toHaveLength(0);
  });

  it("does not mark a plain Verse-tab query", async () => {
    storeSearch("Gott Welt", true, "verses");
    const fetchMock = stubFetch();
    renderPage();
    await screen.findByText("Mensch");
    expect(screen.queryByTestId("search-mark-bar")).not.toBeInTheDocument();
    expect(grammarCalls(fetchMock)).toHaveLength(0);
  });

  it("marks revised words through their base position only", async () => {
    localStorage.setItem("uw.readerLevel", "300");
    storeSearch("agape Kasus:nom", true);
    stubFetch();
    renderPage();
    await screen.findByText("Jener");
    await waitFor(() => expect(wordButton("zu").className).toContain("bg-mark-search"));
    expect(wordButton("Jener").className).not.toContain("bg-mark-search");
  });
});
