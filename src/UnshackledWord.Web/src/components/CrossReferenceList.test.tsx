import { QueryClient, QueryClientProvider, useQuery } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { bibleApi } from "@/lib/bible/api";
import { sidePanelKeys } from "@/lib/bible/sidePanel";
import type { BibleStructureBook, CrossReference } from "@/lib/bible/types";
import { CrossReferenceList, cardLeft } from "./CrossReferenceList";

const books = [
  { id: 1, nameDe: "1. Mose", nameEn: "Genesis", abbreviationDe: "1Mo", abbreviationEn: "Gen" },
  { id: 19, nameDe: "Psalm", nameEn: "Psalms", abbreviationDe: "Ps", abbreviationEn: "Ps" },
] as BibleStructureBook[];

const references = [
  { bibleBookId: 1, chapter: 1, verse: 10 },
  { bibleBookId: 19, chapter: 33, verse: 6, endVerse: 9 },
  { bibleBookId: 1, chapter: 2, verse: 4 },
];

function renderInline() {
  return render(
    <MemoryRouter>
      <CrossReferenceList variant="inline" references={references} books={books} />
    </MemoryRouter>,
  );
}

describe("CrossReferenceList inline", () => {
  it("links abbreviated labels to the verse and names the full book for screen readers", () => {
    renderInline();
    const link = screen.getByRole("link", { name: /1\. Mose 1,10|Genesis 1,10/ });
    expect(link).toHaveTextContent(/^(1Mo|Gen) 1,10$/);
    expect(link).toHaveAttribute("href", "/bible/1/1#v10");
    expect(screen.getByRole("link", { name: /33,6–9/ })).toHaveAttribute("href", "/bible/19/33#v6");
  });

  it("is one Tab stop and moves with the arrow keys", async () => {
    const user = userEvent.setup();
    renderInline();
    const links = screen.getAllByRole("link");
    expect(links.map((l) => l.tabIndex)).toEqual([0, -1, -1]);
    links[0]?.focus();
    await user.keyboard("{ArrowRight}");
    expect(links[1]).toHaveFocus();
    expect(links.map((l) => l.tabIndex)).toEqual([-1, 0, -1]);
    await user.keyboard("{End}");
    expect(links[2]).toHaveFocus();
    await user.keyboard("{Home}");
    expect(links[0]).toHaveFocus();
  });
});

describe("CrossReferenceList preview", () => {
  it("loads all texts of the verse once and shows the hovered target", async () => {
    const fetchMock = vi.fn(async () =>
      Response.json({
        references: [
          {
            bibleBookId: 1,
            chapter: 1,
            verse: 10,
            verses: [{ verse: 10, text: "Und Gott nannte" }],
          },
          { bibleBookId: 1, chapter: 2, verse: 4, verses: [{ verse: 4, text: "Dies ist die" }] },
        ],
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter>
          <CrossReferenceList
            variant="inline"
            references={[
              { bibleBookId: 1, chapter: 1, verse: 10 },
              { bibleBookId: 1, chapter: 2, verse: 4 },
            ]}
            books={books}
            preview={{ bookId: 43, chapter: 3, verse: 1, level: 200 }}
          />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    const [first, second] = screen.getAllByRole("link") as [HTMLElement, HTMLElement];
    await user.hover(first);
    expect(await screen.findByText("Und Gott nannte")).toBeInTheDocument();
    await user.unhover(first);
    await user.hover(second);
    expect(await screen.findByText("Dies ist die")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
    vi.unstubAllGlobals();
  });

  it("widens the card for originals from md and keeps it inside the viewport at the right edge", async () => {
    vi.stubGlobal("innerWidth", 1000);
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query === "(min-width: 48rem)",
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          references: [
            {
              bibleBookId: 1,
              chapter: 1,
              verse: 10,
              verses: [
                {
                  verse: 10,
                  text: "Und Gott nannte",
                  original: { language: "hebrew", text: "וַיִּקְרָא" },
                },
              ],
            },
            { bibleBookId: 1, chapter: 2, verse: 4, verses: [{ verse: 4, text: "Dies ist die" }] },
          ],
        }),
      ),
    );
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter>
          <CrossReferenceList
            variant="inline"
            references={[
              { bibleBookId: 1, chapter: 1, verse: 10 },
              { bibleBookId: 1, chapter: 2, verse: 4 },
            ]}
            books={books}
            preview={{ bookId: 1, chapter: 1, verse: 1, level: 200 }}
          />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    const [first, second] = screen.getAllByRole("link") as [HTMLElement, HTMLElement];
    const at = (element: HTMLElement, left: number) => {
      const rect = {
        left,
        right: left + 40,
        top: 100,
        bottom: 120,
        width: 40,
        height: 20,
      } as DOMRect;
      element.getBoundingClientRect = () => rect;
      element.getClientRects = () => [rect] as unknown as DOMRectList;
    };
    at(first, 900);
    at(second, 900);
    await user.hover(first);
    expect(await screen.findByText("וַיִּקְרָא")).toBeInTheDocument();
    // wide card: 1000 - 640 - 8
    expect(screen.getByRole("tooltip").style.left).toBe("352px");
    await user.unhover(first);
    await user.hover(second);
    expect(await screen.findByText("Dies ist die")).toBeInTheDocument();
    // no originals: the narrow card, 1000 - 320 - 8
    expect(screen.getByRole("tooltip").style.left).toBe("672px");
    vi.unstubAllGlobals();
  });
});

describe("cardLeft", () => {
  it("clamps to the viewport with the narrow and the wide width", () => {
    expect(cardLeft(100, false, 1000)).toBe(100);
    expect(cardLeft(900, false, 1000)).toBe(672);
    expect(cardLeft(900, true, 1000)).toBe(352);
    expect(cardLeft(2, true, 1000)).toBe(8);
    // phone narrower than the card: the left gap wins
    expect(cardLeft(200, false, 300)).toBe(8);
  });
});

describe("CrossReferenceList curation (roadmap 0024.3f)", () => {
  const curated: CrossReference[] = [
    { bibleBookId: 19, chapter: 33, verse: 6, rating: "highlight", comment: "Schöpfung" },
    { bibleBookId: 1, chapter: 1, verse: 10 },
    { bibleBookId: 1, chapter: 2, verse: 4, rating: "hide" },
  ];
  const source = { bookId: 1, chapter: 1, verse: 1, level: 200 };

  function renderList(
    props: Partial<Parameters<typeof CrossReferenceList>[0]> = {},
    client = new QueryClient({ defaultOptions: { queries: { retry: false } } }),
  ) {
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <CrossReferenceList variant="inline" references={curated} books={books} {...props} />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    return client;
  }

  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });
  afterEach(() => vi.unstubAllGlobals());

  it("tints pills linked to a word and flashes the ones in `flashKeys`", () => {
    const linked: CrossReference = {
      bibleBookId: 19,
      chapter: 33,
      verse: 6,
      word: { startPosition: 2, endPosition: 2, quotedText: "Wort", state: "ok" },
    };
    renderList({ references: [linked], flashKeys: new Set(["19-33-6-0"]) });
    expect(screen.getByRole("link").className).toContain("ring-mark-linked");
    cleanup();
    renderList({ references: [linked] });
    expect(screen.getByRole("link").className).toContain("bg-mark-link/40");
  });

  it("reports the hovered link so the reader can mark its word", async () => {
    const user = userEvent.setup();
    const onHoverReference = vi.fn();
    renderList({ references: [curated[1] as CrossReference], onHoverReference });
    const link = screen.getByRole("link");
    await user.hover(link);
    expect(onHoverReference).toHaveBeenLastCalledWith(curated[1]);
    await user.unhover(link);
    expect(onHoverReference).toHaveBeenLastCalledWith(undefined);
  });

  it("marks highlighted links with a star and font-medium, in the order the API sends", () => {
    renderList();
    const links = screen.getAllByRole("link");
    expect(links.map((l) => l.textContent)).toEqual(["Ps 33,6 (hervorgehoben)", "1Mo 1,10"]);
    expect(links[0]).toHaveAccessibleName("Psalm 33,6 (hervorgehoben)");
    expect(links[0]?.className).toContain("font-medium");
    expect(links[0]?.querySelector("svg")).not.toBeNull();
    expect(links[1]?.className).not.toContain("font-medium");
    expect(links[1]?.querySelector("svg")).toBeNull();
  });

  it("never lists hidden links for readers, also in the details variant", () => {
    renderList();
    expect(screen.queryByRole("link", { name: /1\. Mose 2,4/ })).toBeNull();
    cleanup();
    renderList({ variant: "details" });
    expect(screen.getAllByRole("link")).toHaveLength(2);
    expect(screen.getByText("Querverweise zu diesem Vers (2)")).toBeInTheDocument();
  });

  it("lists hidden links greyed and struck through in curate mode", () => {
    renderList({ showHidden: true });
    const hidden = screen.getByRole("link", { name: /1\. Mose 2,4/ });
    expect(hidden.className).toContain("text-fg-muted");
    expect(hidden.className).toContain("line-through");
    expect(hidden).toHaveTextContent("(ausgeblendet)");
  });

  it("shows the comment of a link in its hover card", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        Response.json({
          references: [
            {
              bibleBookId: 19,
              chapter: 33,
              verse: 6,
              rating: "highlight",
              comment: "Schöpfung",
              verses: [{ verse: 6, text: "Durch des HERRN Wort" }],
            },
          ],
        }),
      ),
    );
    const user = userEvent.setup();
    renderList({ preview: source });
    await user.hover(screen.getAllByRole("link")[0] as HTMLElement);
    expect(await screen.findByText("Durch des HERRN Wort")).toBeInTheDocument();
    expect(screen.getByTestId("preview-comment")).toHaveTextContent("Schöpfung");
  });

  it("has no pen without `editable`", () => {
    renderList({ preview: source });
    expect(screen.queryByRole("button", { name: /bearbeiten/ })).toBeNull();
  });

  it("a click on the link opens the editor, saves rating and comment and invalidates the TSK queries", async () => {
    const fetchMock = vi.fn(async (_url: string, init?: RequestInit) =>
      Response.json(
        init?.method === "PUT"
          ? { sourceHebRefId: 1001001, targetStartHebRefId: 1001010, origin: "tsk" }
          : { references: [] },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    const client = new QueryClient();
    const invalidate = vi.spyOn(client, "invalidateQueries");
    renderList({ preview: source, editable: true }, client);

    await user.click(screen.getByRole("link", { name: "1. Mose 1,10" }));
    const editor = screen.getByRole("dialog", { name: "Querverweis 1. Mose 1,10 bearbeiten" });
    expect(within(editor).getByRole("button", { name: "Neutral" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    const save = within(editor).getByRole("button", { name: "Speichern" });
    expect(save).toBeDisabled();
    await user.click(within(editor).getByRole("button", { name: "Hervorheben" }));
    await user.type(within(editor).getByLabelText("Kommentar"), "  Licht  ");
    expect(within(editor).getByText("9/500")).toBeInTheDocument();
    await user.click(save);

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const put = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(put?.[0]).toMatch(/\/tsk\/curation$/);
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({
      sourceHebRefId: 1001001,
      targetStartHebRefId: 1001010,
      rating: "highlight",
      comment: "Licht",
    });
    const keys = invalidate.mock.calls.map(([filters]) => filters?.queryKey);
    expect(keys).toEqual(
      expect.arrayContaining([
        ["bible", "chapterCrossReferences"],
        ["bible", "crossReferenceTexts"],
        ["bible", "wordDetail"],
      ]),
    );
    // focus goes back to the link
    expect(screen.getByRole("link", { name: "1. Mose 1,10" })).toHaveFocus();
  });

  it("un-hides a hidden link with an empty comment as null and keeps a range key", async () => {
    const fetchMock = vi.fn(async () => Response.json({ origin: "tsk" }));
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderList({
      preview: source,
      editable: true,
      showHidden: true,
      references: [
        { bibleBookId: 20, chapter: 8, verse: 22, endVerse: 24, rating: "hide", comment: "alt" },
      ],
    });
    await user.click(screen.getByRole("link"));
    const editor = screen.getByRole("dialog");
    expect(within(editor).getByRole("button", { name: "Ausblenden" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    await user.click(within(editor).getByRole("button", { name: "Neutral" }));
    await user.clear(within(editor).getByLabelText("Kommentar"));
    await user.click(within(editor).getByRole("button", { name: "Speichern" }));
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(JSON.parse(String(init.body))).toEqual({
      sourceHebRefId: 1001001,
      targetStartHebRefId: 20008022,
      targetEndHebRefId: 20008024,
      rating: null,
      comment: null,
    });
  });

  it("closes the editor on Abbrechen and Escape without saving", async () => {
    const fetchMock = vi.fn(async () => Response.json({ references: [] }));
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderList({ preview: source, editable: true });
    const pen = screen.getByRole("link", { name: "1. Mose 1,10" });
    await user.click(pen);
    await user.click(screen.getByRole("button", { name: "Abbrechen" }));
    expect(screen.queryByRole("dialog")).toBeNull();
    await user.click(pen);
    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).toBeNull();
    // opening the editor may load data, but nothing is written
    for (const [, init] of fetchMock.mock.calls as unknown as [string, RequestInit | undefined][]) {
      expect(init?.method ?? "GET").toBe("GET");
    }
  });
});

const addBooks = [
  ...books,
  { id: 45, nameDe: "Römer", nameEn: "Romans", abbreviationDe: "Röm", abbreviationEn: "Rom" },
] as BibleStructureBook[];

/** The side panel list of 1Mo 50,20 fed from the chapter query, so invalidation refetches it. */
function Harness({ editable = true }: { editable?: boolean }) {
  const query = useQuery({
    queryKey: sidePanelKeys.crossReferences(1, 50, false),
    queryFn: ({ signal }) => bibleApi.chapterCrossReferences(1, 50, signal),
  });
  const references = query.data?.verses.find((v) => v.verse === 20)?.references;
  if (!references) return null;
  return (
    <CrossReferenceList
      variant="inline"
      references={references}
      books={addBooks}
      preview={{ bookId: 1, chapter: 50, verse: 20, level: 200 }}
      editable={editable}
    />
  );
}

describe("CrossReferenceList add (roadmap 0024.4f)", () => {
  const tsk: CrossReference = { bibleBookId: 1, chapter: 45, verse: 5 };
  const romans: CrossReference = {
    bibleBookId: 45,
    chapter: 8,
    verse: 28,
    endVerse: 30,
    origin: "added",
  };

  /** The API of 1Mo 50,20: parse, passage text, chapter list (with Röm 8,28-30 once added) and the writes. */
  function stubApi({ addStatus = 200, startAdded = false } = {}) {
    let added = startAdded;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.includes("/references/parse")) {
        const q = new URL(url, "http://x").searchParams.get("q");
        return Response.json(
          q === "Röm 8,28-30"
            ? {
                references: [
                  {
                    bookId: 45,
                    bookOnly: false,
                    display: "Römer 8,28–30",
                    spans: [{ startChapter: 8, startVerse: 28, endChapter: 8, endVerse: 30 }],
                    groups: [],
                  },
                ],
                bookCandidates: [],
              }
            : {
                references: [],
                error: { code: "unknownBook", position: 0, args: {} },
                bookCandidates: [],
              },
        );
      }
      if (url.includes("/passageText")) {
        return Response.json({
          passages: [
            {
              bookId: 45,
              chapter: 8,
              verse: 28,
              endVerse: 30,
              verses: [
                { verse: 28, text: "Wir wissen aber, dass denen, die Gott lieben" },
                { verse: 29, text: "Denn welche er zuvorerkannt hat" },
                { verse: 30, text: "Welche er aber zuvorbestimmt hat" },
              ],
            },
          ],
          truncated: false,
        });
      }
      if (url.includes("/tsk/curation/added")) {
        if (init?.method === "DELETE") {
          added = false;
          return new Response(null, { status: 204 });
        }
        if (addStatus !== 200) {
          return Response.json(
            { statusCode: addStatus, errors: { generalErrors: ["The link already exists."] } },
            { status: addStatus },
          );
        }
        added = true;
        return Response.json({
          sourceHebRefId: 1050020,
          targetStartHebRefId: 45008028,
          targetEndHebRefId: 45008030,
          origin: "added",
          updatedAt: "2026-11-23T10:00:00Z",
        });
      }
      if (url.includes("/chapterCrossReferences/")) {
        return Response.json({
          bibleBookId: 1,
          chapter: 50,
          verses: [{ verse: 20, references: added ? [tsk, romans] : [tsk] }],
        });
      }
      return Response.json({ references: [] });
    });
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
  }

  function renderHarness(editable = true) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <Harness editable={editable} />
        </MemoryRouter>
      </QueryClientProvider>,
    );
  }

  const posts = (fetchMock: ReturnType<typeof stubApi>) =>
    fetchMock.mock.calls.filter(([, init]) => init?.method === "POST");

  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });
  afterEach(() => vi.unstubAllGlobals());

  it("adds Röm 8,28-30 under 1Mo 50,20: parse, preview, POST, listed with a + after the refetch", async () => {
    const fetchMock = stubApi();
    const user = userEvent.setup();
    renderHarness();
    await user.click(await screen.findByRole("button", { name: "Querverweis hinzufügen" }));
    const editor = screen.getByRole("dialog", { name: "Querverweis hinzufügen" });
    const save = within(editor).getByRole("button", { name: "Speichern" });
    expect(save).toBeDisabled();

    await user.type(within(editor).getByLabelText("Bibelstelle"), "Röm 8,28-30");
    expect(await within(editor).findByText("Römer 8,28–30")).toBeInTheDocument();
    expect(
      await within(editor).findByText("Wir wissen aber, dass denen, die Gott lieben"),
    ).toBeInTheDocument();
    await user.type(within(editor).getByLabelText("Kommentar"), " Gottes Absicht ");
    expect(save).toBeEnabled();
    await user.click(save);

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const [post] = posts(fetchMock);
    expect(post?.[0]).toMatch(/\/tsk\/curation\/added$/);
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({
      sourceHebRefId: 1050020,
      reference: "Röm 8,28-30",
      comment: "Gottes Absicht",
    });
    const link = await screen.findByRole("link", { name: "Römer 8,28–30 (hinzugefügt)" });
    expect(link).toHaveAttribute("href", "/bible/45/8#v28");
    expect(within(link).getByTestId("added-marker")).toHaveTextContent("+");
    expect(screen.getByRole("link", { name: "1. Mose 45,5" })).toBeInTheDocument();
  });

  it("shows the parser's error for „Xyz 1,1“, keeps Speichern disabled and sends nothing", async () => {
    const fetchMock = stubApi();
    const user = userEvent.setup();
    renderHarness();
    await user.click(await screen.findByRole("button", { name: "Querverweis hinzufügen" }));
    const editor = screen.getByRole("dialog");
    await user.type(within(editor).getByLabelText("Bibelstelle"), "Xyz 1,1");
    expect(await within(editor).findByText("Dieses Buch ist nicht bekannt.")).toBeInTheDocument();
    expect(within(editor).getByLabelText("Bibelstelle")).toHaveAttribute("aria-invalid", "true");
    const save = within(editor).getByRole("button", { name: "Speichern" });
    expect(save).toBeDisabled();
    await user.type(within(editor).getByLabelText("Bibelstelle"), "{Enter}");
    expect(posts(fetchMock)).toHaveLength(0);
    expect(screen.queryByTestId("cross-reference-add-preview")).toBeNull();
  });

  it("shows a 409 as a readable message and keeps the form open", async () => {
    stubApi({ addStatus: 409 });
    const user = userEvent.setup();
    renderHarness();
    await user.click(await screen.findByRole("button", { name: "Querverweis hinzufügen" }));
    const editor = screen.getByRole("dialog");
    await user.type(within(editor).getByLabelText("Bibelstelle"), "Röm 8,28-30");
    await within(editor).findByText("Römer 8,28–30");
    await user.click(within(editor).getByRole("button", { name: "Speichern" }));
    expect(
      await within(editor).findByText("Diesen Querverweis gibt es für diesen Vers schon."),
    ).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("removes an added link from its editor after a confirm step (DELETE with the key in the body)", async () => {
    const fetchMock = stubApi({ startAdded: true });
    const user = userEvent.setup();
    renderHarness();
    await user.click(await screen.findByRole("link", { name: /^Römer 8,28–30/ }));
    const editor = screen.getByRole("dialog");
    await user.click(within(editor).getByRole("button", { name: "Querverweis entfernen" }));
    expect(
      within(editor).getByText("Diesen hinzugefügten Querverweis wirklich entfernen?"),
    ).toBeInTheDocument();
    // back without a request
    await user.click(within(editor).getByRole("button", { name: "Abbrechen" }));
    expect(within(editor).queryByText(/wirklich entfernen/)).toBeNull();
    await user.click(within(editor).getByRole("button", { name: "Querverweis entfernen" }));
    await user.click(within(editor).getByRole("button", { name: "Entfernen" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const deletes = fetchMock.mock.calls.filter(([, init]) => init?.method === "DELETE");
    expect(deletes).toHaveLength(1);
    expect(deletes[0]?.[0]).toMatch(/\/tsk\/curation\/added$/);
    expect(JSON.parse(String(deletes[0]?.[1]?.body))).toEqual({
      sourceHebRefId: 1050020,
      targetStartHebRefId: 45008028,
      targetEndHebRefId: 45008030,
    });
    await waitFor(() => expect(screen.queryByRole("link", { name: /Römer 8/ })).toBeNull());
  });

  it("offers no remove action for a TSK link and no add action without `editable`", async () => {
    stubApi();
    const user = userEvent.setup();
    renderHarness();
    await user.click(await screen.findByRole("link", { name: "1. Mose 45,5" }));
    expect(screen.queryByRole("button", { name: "Querverweis entfernen" })).toBeNull();
    cleanup();
    renderHarness(false);
    await screen.findByRole("link", { name: "1. Mose 45,5" });
    expect(screen.queryByRole("button", { name: "Querverweis hinzufügen" })).toBeNull();
  });
});
