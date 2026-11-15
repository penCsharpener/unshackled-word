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
}

function stubFetch(overrides: Partial<Stub> = {}, levels: unknown[] = []) {
  const handlers: Stub = {
    verses: () => json(fixtureVerseSearch),
    originals: () => json(fixtureOriginals),
    ...overrides,
  };
  const fetchMock = vi.fn(async (url: string) => {
    const parsed = new URL(url, "http://localhost");
    const path = parsed.pathname.replace(/^\/api/, "");
    if (path === "/meta-bible/structure") return json(fixtureStructure);
    if (path === "/revision/levels") return json(levels);
    if (path === "/elberfelder/search/verses") return handlers.verses(parsed.searchParams);
    if (path === "/elberfelder/search/originals") return handlers.originals(parsed.searchParams);
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

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
});
