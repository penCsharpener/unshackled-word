import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { LemmaBulkEdit, type LemmaBulkEditProps } from "./LemmaBulkEdit";

const TOTAL = 120;
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status });

function occurrence(id: number) {
  return {
    wordId: id,
    bookId: 40,
    chapter: 1,
    verse: id,
    positionInVerse: 3,
    lemma: "Jesu",
    wordInContext: "Jesu",
    plainWord: id <= 100 ? "Jesu" : "Jesu,",
    partOfSpeech: "PROPN",
    left: "Das Buch des",
    right: "Christi, des Sohnes",
  };
}

function occurrencesPage(page: number, size: number) {
  const first = (page - 1) * size + 1;
  const last = Math.min(TOTAL, page * size);
  return {
    from: ["Jesu"],
    page,
    pageSize: size,
    total: TOTAL,
    totalPages: Math.ceil(TOTAL / size),
    byBook: [{ bookId: 40, count: TOTAL }],
    byLemma: [{ lemma: "Jesu", count: TOTAL }],
    groups: [
      { plainWord: "Jesu", partOfSpeech: "PROPN", count: 100 },
      { plainWord: "Jesu,", partOfSpeech: "PROPN", count: 20 },
    ],
    occurrences: Array.from({ length: Math.max(0, last - first + 1) }, (_, i) =>
      occurrence(first + i),
    ),
  };
}

type Handler = (url: URL, init?: RequestInit) => Response;

function mockApi(overrides: Record<string, Handler> = {}) {
  const calls: { method: string; url: URL; body?: unknown }[] = [];
  const routes: Record<string, Handler> = {
    "GET /lemmas/bulk/occurrences": (url) =>
      json(
        occurrencesPage(
          Number(url.searchParams.get("page")),
          Number(url.searchParams.get("pageSize")),
        ),
      ),
    "GET /lemmas/bulk/word-ids": () =>
      json({
        total: TOTAL,
        truncated: false,
        wordIds: Array.from({ length: TOTAL }, (_, i) => i + 1),
      }),
    "GET /lemmas/lemma-counts": (url) =>
      json({
        items: url.searchParams.get("q")?.toLowerCase().startsWith("jes")
          ? [
              { lemma: "Jesus", count: 580 },
              { lemma: "Jesu", count: 120 },
            ]
          : [],
      }),
    "GET /lemmas/verse": () =>
      json({
        bookId: 40,
        chapter: 1,
        verse: 1,
        words: [
          { wordId: 900, wordInContext: "Das", verse: 1, positionInVerse: 1, hasMorphology: true },
          { wordId: 1, wordInContext: "Jesu", verse: 1, positionInVerse: 2, hasMorphology: true },
        ],
      }),
    "POST /lemmas/bulk/apply": () => json({ changeSetId: 17, updated: 1 }),
    "POST /lemmas/bulk/17/revert": () => json({ changeSetId: 18, restored: 1, skipped: 0 }),
    ...overrides,
  };
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: string, init?: RequestInit) => {
      const url = new URL(input, "http://localhost/api".replace(/\/api$/, ""));
      const path = url.pathname.replace(/^\/api/, "");
      const method = init?.method ?? "GET";
      calls.push({ method, url, body: init?.body ? JSON.parse(init.body as string) : undefined });
      return routes[`${method} ${path}`]?.(url, init) ?? new Response("", { status: 404 });
    }),
  );
  return calls;
}

function Probe() {
  const location = useLocation();
  return <output data-testid="search">{location.search}</output>;
}

function renderBulk(entry: string, props: Partial<LemmaBulkEditProps> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const all: LemmaBulkEditProps = {
    initialFrom: "Jesu",
    bookId: 66,
    chapter: 1,
    books: fixtureBooks,
    partOfSpeechOptions: [{ value: "PROPN", label: "eigenname" }],
    dirtyCount: 0,
    onSaveGrid: vi.fn(async () => true),
    onApplied: vi.fn(),
    ...props,
  };
  const router = createMemoryRouter(
    [
      {
        path: "/",
        element: (
          <>
            <LemmaBulkEdit {...all} />
            <Probe />
          </>
        ),
      },
    ],
    { initialEntries: [entry] },
  );
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return all;
}

const REVIEW = "/?dlg=review&from=Jesu&to=Jesus&scope=all&page=1&size=50";
const searchOf = () => screen.getByTestId("search").textContent ?? "";

describe("LemmaBulkEdit", () => {
  beforeAll(() => {
    const size = { configurable: true, get: () => 800 };
    Object.defineProperty(HTMLElement.prototype, "clientHeight", size);
    Object.defineProperty(HTMLElement.prototype, "clientWidth", size);
    // measured row height (jsdom has no layout)
    Object.defineProperty(HTMLElement.prototype, "offsetHeight", {
      configurable: true,
      get: () => 40,
    });
  });
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });
  afterEach(() => vi.unstubAllGlobals());

  it("renders nothing while the URL has no dialog", () => {
    mockApi();
    renderBulk("/");
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("asks to save the grid first and continues after saving", async () => {
    mockApi();
    const props = renderBulk("/?dlg=search", { dirtyCount: 3 });
    expect(await screen.findByText("Erst 3 Änderungen speichern")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Speichern und weiter" }));
    expect(props.onSaveGrid).toHaveBeenCalled();
  });

  it("prefills the source lemma and searches into the review URL", async () => {
    mockApi();
    renderBulk("/?dlg=search");
    expect(await screen.findByText("Jesu")).toBeInTheDocument();
    const search = screen.getByRole("button", { name: "Suchen" });
    expect(search).toBeDisabled();
    await userEvent.type(screen.getByRole("textbox", { name: "Durch dieses Lemma" }), "Jesus");
    await waitFor(() => expect(search).toBeEnabled());
    await userEvent.click(search);
    await waitFor(() => expect(searchOf()).toContain("dlg=review"));
    expect(searchOf()).toContain("from=Jesu");
    expect(searchOf()).toContain("to=Jesus");
    expect(searchOf()).toContain("page=1");
    expect(await screen.findByRole("listbox")).toBeInTheDocument();
  });

  it("restores page and page size from the URL and keeps the selection across pages", async () => {
    mockApi();
    renderBulk("/?dlg=review&from=Jesu&to=Jesus&scope=all&page=2&size=50");
    const list = await screen.findByRole("listbox");
    expect(screen.getByText("Seite 2 von 3")).toBeInTheDocument();
    expect(screen.getByText("Zeilen 51–100 von 120")).toBeInTheDocument();
    expect(screen.getByText("120 von 120 ausgewählt")).toBeInTheDocument();

    const first = within(list)
      .getAllByRole("option")
      .find((o) => o.id && o.getAttribute("aria-posinset"));
    await userEvent.click(first as HTMLElement);
    expect(screen.getByText("119 von 120 ausgewählt")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Nächste Seite" }));
    await waitFor(() => expect(searchOf()).toContain("page=3"));
    expect(await screen.findByText("Seite 3 von 3")).toBeInTheDocument();
    expect(screen.getByText("119 von 120 ausgewählt")).toBeInTheDocument();
  });

  it("derives group checkboxes from the server counts", async () => {
    mockApi();
    renderBulk(REVIEW);
    const group = await screen.findByRole("checkbox", { name: /Gruppe Jesu, PROPN, 100 Treffer/ });
    expect(group).toBeChecked();
    await userEvent.click(group);
    expect(screen.getByText("20 von 120 ausgewählt")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Alle abwählen" }));
    expect(screen.getByText("0 von 120 ausgewählt")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Auswahl anwenden \(0\)/ })).toBeDisabled();
  });

  it("changes the page size and keeps the first visible row", async () => {
    mockApi();
    renderBulk("/?dlg=review&from=Jesu&to=Jesus&scope=all&page=3&size=50");
    await screen.findByRole("listbox");
    await userEvent.selectOptions(screen.getByLabelText(/Zeilen pro Seite/), "100");
    await waitFor(() => expect(searchOf()).toContain("size=100"));
    expect(searchOf()).toContain("page=2");
  });

  it("loads the full verse only on demand", async () => {
    const calls = mockApi();
    renderBulk(REVIEW);
    await screen.findByRole("listbox");
    expect(calls.some((c) => c.url.pathname.endsWith("/verse"))).toBe(false);
    await userEvent.click(
      screen.getAllByRole("button", { name: "Vers anzeigen" })[0] as HTMLElement,
    );
    expect(await screen.findByText("Das")).toBeInTheDocument();
    expect(calls.filter((c) => c.url.pathname.endsWith("/verse"))).toHaveLength(1);
    expect(screen.getAllByRole("button", { name: "Vers ausblenden" })[0]).toHaveAttribute(
      "aria-expanded",
      "true",
    );
  });

  it("applies explicit word ids, shows the result and undoes the change set", async () => {
    const calls = mockApi();
    const props = renderBulk(REVIEW);
    await screen.findByRole("listbox");
    await userEvent.click(screen.getByRole("button", { name: "Alle abwählen" }));
    await userEvent.click(
      screen
        .getAllByRole("option")
        .find((o) => o.getAttribute("aria-posinset") === "2") as HTMLElement,
    );
    await userEvent.click(screen.getByRole("button", { name: /Auswahl anwenden \(1\)/ }));

    expect(await screen.findByText("1 Vorkommen aktualisiert")).toBeInTheDocument();
    const apply = calls.find((c) => c.method === "POST" && c.url.pathname.endsWith("/apply"));
    expect(apply?.body).toEqual({
      from: ["Jesu"],
      to: "Jesus",
      wordIds: [2],
      expectedOriginal: [""],
    });
    expect(props.onApplied).toHaveBeenCalledTimes(1);
    expect(searchOf()).toContain("dlg=done");

    await userEvent.click(screen.getByRole("button", { name: "Rückgängig machen" }));
    expect(await screen.findByText("1 Vorkommen zurückgesetzt")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Bereits rückgängig gemacht" })).toBeDisabled();
    expect(props.onApplied).toHaveBeenCalledTimes(2);
  });

  it("fetches all ids when the whole search is selected and drops deselected rows", async () => {
    const calls = mockApi();
    renderBulk(REVIEW);
    await screen.findByRole("listbox");
    await userEvent.click(
      screen
        .getAllByRole("option")
        .find((o) => o.getAttribute("aria-posinset") === "1") as HTMLElement,
    );
    await userEvent.click(screen.getByRole("button", { name: /Auswahl anwenden \(119\)/ }));
    await screen.findByText("1 Vorkommen aktualisiert");
    const apply = calls.find((c) => c.method === "POST" && c.url.pathname.endsWith("/apply"));
    const ids = ((apply?.body ?? {}) as { wordIds?: number[] }).wordIds ?? [];
    expect(ids).toHaveLength(119);
    expect(ids).not.toContain(1);
  });

  it("marks stale rows on a 409, deselects them and keeps the rest", async () => {
    mockApi({ "POST /lemmas/bulk/apply": () => json({ stale: [3] }, 409) });
    renderBulk(REVIEW);
    await screen.findByRole("listbox");
    await userEvent.click(screen.getByRole("button", { name: /Auswahl anwenden \(120\)/ }));
    expect(await screen.findByText(/1 Zeile hat sich inzwischen geändert/)).toBeInTheDocument();
    expect(screen.getByText("119 von 120 ausgewählt")).toBeInTheDocument();
    expect(screen.getByText("schon geändert")).toBeInTheDocument();
  });

  it("shows server errors with a message and keeps the dialog open", async () => {
    mockApi({ "POST /lemmas/bulk/apply": () => json({}, 403) });
    renderBulk(REVIEW);
    await screen.findByRole("listbox");
    await userEvent.click(screen.getByRole("button", { name: /Auswahl anwenden/ }));
    expect(await screen.findByRole("alert")).toHaveTextContent("keine Berechtigung");
    expect(screen.getByRole("listbox")).toBeInTheDocument();
  });

  it("shows loading errors and the empty state", async () => {
    mockApi({ "GET /lemmas/bulk/occurrences": () => new Response("", { status: 500 }) });
    renderBulk(REVIEW);
    expect(
      await screen.findByText("Die Treffer konnten nicht geladen werden."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Erneut versuchen" })).toBeInTheDocument();
  });

  it("shows an empty result", async () => {
    mockApi({
      "GET /lemmas/bulk/occurrences": () =>
        json({
          from: ["Jesu"],
          page: 1,
          pageSize: 50,
          total: 0,
          totalPages: 0,
          byBook: [],
          byLemma: [],
          groups: [],
          occurrences: [],
        }),
    });
    renderBulk(REVIEW);
    expect(await screen.findByText("Keine Treffer")).toBeInTheDocument();
  });

  describe("original-language guard", () => {
    const OWN: Record<string, number[]> = { H2450: [1, 2, 3, 4], G4680: [5, 6], G5158: [7, 8] };
    const weiseRow = (id: number) => {
      const key = Object.keys(OWN).find((k) => OWN[k]?.includes(id)) as string;
      return {
        ...occurrence(id),
        lemma: "Weise",
        wordInContext: "Weise",
        plainWord: "Weise",
        partOfSpeech: "NOUN",
        origKind: "aligned",
        origKey: key,
        origStrongs: `${key}G`,
        origWord: key === "G5158" ? "τρόπος" : "חָכָם",
        left: "der",
        right: "des Landes",
      };
    };
    const weiseResponse = (ambiguous: boolean) => ({
      from: ["Weise"],
      page: 1,
      pageSize: 50,
      total: 8,
      totalPages: 1,
      byBook: [],
      byLemma: [{ lemma: "Weise", count: 8 }],
      groups: [
        { origKey: "H2450", plainWord: "Weise", partOfSpeech: "NOUN", count: 4 },
        { origKey: "G4680", plainWord: "Weise", partOfSpeech: "NOUN", count: 2 },
        { origKey: "G5158", plainWord: "Weise", partOfSpeech: "NOUN", count: 2 },
      ],
      byOriginal: [
        { key: "H2450", word: "חָכָם", gloss: "weise", pos: "A", count: 4 },
        { key: "G4680", word: "σοφός", gloss: "weise", pos: "A", count: 2 },
        { key: "G5158", word: "τρόπος", gloss: "Art", pos: "N", count: 2, minority: true },
      ],
      ambiguous,
      occurrences: [1, 2, 3, 4, 5, 6, 7, 8].map(weiseRow),
    });
    const weiseUrl = "/?dlg=review&from=Weise&to=Weiser&scope=all&page=1&size=50";
    const weiseApi = (ambiguous = true, extra: Record<string, Handler> = {}) =>
      mockApi({
        "GET /lemmas/bulk/occurrences": () => json(weiseResponse(ambiguous)),
        "GET /lemmas/bulk/word-ids": (url) => {
          const key = url.searchParams.get("original") ?? "";
          const ids = OWN[key] ?? [1, 2, 3, 4, 5, 6, 7, 8];
          return json({ total: ids.length, truncated: false, wordIds: ids });
        },
        ...extra,
      });

    it("groups by original and deselects only flagged groups when ambiguous", async () => {
      weiseApi();
      renderBulk(weiseUrl);
      await screen.findByRole("listbox");
      expect(screen.getByRole("button", { name: "Original" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
      expect(screen.getByText(/steht für 3 verschiedene Originalwörter/)).toBeInTheDocument();
      // synonym group (second original) stays selected, the flagged one is off but visible
      expect(await screen.findByText("6 von 8 ausgewählt")).toBeInTheDocument();
      expect(screen.getByText(/1 Originalgruppe abgewählt/)).toBeInTheDocument();
      expect(
        screen.getByRole("checkbox", {
          name: /Original G5158 τρόπος, 2 Treffer, nicht ausgewählt/,
        }),
      ).not.toBeChecked();
      expect(
        screen.getByRole("checkbox", { name: /Original G4680 σοφός, 2 Treffer, ausgewählt/ }),
      ).toBeChecked();
      expect(screen.getByText("»Art«")).toBeInTheDocument();
      expect(screen.getByText("andere Bedeutung")).toBeInTheDocument();
    });

    it("lets the user switch the grouping and keeps it in the URL", async () => {
      weiseApi();
      renderBulk(weiseUrl);
      await screen.findByRole("listbox");
      await userEvent.click(screen.getByRole("button", { name: "Form" }));
      await waitFor(() => expect(searchOf()).toContain("grp=form"));
      await screen.findByRole("checkbox", { name: /^Gruppe Weise/ });
      expect(screen.getByRole("button", { name: "Form" })).toHaveAttribute("aria-pressed", "true");
      expect(screen.queryByRole("checkbox", { name: /^Original G/ })).toBeNull();
      // one form group over all originals, mixed because the minority original is off
      expect(
        screen.getByRole("checkbox", {
          name: /Gruppe Weise, NOUN, 8 Treffer, teilweise ausgewählt/,
        }),
      ).toBeInTheDocument();
    });

    it("stays on the form view and shows no hint when the lemma is not ambiguous", async () => {
      weiseApi(false);
      renderBulk(weiseUrl);
      await screen.findByRole("listbox");
      expect(screen.getByRole("button", { name: "Form" })).toHaveAttribute("aria-pressed", "true");
      expect(screen.queryByText(/steht für/)).toBeNull();
      expect(screen.getByText("8 von 8 ausgewählt")).toBeInTheDocument();
    });

    it("shows the original word, its Strong's number and a mapping link per row", async () => {
      weiseApi();
      renderBulk(weiseUrl);
      await screen.findByRole("listbox");
      const link = screen.getAllByRole("link", { name: /Zuordnung von Mt 1,1 öffnen/ })[0];
      expect(link).toHaveAttribute("target", "_blank");
      expect(link).toHaveAttribute("href", "/bible/40/1#v1");
      expect(screen.getAllByText("H2450G").length).toBeGreaterThan(0);
      expect(screen.getAllByText("τρόπος").length).toBeGreaterThan(0);
    });

    it("sends the expected original parallel to the word ids", async () => {
      const calls = weiseApi();
      renderBulk(weiseUrl);
      await screen.findByRole("listbox");
      await screen.findByText("6 von 8 ausgewählt");
      await userEvent.click(screen.getByRole("button", { name: /Auswahl anwenden \(6\)/ }));
      await screen.findByText(/Vorkommen aktualisiert/);
      const apply = calls.find((c) => c.method === "POST" && c.url.pathname.endsWith("/apply"));
      expect(apply?.body).toEqual({
        from: ["Weise"],
        to: "Weiser",
        wordIds: [1, 2, 3, 4, 5, 6],
        expectedOriginal: ["H2450", "H2450", "H2450", "H2450", "G4680", "G4680"],
      });
      const idCalls = calls.filter((c) => c.url.pathname.endsWith("/word-ids"));
      expect(idCalls.map((c) => c.url.searchParams.get("original")).sort()).toEqual([
        "G4680",
        "H2450",
      ]);
    });

    it("names the groups left out and starts a new run with the target field focused", async () => {
      weiseApi(true, { "POST /lemmas/bulk/apply": () => json({ changeSetId: 9, updated: 6 }) });
      renderBulk(weiseUrl);
      await screen.findByText("6 von 8 ausgewählt");
      await userEvent.click(screen.getByRole("button", { name: /Auswahl anwenden \(6\)/ }));
      expect((await screen.findAllByText(/τρόπος \(2\)/)).length).toBeGreaterThan(0);
      await userEvent.click(
        screen.getByRole("button", { name: /τρόπος \(2\): anderes Lemma setzen/ }),
      );
      const target = await screen.findByRole("textbox", { name: "Durch dieses Lemma" });
      expect(target).toHaveValue("");
      await waitFor(() => expect(target).toHaveFocus());
      expect(await screen.findByRole("combobox", { name: /Original/ })).toHaveValue("G5158");
    });

    it("shows why a row was refused on a 409", async () => {
      weiseApi(true, {
        "POST /lemmas/bulk/apply": () =>
          json(
            {
              stale: [1, 5],
              details: [
                { wordId: 1, reason: "original", origKey: "G5158" },
                { wordId: 5, reason: "lemma", lemma: "Weiser" },
              ],
            },
            409,
          ),
      });
      renderBulk(weiseUrl);
      await screen.findByText("6 von 8 ausgewählt");
      await userEvent.click(screen.getByRole("button", { name: /Auswahl anwenden \(6\)/ }));
      expect(await screen.findByText("Zuordnung geändert (jetzt G5158)")).toBeInTheDocument();
      expect(screen.getByText("schon geändert (jetzt »Weiser«)")).toBeInTheDocument();
      expect(screen.getByText(/2 Zeilen haben sich inzwischen geändert/)).toBeInTheDocument();
      expect(screen.getByText("4 von 8 ausgewählt")).toBeInTheDocument();
    });

    it("offers an original filter in the search step and explains an ambiguous lemma", async () => {
      weiseApi();
      renderBulk("/?dlg=search", { initialFrom: "Weise" });
      expect(
        await screen.findByText(/steht für 3 verschiedene Originalwörter/),
      ).toBeInTheDocument();
      const select = await screen.findByRole("combobox", { name: /Original/ });
      expect(
        within(select).getByRole("option", { name: /G5158 τρόπος »Art« 2/ }),
      ).toBeInTheDocument();
      await userEvent.selectOptions(select, "G5158");
      await userEvent.type(screen.getByRole("textbox", { name: "Durch dieses Lemma" }), "Weiser");
      await userEvent.click(screen.getByRole("button", { name: "Suchen" }));
      await waitFor(() => expect(searchOf()).toContain("orig=G5158"));
    });
  });
});
