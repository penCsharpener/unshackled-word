import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { ThemeProvider } from "@/lib/theme";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import {
  fixtureOriginals,
  fixtureOriginalsFrequent,
  fixtureSourceWords,
  fixtureVerseSearch,
} from "@/test/fixtures/verseSearch";
import { AppShell } from "./AppShell";

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status });

function Where() {
  const location = useLocation();
  return <p data-testid="where">{`${location.pathname}${location.hash}`}</p>;
}

interface Stub {
  verses: (params: URLSearchParams) => Response;
  originals: (params: URLSearchParams) => Response;
  sourceWords: (params: URLSearchParams) => Response;
  parse: (params: URLSearchParams) => Response;
  grammar: (params: URLSearchParams) => Response;
}

function stubFetch(overrides: Partial<Stub> = {}, levels: unknown[] = []) {
  const handlers: Stub = {
    verses: () => json(fixtureVerseSearch),
    originals: () => json(fixtureOriginals),
    sourceWords: () => json(fixtureSourceWords),
    parse: () => json({ references: [], bookCandidates: [] }),
    grammar: () => json(fixtureGrammar),
    ...overrides,
  };
  const fetchMock = vi.fn(async (url: string) => {
    const parsed = new URL(url, "http://localhost");
    const path = parsed.pathname.replace(/^\/api/, "");
    if (path === "/meta-bible/structure") return json(fixtureStructure);
    if (path === "/revision/levels") return json(levels);
    if (path === "/elberfelder/search/verses") return handlers.verses(parsed.searchParams);
    if (path === "/elberfelder/search/grammar") return handlers.grammar(parsed.searchParams);
    if (path === "/references/parse") return handlers.parse(parsed.searchParams);
    if (path === "/elberfelder/search/originals") return handlers.originals(parsed.searchParams);
    if (path === "/elberfelder/search/sourcewords")
      return handlers.sourceWords(parsed.searchParams);
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const fixtureGrammar = {
  slots: [
    {
      text: "agape Kasus:nom",
      readAs: "lemma",
      lemmas: [{ strongs: "G26", lemma: "ἀγάπη", transliteration: "agapē", gloss: "love" }],
      filters: [{ key: "Kasus", values: ["Nominativ"], negated: false, german: false }],
    },
  ],
  totalWords: 36,
  totalVerses: 32,
  page: 1,
  pageSize: 50,
  books: [{ bibleBookId: 40, count: 1 }],
  hits: [
    {
      bibleBookId: 40,
      chapter: 24,
      verse: 12,
      words: [
        { text: "wird", position: 1 },
        { text: "die", position: 2 },
        { text: "Liebe", position: 3 },
        { text: "erkalten", position: 4 },
      ],
      matches: [
        {
          slot: 0,
          germanPositions: [3],
          original: {
            form: "ἀγάπη",
            lemma: "ἀγάπη",
            strongs: "G26",
            code: "N-NSF",
            keys: {
              partOfSpeech: "noun",
              case: "nominative",
              number: "singular",
              gender: "feminine",
            },
          },
        },
      ],
    },
  ],
};

const verseCalls = (fetchMock: ReturnType<typeof stubFetch>) =>
  fetchMock.mock.calls.filter(([url]) => String(url).includes("/search/verses"));

function setup(route = "/bible/40/5") {
  vi.stubGlobal("matchMedia", () => ({
    matches: false,
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
  const router = createMemoryRouter(
    [
      {
        element: <AppShell />,
        children: [
          { path: "bible", element: <Where /> },
          { path: "bible/:bookId/:chapter", element: <Where /> },
          { path: "login", element: <Where /> },
        ],
      },
    ],
    { initialEntries: [route] },
  );
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <ThemeProvider>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </ThemeProvider>
    </QueryClientProvider>,
  );
  return router;
}

const searchField = () => screen.getByRole("combobox", { name: "Suchbegriffe" });
const magnifier = () => screen.getByRole("button", { name: "Suche (F)" });

describe("SearchOverlay", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("opens with F and ignores F while typing in an input", async () => {
    stubFetch();
    const user = userEvent.setup();
    setup();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    await user.keyboard("f");
    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(searchField()).toHaveFocus();

    await user.keyboard("f");
    expect(searchField()).toHaveValue("f");
    expect(screen.getAllByRole("dialog")).toHaveLength(1);
  });

  it("does not open with F outside an overlay-capable route, with a modifier or in another field", async () => {
    stubFetch();
    const user = userEvent.setup();
    setup("/login");
    expect(screen.queryByRole("button", { name: "Suche (F)" })).not.toBeInTheDocument();
    await user.keyboard("f");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("ignores F with Ctrl and inside a mapping editor", async () => {
    stubFetch();
    const user = userEvent.setup();
    setup();
    await user.keyboard("{Control>}f{/Control}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    const editor = document.createElement("div");
    editor.setAttribute("data-mapping-editor", "");
    const inner = document.createElement("span");
    inner.tabIndex = 0;
    editor.append(inner);
    document.body.append(editor);
    inner.focus();
    await user.keyboard("f");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    editor.remove();
  });

  it("closes with Esc and returns focus to the magnifier", async () => {
    stubFetch();
    const user = userEvent.setup();
    setup();
    await user.keyboard("f");
    await screen.findByRole("dialog");
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(magnifier()).toHaveFocus();
  });

  it("opens from the magnifier", async () => {
    stubFetch();
    const user = userEvent.setup();
    setup();
    await user.click(magnifier());
    expect(await screen.findByRole("dialog", { name: "Suche in der Bibel" })).toBeInTheDocument();
  });

  it("searches at once after a space and after a 300 ms pause, not below 2 characters", async () => {
    vi.useFakeTimers();
    const fetchMock = stubFetch();
    setup();
    await act(() => vi.advanceTimersByTimeAsync(0));
    fireEvent.click(magnifier());
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    const settle = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

    fireEvent.change(searchField(), { target: { value: "g" } });
    await settle(1000);
    expect(verseCalls(fetchMock)).toHaveLength(0);

    fireEvent.change(searchField(), { target: { value: "ging" } });
    await settle(299);
    expect(verseCalls(fetchMock)).toHaveLength(0);
    await settle(2);
    expect(verseCalls(fetchMock)).toHaveLength(1);
    expect(String(verseCalls(fetchMock)[0]?.[0])).toContain("q=ging");

    fireEvent.change(searchField(), { target: { value: "ging hin " } });
    await settle(0);
    expect(verseCalls(fetchMock)).toHaveLength(2);
    expect(String(verseCalls(fetchMock)[1]?.[0])).toContain("q=ging+hin");

    fireEvent.change(searchField(), { target: { value: "ging hin xx" } });
    fireEvent.keyDown(searchField(), { key: "Enter" });
    await settle(0);
    expect(verseCalls(fetchMock)).toHaveLength(3);
  });

  it("lists the hits with the matched words marked and the widening line", async () => {
    stubFetch();
    const user = userEvent.setup();
    setup();
    await user.keyboard("f");
    await user.type(searchField(), "ging ");

    const list = await screen.findByRole("listbox", { name: "Treffer" });
    const options = within(list).getAllByRole("option");
    expect(options).toHaveLength(3);
    expect(within(options[0] as HTMLElement).getByText("Matthäus 5,1")).toBeInTheDocument();
    const mark = within(options[0] as HTMLElement).getByText("ging");
    expect(mark.tagName).toBe("MARK");
    expect(screen.getByTestId("widened")).toHaveTextContent("ging → auch Formen von gehen");
    expect(screen.getByRole("status")).toHaveTextContent("3 Verse gefunden");
  });

  it("searches at the reader level and marks revised hits with the left border", async () => {
    localStorage.setItem("uw.readerLevel", "300");
    const revised = {
      bibleBookId: 43,
      chapter: 3,
      verse: 16,
      tokens: [{ text: "einzigen", matched: true }],
      revised: true,
      level: 300,
    };
    const fetchMock = stubFetch(
      {
        verses: () =>
          json({ ...fixtureVerseSearch, total: 2, hits: [revised, fixtureVerseSearch.hits[0]] }),
      },
      [
        { level: 200, code: "elb1871", name: "Elberfelder 1871", isBase: true, isDefault: false },
        { level: 300, code: "modern", name: "Modern", isBase: false, isDefault: true },
      ],
    );
    const user = userEvent.setup();
    try {
      setup();
      await user.keyboard("f");
      await user.type(searchField(), "einzigen ");

      const list = await screen.findByRole("listbox", { name: "Treffer" });
      const options = within(list).getAllByRole("option");
      expect(String(verseCalls(fetchMock)[0]?.[0])).toContain("level=300");
      expect(options[0]).toHaveClass("border-l-4");
      expect(options[0]).toHaveAttribute("title", "revidiert: Modern");
      expect(options[1]).not.toHaveClass("border-l-4");
    } finally {
      localStorage.removeItem("uw.readerLevel");
    }
  });

  it("navigates to the verse and closes on a hit click", async () => {
    stubFetch();
    const user = userEvent.setup();
    const router = setup("/bible");
    await user.keyboard("f");
    await user.type(searchField(), "ging ");
    const list = await screen.findByRole("listbox", { name: "Treffer" });
    await user.click(within(list).getByRole("link", { name: /Matthäus 14,25/ }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe("/bible/40/14");
    expect(router.state.location.hash).toBe("#v25");
  });

  it("moves through the hits with the arrow keys and opens the active one with Enter", async () => {
    stubFetch();
    const user = userEvent.setup();
    const router = setup("/bible");
    await user.keyboard("f");
    await user.type(searchField(), "ging ");
    const list = await screen.findByRole("listbox", { name: "Treffer" });
    const options = within(list).getAllByRole("option");

    await user.keyboard("{ArrowDown}");
    expect(searchField()).toHaveAttribute("aria-activedescendant", options[0]?.id);
    expect(options[0]).toHaveAttribute("aria-selected", "true");
    await user.keyboard("{ArrowDown}{ArrowDown}{ArrowDown}");
    expect(searchField()).toHaveAttribute("aria-activedescendant", options[2]?.id);
    await user.keyboard("{ArrowUp}");
    expect(searchField()).toHaveAttribute("aria-activedescendant", options[1]?.id);
    expect(searchField()).toHaveFocus();

    await user.keyboard("{Enter}");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe("/bible/40/14");
    expect(router.state.location.hash).toBe("#v25");
  });

  it("filters by book with the bar chart and clears with the chip or a second click", async () => {
    const fetchMock = stubFetch({
      verses: (params) => {
        const bookId = Number(params.get("bookId") || 0);
        const hits = fixtureVerseSearch.hits.filter((h) => !bookId || h.bibleBookId === bookId);
        return json({ ...fixtureVerseSearch, total: hits.length, hits });
      },
    });
    const user = userEvent.setup();
    setup();
    await user.keyboard("f");
    await user.type(searchField(), "ging ");
    await screen.findByRole("listbox", { name: "Treffer" });
    expect(screen.getAllByRole("option")).toHaveLength(3);

    const matthew = () => screen.getAllByRole("button", { name: "Matthäus: 2" })[0] as HTMLElement;
    const chipName = "Buchfilter Matthäus entfernen";
    await user.click(matthew());
    await waitFor(() => expect(screen.getAllByRole("option")).toHaveLength(2));
    expect(String(verseCalls(fetchMock).at(-1)?.[0])).toContain("bookId=40");
    const chip = await screen.findByRole("button", { name: chipName });
    expect(chip).toHaveTextContent("Buch: Matthäus");

    await user.click(chip);
    await waitFor(() => expect(screen.queryByRole("button", { name: chipName })).toBeNull());
    await waitFor(() => expect(screen.getAllByRole("option")).toHaveLength(3));

    await user.click(matthew());
    await screen.findByRole("button", { name: chipName });
    await user.click(matthew());
    await waitFor(() => expect(screen.queryByRole("button", { name: chipName })).toBeNull());
  });

  it("loads more hits on request", async () => {
    const fetchMock = stubFetch({
      verses: (params) =>
        json({
          ...fixtureVerseSearch,
          total: 25,
          page: Number(params.get("page")),
          pageSize: 20,
          hits:
            params.get("page") === "1"
              ? fixtureVerseSearch.hits
              : [{ ...fixtureVerseSearch.hits[0], chapter: 28, verse: 9 }],
        }),
    });
    const user = userEvent.setup();
    setup();
    await user.keyboard("f");
    await user.type(searchField(), "ging ");
    await user.click(await screen.findByRole("button", { name: "Mehr laden" }));
    await waitFor(() => expect(screen.getAllByRole("option")).toHaveLength(4));
    expect(String(verseCalls(fetchMock).at(-1)?.[0])).toContain("page=2");
    expect(screen.queryByRole("button", { name: "Mehr laden" })).toBeNull();
  });

  it("shows the empty state and the server's validation message", async () => {
    stubFetch({
      verses: (params) =>
        params.get("q") === "nichts"
          ? json({ ...fixtureVerseSearch, total: 0, bookCounts: [], hits: [] })
          : json(
              {
                statusCode: 400,
                message: "bad",
                errors: { q: ["Die Suche enthält keine Wörter."] },
              },
              400,
            ),
    });
    const user = userEvent.setup();
    setup();
    await user.keyboard("f");
    await user.type(searchField(), "nichts ");
    expect(await screen.findByText(/Keine Verse zu „nichts“ gefunden/)).toBeInTheDocument();

    await user.clear(searchField());
    await user.type(searchField(), '"" ');
    expect(await screen.findByText("Die Suche enthält keine Wörter.")).toBeInTheDocument();
  });

  it("keeps the typed text when closed and reopened", async () => {
    stubFetch();
    const user = userEvent.setup();
    setup();
    await user.keyboard("f");
    await user.type(searchField(), "ging ");
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    await user.keyboard("f");
    expect(await screen.findByRole("combobox", { name: "Suchbegriffe" })).toHaveValue("ging ");
  });

  it("stores query and marking, and the clear button empties the field", async () => {
    stubFetch();
    const user = userEvent.setup();
    setup();
    await user.keyboard("f");
    expect(screen.queryByRole("button", { name: "Suche leeren" })).not.toBeInTheDocument();
    await user.type(searchField(), "agape Kasus:nom");
    expect(localStorage.getItem("uw.search.query")).toBe("agape Kasus:nom");
    const markSwitch = screen.getByRole("switch", { name: "Treffer im Text markieren" });
    expect(markSwitch).toHaveAttribute("aria-checked", "false");
    await user.click(markSwitch);
    expect(markSwitch).toHaveAttribute("aria-checked", "true");
    expect(localStorage.getItem("uw.search.mark")).toBe("true");

    await user.click(screen.getByRole("button", { name: "Suche leeren" }));
    expect(searchField()).toHaveValue("");
    expect(searchField()).toHaveFocus();
    expect(localStorage.getItem("uw.search.query")).toBeNull();
  });

  it("restores the stored query after a reload", async () => {
    localStorage.setItem("uw.search.query", "ging");
    stubFetch();
    const user = userEvent.setup();
    setup();
    await user.keyboard("f");
    expect(await screen.findByRole("combobox", { name: "Suchbegriffe" })).toHaveValue("ging");
  });
});

describe("SearchOverlay originals", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });
  afterEach(() => vi.unstubAllGlobals());

  async function openWith(text: string) {
    const user = userEvent.setup();
    const router = setup("/bible");
    await user.keyboard("f");
    await user.type(searchField(), text);
    return { user, router };
  }

  it("shows a donut per word with links to the Strong's page, collapses frequent words and hides unknown ones", async () => {
    const fetchMock = stubFetch({
      originals: (params) =>
        params.get("includeFrequent") === "true"
          ? json(fixtureOriginalsFrequent)
          : json(fixtureOriginals),
    });
    const { user, router } = await openWith("ging und xyz ");

    const donut = await screen.findByRole("figure", { name: "ging → gehen, Verb" });
    expect(within(donut).getByRole("link", { name: /H1980/ })).toHaveAttribute(
      "href",
      "/bible/strongs/H1980",
    );

    expect(
      await screen.findByText("Sehr häufiges Wort – Grundtext ausgeblendet"),
    ).toBeInTheDocument();
    expect(screen.queryByText("xyz")).toBeNull();

    await user.click(screen.getByRole("button", { name: "Trotzdem anzeigen" }));
    const frequent = await screen.findByRole("figure", { name: /und/ });
    expect(within(frequent).getByRole("link", { name: /G2532/ })).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes("includeFrequent=true"))).toBe(
      true,
    );

    await user.click(within(donut).getByRole("link", { name: /H1980/ }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe("/bible/strongs/H1980");
  });

  it("says the statistics are missing on a 503", async () => {
    stubFetch({ originals: () => new Response("", { status: 503 }) });
    await openWith("ging ");
    expect(await screen.findByText("Statistik noch nicht berechnet")).toBeInTheDocument();
  });

  it("looks up the words without search operators", async () => {
    const fetchMock = stubFetch();
    await openWith('"ging hin" -nicht oder Gott ');
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([url]) => String(url).includes("/search/originals"))).toBe(
        true,
      ),
    );
    const url = String(
      fetchMock.mock.calls.filter(([u]) => String(u).includes("/search/originals")).at(-1)?.[0],
    );
    expect(new URL(url, "http://x").searchParams.get("q")).toBe("ging hin Gott");
  });

  describe("source words", () => {
    const sourceCalls = (fetchMock: ReturnType<typeof stubFetch>) =>
      fetchMock.mock.calls.filter(([url]) => String(url).includes("/search/sourcewords"));
    const originalsCalls = (fetchMock: ReturnType<typeof stubFetch>) =>
      fetchMock.mock.calls.filter(([url]) => String(url).includes("/search/originals"));

    it("looks up Greek script as source words, shows the lemma card with its forms and does not ask the German lookup", async () => {
      const fetchMock = stubFetch();
      const { user, router } = await openWith("λογου ");

      const card = await screen.findByRole("article", { name: /G3056/ });
      expect(within(card).getByRole("link", { name: "G3056" })).toHaveAttribute(
        "href",
        "/bible/strongs/G3056",
      );
      const lemma = within(card).getByText("λόγος");
      expect(lemma).toHaveAttribute("lang", "el");
      expect(lemma).toHaveAttribute("dir", "ltr");
      expect(within(card).getByText("word")).toBeInTheDocument();
      expect(within(card).getByText("330 Vorkommen")).toBeInTheDocument();
      expect(within(card).getByText("λόγου")).toHaveAttribute("lang", "el");
      expect(within(card).getByRole("link", { name: "Mt 5,1" })).toHaveAttribute(
        "href",
        "/bible/40/5#v1",
      );
      const url = String(sourceCalls(fetchMock).at(-1)?.[0]);
      expect(new URL(url, "http://x").searchParams.get("q")).toBe("λογου");
      expect(originalsCalls(fetchMock)).toHaveLength(0);

      await user.click(within(card).getByRole("link", { name: "G3056" }));
      await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
      expect(router.state.location.pathname).toBe("/bible/strongs/G3056");
    });

    it("renders Hebrew lemmas right to left", async () => {
      stubFetch({
        sourceWords: () =>
          json({
            tokens: [
              {
                token: "מלך",
                lemmas: [
                  {
                    strongs: "H4428",
                    language: "hebrew",
                    lemma: "מֶלֶךְ",
                    transliteration: "melek",
                    gloss: "king",
                    occurrences: 2523,
                    matchedBy: "script",
                    forms: [],
                  },
                ],
              },
            ],
          }),
      });
      await openWith("מלך ");

      const lemma = await screen.findByText("מֶלֶךְ");
      expect(lemma).toHaveAttribute("lang", "he");
      expect(lemma).toHaveAttribute("dir", "rtl");
    });

    it("splits a mixed query: script and Strong's numbers go to the source lookup, the German words to the originals lookup", async () => {
      const fetchMock = stubFetch();
      await openWith("ging θεός G3056 h430 ");

      await screen.findByRole("article", { name: /G3056/ });
      await screen.findByRole("figure", { name: "ging → gehen, Verb" });
      expect(
        new URL(String(sourceCalls(fetchMock).at(-1)?.[0]), "http://x").searchParams.get("q"),
      ).toBe("θεός G3056 h430");
      expect(
        new URL(String(originalsCalls(fetchMock).at(-1)?.[0]), "http://x").searchParams.get("q"),
      ).toBe("ging");
      expect(
        screen.getByRole("heading", { name: "Griechische und hebräische Wörter" }),
      ).toBeInTheDocument();
    });

    it("keeps German words on the originals lookup only", async () => {
      const fetchMock = stubFetch();
      await openWith("ging ");
      await screen.findByRole("figure", { name: "ging → gehen, Verb" });
      expect(sourceCalls(fetchMock)).toHaveLength(0);
    });

    it("says when nothing was found and when the statistics are missing", async () => {
      stubFetch({ sourceWords: () => json({ tokens: [{ token: "ωωω", lemmas: [] }] }) });
      await openWith("ωωω ");
      expect(
        await screen.findByText("Zu diesen Grundtextwörtern wurde nichts gefunden."),
      ).toBeInTheDocument();
    });

    it("shows the missing-statistics note on a 503", async () => {
      stubFetch({ sourceWords: () => new Response("", { status: 503 }) });
      await openWith("G3056 ");
      expect(await screen.findByText("Statistik noch nicht berechnet")).toBeInTheDocument();
    });
  });

  it("switches between the verse and the original text view with the tab buttons", async () => {
    stubFetch();
    const { user } = await openWith("ging ");
    const originals = screen.getByRole("button", { name: "Grundtext" });
    const verses = screen.getByRole("button", { name: "Verse" });
    expect(verses).toHaveAttribute("aria-pressed", "true");
    await user.click(originals);
    expect(originals).toHaveAttribute("aria-pressed", "true");
    expect(verses).toHaveAttribute("aria-pressed", "false");
  });

  it("has no unlabeled controls", async () => {
    stubFetch();
    await openWith("ging ");
    await screen.findByRole("listbox", { name: "Treffer" });
    const dialog = screen.getByRole("dialog");
    for (const control of within(dialog).getAllByRole("button")) {
      expect(control).toHaveAccessibleName();
    }
    for (const control of within(dialog).getAllByRole("combobox")) {
      expect(control).toHaveAccessibleName();
    }
  });
  describe("scripture references", () => {
    const reference = (display: string, path: string, groups = [{ display, path }]) => ({
      bookId: 66,
      bookOnly: false,
      display,
      spans: [],
      path,
      groups,
    });

    it("shows the reference as a link, skips the full-text search and jumps with Enter", async () => {
      const path = "/bible/66/2?v=1-5,7,9-10";
      const fetchMock = stubFetch({
        parse: () =>
          json({ references: [reference("Off 2,1–5.7.9–10", path)], bookCandidates: [] }),
      });
      const user = userEvent.setup();
      const router = setup("/bible");
      await user.keyboard("f");
      fireEvent.change(searchField(), { target: { value: "Offb 2,1-5.7.9-10 " } });
      expect(await screen.findByRole("link", { name: "Off 2,1–5.7.9–10" })).toHaveAttribute(
        "href",
        path,
      );
      expect(verseCalls(fetchMock)).toHaveLength(0);

      await user.keyboard("{Enter}");
      await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
      expect(router.state.location.pathname).toBe("/bible/66/2");
      expect(router.state.location.search).toBe("?v=1-5,7,9-10");
    });

    it("jumps with Enter although the answer is not there yet", async () => {
      stubFetch({
        parse: () => json({ references: [reference("Off 2", "/bible/66/2")], bookCandidates: [] }),
      });
      const user = userEvent.setup();
      const router = setup("/bible");
      await user.keyboard("f");
      fireEvent.change(searchField(), { target: { value: "Offb 2" } });
      fireEvent.keyDown(searchField(), { key: "Enter" });
      await waitFor(() => expect(router.state.location.pathname).toBe("/bible/66/2"));
    });

    it("gives every chapter group its own link", async () => {
      stubFetch({
        parse: () =>
          json({
            references: [
              reference("Röm 8,28; 12,1–2", "/bible/45/8?v=28", [
                { display: "Röm 8,28", path: "/bible/45/8?v=28" },
                { display: "12,1–2", path: "/bible/45/12?v=1-2" },
              ]),
            ],
            bookCandidates: [],
          }),
      });
      const user = userEvent.setup();
      setup("/bible");
      await user.keyboard("f");
      fireEvent.change(searchField(), { target: { value: "Röm 8,28; 12,1-2 " } });
      const first = await screen.findByRole("link", { name: "Röm 8,28" });
      expect(first).toHaveAttribute("href", "/bible/45/8?v=28");
      expect(screen.getByRole("link", { name: "12,1–2" })).toHaveAttribute(
        "href",
        "/bible/45/12?v=1-2",
      );
    });

    it("offers the candidates of an ambiguous book and puts the chosen one into the input", async () => {
      const fetchMock = stubFetch({
        parse: () =>
          json({
            references: [],
            error: { code: "ambiguousBook", position: 0, args: { text: "Ma" } },
            bookCandidates: [39, 40],
          }),
      });
      const user = userEvent.setup();
      setup("/bible");
      await user.keyboard("f");
      fireEvent.change(searchField(), { target: { value: "Ma 1 " } });
      await user.click(await screen.findByRole("button", { name: "Matthäus" }));
      expect(searchField()).toHaveValue("Matthäus 1 ");
      expect(screen.getByRole("button", { name: "Maleachi" })).toBeInTheDocument();
      expect(verseCalls(fetchMock)).toHaveLength(0);
    });

    it("shows the error hint of a wrong reference and skips the full-text search", async () => {
      const fetchMock = stubFetch({
        parse: () =>
          json({
            references: [],
            error: { code: "chapterOutOfRange", position: 5, args: { value: "23", max: "22" } },
            bookCandidates: [],
          }),
      });
      const user = userEvent.setup();
      setup("/bible");
      await user.keyboard("f");
      fireEvent.change(searchField(), { target: { value: "Offb 23 " } });
      expect(
        await screen.findByText("Kapitel 23 gibt es nicht (höchstens 22)."),
      ).toBeInTheDocument();
      expect(verseCalls(fetchMock)).toHaveLength(0);
    });

    it("keeps the full-text hits for a whole book and adds the book link", async () => {
      const fetchMock = stubFetch({
        parse: () =>
          json({
            references: [{ bookId: 19, bookOnly: true, display: "Psalmen", spans: [], groups: [] }],
            bookCandidates: [],
          }),
      });
      const user = userEvent.setup();
      setup("/bible");
      await user.keyboard("f");
      fireEvent.change(searchField(), { target: { value: "Psalmen " } });
      expect(await screen.findByRole("link", { name: "Zu Psalmen 1" })).toHaveAttribute(
        "href",
        "/bible/19/1",
      );
      expect(await screen.findByRole("listbox", { name: "Treffer" })).toBeInTheDocument();
      expect(verseCalls(fetchMock).length).toBeGreaterThan(0);
    });

    it("shows no reference card for plain words", async () => {
      stubFetch();
      const user = userEvent.setup();
      setup("/bible");
      await user.keyboard("f");
      fireEvent.change(searchField(), { target: { value: "ging " } });
      expect(await screen.findByRole("listbox", { name: "Treffer" })).toBeInTheDocument();
      expect(screen.queryByRole("list", { name: "Bibelstellen" })).not.toBeInTheDocument();
    });
  });
  describe("grammar search", () => {
    beforeEach(async () => {
      await i18n.changeLanguage("de");
    });
    afterEach(() => vi.unstubAllGlobals());

    async function openGrammar(text: string) {
      const user = userEvent.setup();
      const router = setup("/bible");
      await user.keyboard("f");
      fireEvent.change(searchField(), { target: { value: text } });
      return { user, router };
    }

    it("shows the slot chip, the summary and the hits with the original line instead of the lemma cards", async () => {
      const fetchMock = stubFetch();
      const { router } = await openGrammar("agape Kasus:nom ");

      const chips = await screen.findByRole("list", { name: "So wurde die Suche gelesen" });
      expect(chips).toHaveTextContent("ἀγάπη G26 · Nominativ");
      expect(within(chips).getByRole("link", { name: /G26/ })).toHaveAttribute(
        "href",
        "/bible/strongs/G26?grammar=Kasus%3ANominativ",
      );
      expect(await screen.findByText("36 Vorkommen in 32 Versen")).toBeInTheDocument();
      const list = await screen.findByRole("listbox", { name: "Treffer" });
      expect(within(list).getByText("Liebe").tagName).toBe("MARK");
      expect(within(list).getByTestId("grammar-match")).toHaveTextContent(
        "ἀγάπη · Substantiv, Nominativ Singular feminin",
      );
      expect(
        screen.getByText("Grammatiksuche – die Treffer stehen im Grundtext."),
      ).toBeInTheDocument();
      expect(verseCalls(fetchMock)).toHaveLength(0);
      expect(
        fetchMock.mock.calls.some(([url]) => String(url).includes("/search/sourcewords")),
      ).toBe(false);

      fireEvent.keyDown(searchField(), { key: "ArrowDown" });
      fireEvent.keyDown(searchField(), { key: "Enter" });
      await waitFor(() => expect(router.state.location.pathname).toBe("/bible/40/24"));
      expect(router.state.location.hash).toBe("#v12");
    });

    it("marks the ambiguous token and replaces it with a candidate", async () => {
      stubFetch({
        grammar: () =>
          json(
            {
              status: 400,
              code: "ambiguousValue",
              token: "Tempus:p",
              candidates: ["Präsens", "Perfekt", "Plusquamperfekt"],
            },
            400,
          ),
      });
      const { user } = await openGrammar("Tempus:p ");

      const alert = await screen.findByText("Tempus:p ist mehrdeutig.");
      expect(alert).toBeInTheDocument();
      expect(screen.getByText("Tempus:p", { selector: "li" })).toHaveAttribute(
        "data-problem",
        "true",
      );
      await user.click(screen.getByRole("button", { name: "Tempus:p durch Perfekt ersetzen" }));
      expect(searchField()).toHaveValue("Tempus:Perfekt");
    });
  });
});
