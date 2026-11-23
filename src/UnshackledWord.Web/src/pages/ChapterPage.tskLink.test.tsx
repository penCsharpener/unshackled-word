import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { SIDE_PANEL_KEY, TSK_ONLY_HIGHLIGHTED_KEY } from "@/lib/bible/sidePanel";
import type {
  BibleStructureBook,
  CrossReference,
  GetWordsOfChapterResponse,
} from "@/lib/bible/types";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { ChapterPage } from "./ChapterPage";

vi.mock("@mdxeditor/editor", () => import("@/test/mdxEditorMock"));

const book = (id: number, nameDe: string, abbreviationDe: string): BibleStructureBook => ({
  id,
  testament: id < 40 ? "ot" : "nt",
  nameDe,
  nameEn: nameDe,
  abbreviationDe,
  abbreviationEn: abbreviationDe,
  versesPerChapter: Array<number>(30).fill(30),
});
const books = [
  ...fixtureBooks,
  book(20, "Sprüche", "Spr"),
  book(43, "Johannes", "Joh"),
  book(58, "Hebräer", "Hebr"),
];

const sprueche: CrossReference = { bibleBookId: 20, chapter: 8, verse: 22, endVerse: 24 };
const johannes: CrossReference = { bibleBookId: 43, chapter: 1, verse: 1, endVerse: 3 };
const hebraeer: CrossReference = { bibleBookId: 58, chapter: 1, verse: 10 };
const psalm: CrossReference = { bibleBookId: 19, chapter: 33, verse: 6 };
const verse2Ref: CrossReference = { bibleBookId: 19, chapter: 104, verse: 30 };
const anfang = { startPosition: 2, endPosition: 2, quotedText: "Anfang", state: "ok" as const };

const w = (id: number, verse: number, positionInVerse: number, wordInContext: string) => ({
  id,
  verse,
  positionInVerse,
  wordInContext,
});
const genesisWords: GetWordsOfChapterResponse = {
  bibleBookId: 1,
  chapter: 1,
  words: [
    w(1, 1, 1, "Im"),
    w(2, 1, 2, "Anfang"),
    w(3, 1, 3, "schuf"),
    w(4, 1, 4, "Gott"),
    w(5, 2, 1, "Und"),
    w(6, 2, 2, "die"),
    w(7, 2, 3, "Erde"),
  ],
};
/** Verse 1 at level 300: „Ganz“ is new, the others keep their Elb1871 positions shifted by one. */
const revisedWords: GetWordsOfChapterResponse = {
  ...genesisWords,
  words: [
    { id: 0, verse: 1, positionInVerse: 1, wordInContext: "Ganz", revisionWordId: 501 },
    {
      id: 1,
      verse: 1,
      positionInVerse: 2,
      wordInContext: "im",
      revisionWordId: 502,
      basePositionInVerse: 1,
    },
    {
      id: 2,
      verse: 1,
      positionInVerse: 3,
      wordInContext: "Anfang",
      revisionWordId: 503,
      basePositionInVerse: 2,
    },
    {
      id: 4,
      verse: 1,
      positionInVerse: 4,
      wordInContext: "Gott",
      revisionWordId: 504,
      basePositionInVerse: 4,
    },
    ...genesisWords.words.filter((x) => x.verse !== 1),
  ],
  revisedVerses: [{ verse: 1, level: 300, revisionVerseId: 9 }],
};
const levels = [
  { level: 200, code: "elb1871", name: "Elb 1871", isBase: true, isDefault: false },
  { level: 300, code: "revision", name: "Revision", isBase: false, isDefault: true },
];

interface Options {
  /** references of 1Mo 1,1 (before any link write) */
  verse1?: CrossReference[];
  revised?: boolean;
}

/** A maintainer and a stateful TSK list: after `PUT tsk/curation/word` the linked refs carry the word. */
function stubFetch({
  verse1 = [sprueche, johannes, hebraeer, psalm],
  revised = false,
}: Options = {}) {
  let current = verse1;
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.includes("/meta-bible/structure")) return new Response(JSON.stringify({ books }));
    if (url.includes("/auth/me")) {
      return new Response(
        JSON.stringify({ userId: 1, email: "a@b.c", displayName: "Ada", roles: ["Maintainer"] }),
      );
    }
    if (url.includes("/revision/levels")) {
      return new Response(JSON.stringify(revised ? levels : []));
    }
    if (url.endsWith("/tsk/curation/word") && init?.method === "PUT") {
      const body = JSON.parse(String(init.body)) as {
        keys: { targetStartHebRefId: number }[];
        startPosition: number;
        endPosition: number;
      };
      const linked = new Set(body.keys.map((k) => k.targetStartHebRefId));
      const id = (r: CrossReference) => r.bibleBookId * 1_000_000 + r.chapter * 1_000 + r.verse;
      current = current.map((r) =>
        linked.has(id(r)) ? { ...r, word: { ...anfang, quotedText: "Anfang" } } : r,
      );
      return new Response(JSON.stringify({ links: [] }));
    }
    if (url.endsWith("/tsk/curation/word") && init?.method === "DELETE") {
      current = current.map(({ word: _, ...r }) => r);
      return new Response(JSON.stringify({ links: [] }));
    }
    if (url.includes("/chapterCrossReferences/")) {
      return new Response(
        JSON.stringify({
          bibleBookId: 1,
          chapter: 1,
          verses: [
            { verse: 1, references: current },
            { verse: 2, references: [verse2Ref] },
          ],
        }),
      );
    }
    if (url.includes("/crossReferenceTexts/")) {
      return new Response(
        JSON.stringify({
          references: [
            {
              ...psalm,
              verses: [
                {
                  verse: 6,
                  text: "Durch des HERRN Wort sind die Himmel gemacht",
                  original: { language: "hebrew", text: "בִּדְבַר יְהוָה" },
                },
              ],
            },
          ],
        }),
      );
    }
    if (url.includes("/chapterOriginals/")) return new Response(JSON.stringify({ verses: [] }));
    if (url.includes("/notes/chapter/")) return new Response(JSON.stringify({ notes: [] }));
    if (url.includes("chapterWords")) {
      return new Response(JSON.stringify(url.includes("level=300") ? revisedWords : genesisWords));
    }
    return new Response("{}", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [{ path: "/bible/:bookId/:chapter", element: <ChapterPage /> }],
    { initialEntries: [path] },
  );
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

/** Every media query matches: the 120rem layout with the left margin. */
function stubWideScreen() {
  vi.stubGlobal(
    "matchMedia",
    (query: string) =>
      ({
        matches: true,
        media: query,
        addEventListener: () => {},
        removeEventListener: () => {},
      }) as unknown as MediaQueryList,
  );
}

const wordButton = (verse: number, word: string) =>
  within(document.querySelector(`#v${verse} .verse-main`) as HTMLElement).getByRole("button", {
    name: `Grundtext zu „${word}“ anzeigen`,
  });
const checkbox = (name: string) => screen.getByRole("checkbox", { name: `${name} auswählen` });
const bodyOf = (fetchMock: ReturnType<typeof stubFetch>, method: string) =>
  JSON.parse(
    String(
      fetchMock.mock.calls.find(
        ([url, init]) => String(url).endsWith("/tsk/curation/word") && init?.method === method,
      )?.[1]?.body,
    ),
  );

async function openLinkMode() {
  const user = userEvent.setup();
  await user.click(await screen.findByRole("switch", { name: "Verknüpfen" }));
  await screen.findByRole("checkbox", { name: "Sprüche 8,22–24 auswählen" });
  return user;
}

beforeEach(async () => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  await i18n.changeLanguage("de");
  stubWideScreen();
});
afterEach(() => {
  vi.unstubAllGlobals();
  localStorage.removeItem(SIDE_PANEL_KEY);
  localStorage.removeItem(TSK_ONLY_HIGHLIGHTED_KEY);
});

describe("ChapterPage TSK link mode (roadmap 0024.5f)", () => {
  it("drags three selected refs of 1Mo 1,1 onto „Anfang“ and groups them under the word", async () => {
    const fetchMock = stubFetch();
    renderAt("/bible/1/1");
    const user = await openLinkMode();
    // click selects one, Shift-click the run up to Hebräer 1,10
    await user.click(checkbox("Sprüche 8,22–24"));
    await user.keyboard("{Shift>}");
    await user.click(checkbox("Hebräer 1,10"));
    await user.keyboard("{/Shift}");
    expect(checkbox("Johannes 1,1–3")).toBeChecked();
    expect(checkbox("Psalmen 33,6")).not.toBeChecked();
    expect(screen.getByTestId("tsk-link-hint")).toHaveTextContent("3 Querverweise ausgewählt");

    const dragged = checkbox("Johannes 1,1–3").closest("li") as HTMLElement;
    fireEvent.dragStart(dragged);
    const target = wordButton(1, "Anfang");
    fireEvent.dragOver(target);
    expect(target.className).toContain("bg-mark-link");
    fireEvent.drop(target);
    fireEvent.dragEnd(dragged);

    await waitFor(() =>
      expect(bodyOf(fetchMock, "PUT")).toEqual({
        sourceHebRefId: 1001001,
        keys: [
          { targetStartHebRefId: 20008022, targetEndHebRefId: 20008024 },
          { targetStartHebRefId: 43001001, targetEndHebRefId: 43001003 },
          { targetStartHebRefId: 58001010 },
        ],
        startPosition: 2,
        endPosition: 2,
      }),
    );
    const group = await screen.findByRole("group", { name: "Querverweise zu „Anfang“" });
    expect(
      within(group)
        .getAllByRole("link")
        .map((l) => l.getAttribute("aria-label")),
    ).toEqual(["Sprüche 8,22–24", "Johannes 1,1–3", "Hebräer 1,10"]);
    const rest = within(screen.getByRole("group", { name: "Querverweise zu Vers 1" })).getByRole(
      "group",
      { name: "Querverweise ohne Wort" },
    );
    expect(within(rest).getAllByRole("link")).toHaveLength(1);
    // the reader marks the linked word; the drop feedback is gone
    expect(wordButton(1, "Anfang")).toHaveAttribute("data-tsk-link");
    expect(wordButton(1, "Anfang").className).not.toContain("bg-mark-link");
    expect(wordButton(1, "Gott")).not.toHaveAttribute("data-tsk-link");
    // the selection is cleared after the link
    expect(screen.queryByTestId("tsk-link-hint")).toBeNull();
  });

  it("links the selection with Enter on a word of the source verse (touch / keyboard path)", async () => {
    const fetchMock = stubFetch();
    renderAt("/bible/1/1");
    const user = await openLinkMode();
    await user.click(checkbox("Hebräer 1,10"));
    expect(screen.getByTestId("tsk-link-hint")).toHaveTextContent(
      "Auf Wort legen: 1 Querverweis ausgewählt",
    );
    // a word of another verse does nothing
    wordButton(2, "Erde").focus();
    await user.keyboard("{Enter}");
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("/tsk/curation/word"))).toBe(
      false,
    );
    wordButton(1, "Gott").focus();
    await user.keyboard("{Enter}");
    await waitFor(() =>
      expect(bodyOf(fetchMock, "PUT")).toMatchObject({
        sourceHebRefId: 1001001,
        keys: [{ targetStartHebRefId: 58001010 }],
        startPosition: 4,
        endPosition: 4,
      }),
    );
  });

  it("Esc and Abbrechen clear the selection", async () => {
    stubFetch();
    renderAt("/bible/1/1");
    const user = await openLinkMode();
    await user.click(checkbox("Psalmen 33,6"));
    await user.keyboard("{Escape}");
    expect(checkbox("Psalmen 33,6")).not.toBeChecked();
    await user.click(checkbox("Psalmen 33,6"));
    await user.click(screen.getByRole("button", { name: "Abbrechen" }));
    expect(screen.queryByTestId("tsk-link-hint")).toBeNull();
  });

  it("does not take a drop on a word of another verse", async () => {
    const fetchMock = stubFetch();
    renderAt("/bible/1/1");
    await openLinkMode();
    const dragged = checkbox("Psalmen 33,6").closest("li") as HTMLElement;
    fireEvent.dragStart(dragged);
    expect(wordButton(1, "Anfang")).toHaveAttribute("data-link-drop");
    const other = wordButton(2, "Erde");
    expect(other).not.toHaveAttribute("data-link-drop");
    fireEvent.dragOver(other);
    expect(other.className).not.toContain("bg-mark-link");
    fireEvent.drop(other);
    fireEvent.dragEnd(dragged);
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("/tsk/curation/word"))).toBe(
      false,
    );
  });

  it("starts no notes selection in link mode, but does again with link mode off", async () => {
    stubFetch();
    const { container } = renderAt("/bible/1/1");
    const user = await openLinkMode();
    const verse1 = container.querySelector("#v1") as HTMLElement;
    const drag = () =>
      user.pointer([
        { keys: "[MouseLeft>]", target: wordButton(1, "Im") },
        { target: wordButton(1, "Anfang") },
        { target: wordButton(1, "schuf") },
        { keys: "[/MouseLeft]", target: wordButton(1, "schuf") },
      ]);
    await drag();
    expect(verse1.querySelectorAll("[data-selected]")).toHaveLength(0);
    expect(within(verse1).queryByRole("button", { name: "Notiz" })).toBeNull();

    await user.click(screen.getByRole("switch", { name: "Verknüpfen" }));
    await drag();
    expect(verse1.querySelectorAll("[data-selected]")).toHaveLength(3);
  });

  it("still shows the hover card in link mode, but not while dragging", async () => {
    stubFetch();
    renderAt("/bible/1/1");
    const user = await openLinkMode();
    const link = screen.getByRole("link", { name: "Psalmen 33,6" });
    await user.hover(link);
    expect(await screen.findByText("Durch des HERRN Wort sind die Himmel gemacht")).toBeVisible();
    expect(screen.getByText("בִּדְבַר יְהוָה")).toBeInTheDocument();
    await user.unhover(link);

    fireEvent.dragStart(link.closest("li") as HTMLElement);
    fireEvent.mouseEnter(link);
    expect(screen.queryByTestId("cross-reference-preview")).toBeNull();
  });

  it("unlinks a linked ref from the editor with DELETE", async () => {
    const fetchMock = stubFetch({ verse1: [{ ...sprueche, word: anfang }, psalm] });
    renderAt("/bible/1/1");
    const user = userEvent.setup();
    // outside link mode the panel is grouped as soon as a link has a word
    await screen.findByRole("group", { name: "Querverweise zu „Anfang“" });
    await user.click(
      screen.getByRole("button", { name: "Querverweis Sprüche 8,22–24 bearbeiten" }),
    );
    const editor = screen.getByRole("dialog", { name: "Querverweis Sprüche 8,22–24 bearbeiten" });
    expect(within(editor).getByText("Wort: „Anfang“")).toBeInTheDocument();
    await user.click(within(editor).getByRole("button", { name: "Wortverknüpfung lösen" }));
    await waitFor(() =>
      expect(bodyOf(fetchMock, "DELETE")).toEqual({
        sourceHebRefId: 1001001,
        keys: [{ targetStartHebRefId: 20008022, targetEndHebRefId: 20008024 }],
      }),
    );
    await waitFor(() =>
      expect(screen.queryByRole("group", { name: "Querverweise zu „Anfang“" })).toBeNull(),
    );
  });

  it("shows a drifted link with its quote at verse level and without word marker", async () => {
    stubFetch({
      verse1: [{ ...sprueche, word: { quotedText: "Anfang", state: "drifted" } }, psalm],
    });
    renderAt("/bible/1/1");
    const rest = await screen.findByRole("group", { name: "Querverweise ohne Wort" });
    expect(within(rest).getAllByRole("link")).toHaveLength(2);
    expect(within(rest).getByTestId("drifted-word")).toHaveTextContent(
      "„Anfang“ (Text geändert, auf Versebene)",
    );
    expect(screen.queryByRole("group", { name: "Querverweise zu „Anfang“" })).toBeNull();
    expect(wordButton(1, "Anfang")).not.toHaveAttribute("data-tsk-link");
  });

  it("marks the mapped variant words on a revision level (empty list = verse level)", async () => {
    const fetchMock = stubFetch({
      revised: true,
      verse1: [
        {
          ...sprueche,
          word: { ...anfang, mappedWords: [{ positionInVerse: 3, basePositionInVerse: 2 }] },
        },
        {
          ...psalm,
          word: {
            startPosition: 4,
            endPosition: 4,
            quotedText: "Gott",
            state: "ok",
            mappedWords: [],
          },
        },
      ],
    });
    renderAt("/bible/1/1");
    await screen.findByText("Ganz");
    await waitFor(() => expect(wordButton(1, "Anfang")).toHaveAttribute("data-tsk-link"));
    // „Gott“ (base position 4) is linked, but the variant maps no word (empty list): verse level, no marker
    expect(wordButton(1, "Gott")).not.toHaveAttribute("data-tsk-link");
    expect(wordButton(1, "im")).not.toHaveAttribute("data-tsk-link");
    expect(wordButton(1, "Ganz")).not.toHaveAttribute("data-tsk-link");
    expect(
      fetchMock.mock.calls.some(([url]) =>
        String(url).endsWith("/chapterCrossReferences/bookId/1/chapterId/1?level=300"),
      ),
    ).toBe(true);
  });
});
