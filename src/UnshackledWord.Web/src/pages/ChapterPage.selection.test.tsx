import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { BibleStructureBook, GetWordsOfChapterResponse } from "@/lib/bible/types";
import type { Note, NoteAnchor } from "@/lib/notes/api";
import { fixtureBooks, fixtureChapterWords } from "@/test/fixtures/bibleStructure";
import { ChapterPage } from "./ChapterPage";

vi.mock("@mdxeditor/editor", () => import("@/test/mdxEditorMock"));

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

/** Verse 2 at level 300: „Jener“ is new, „kam zu“ are based on Elb1871 positions 2 and 3. */
const revisedResponse: GetWordsOfChapterResponse = {
  ...fixtureChapterWords,
  words: [
    ...fixtureChapterWords.words.filter((w) => w.verse !== 2),
    { id: 0, verse: 2, positionInVerse: 1, wordInContext: "Jener", revisionWordId: 501 },
    {
      id: 6,
      verse: 2,
      positionInVerse: 2,
      wordInContext: "kam",
      revisionWordId: 502,
      basePositionInVerse: 2,
    },
    {
      id: 7,
      verse: 2,
      positionInVerse: 3,
      wordInContext: "zu",
      revisionWordId: 503,
      basePositionInVerse: 3,
    },
  ],
  revisedVerses: [{ verse: 2, level: 300, revisionVerseId: 9 }],
};

const note = (
  id: string,
  anchor: Omit<NoteAnchor, "bookId" | "chapter">,
  extra: Partial<Note> = {},
): Note => ({
  id,
  anchor: { bookId: 43, chapter: 3, ...anchor },
  anchorState: "ok",
  bodyMarkdown: `Text ${id}`,
  bodyLinked: `Text ${id}`,
  visibility: "private",
  publicState: "none",
  isOwn: true,
  authorDisplayName: "Ada",
  createdAt: "2026-11-22T10:00:00Z",
  ...extra,
});

function stubFetch({ notes = [] as Note[], revised = false } = {}) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.includes("/meta-bible/structure")) return new Response(JSON.stringify({ books }));
    if (url.includes("/auth/me")) {
      return new Response(JSON.stringify({ userId: 1, email: "a@b.c", displayName: "Ada" }));
    }
    if (url.includes("/revision/levels")) {
      return new Response(JSON.stringify(revised ? levels : []));
    }
    if (url.endsWith("/notes") && init?.method === "POST") {
      return new Response(JSON.stringify(note("new", { kind: "verse", startHebRefId: 43003001 })));
    }
    if (url.includes("/notes/") && init?.method === "PUT") {
      return new Response(JSON.stringify(notes[0]));
    }
    if (url.includes("/notes/chapter/43/3")) return new Response(JSON.stringify({ notes }));
    if (url.includes("chapterWords")) {
      return new Response(
        JSON.stringify(url.includes("level=300") ? revisedResponse : fixtureChapterWords),
      );
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
    {
      initialEntries: [path],
    },
  );
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

const wordButton = (verse: HTMLElement, word: string) =>
  within(verse).getByRole("button", { name: `Grundtext zu „${word}“ anzeigen` });
const bodyOf = (fetchMock: ReturnType<typeof stubFetch>, method: string) =>
  JSON.parse(String(fetchMock.mock.calls.find(([, init]) => init?.method === method)?.[1]?.body));

beforeEach(async () => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("ChapterPage note selection", () => {
  it("drags across words of one verse and saves a words note with Elb1871 positions", async () => {
    const fetchMock = stubFetch();
    const { container } = renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    const verse2 = container.querySelector("#v2") as HTMLElement;
    const user = userEvent.setup();
    await user.pointer([
      { keys: "[MouseLeft>]", target: wordButton(verse2, "kam") },
      { target: wordButton(verse2, "zu") },
      { target: wordButton(verse2, "Jesu") },
      { keys: "[/MouseLeft]", target: wordButton(verse2, "Jesu") },
    ]);
    expect(verse2.querySelectorAll("[data-selected]")).toHaveLength(3);
    // the drag does not open the word detail
    expect(screen.queryByRole("dialog")).toBeNull();
    await user.click(within(verse2).getByRole("button", { name: "Notiz" }));
    // the overlay opens in write mode with the selection as anchor, its words marked in the excerpt
    const overlay = await screen.findByRole("dialog", { name: "Johannes 3,2" });
    expect(
      await within(overlay).findByText("Neue Notiz zu „kam zu Jesu“ · Johannes 3,2"),
    ).toBeInTheDocument();
    expect(
      Array.from(
        within(overlay).getByTestId("notes-excerpt").querySelectorAll(".bg-mark-note"),
      ).map((m) => m.textContent),
    ).toEqual(["kam", "zu", "Jesu"]);
    await user.type(await screen.findByLabelText("Notiztext"), "Gedanke");
    await user.click(screen.getByRole("button", { name: "Speichern" }));
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, i]) => i?.method === "POST")).toBe(true),
    );
    expect(bodyOf(fetchMock, "POST")).toEqual({
      anchor: {
        kind: "words",
        bookId: 43,
        chapter: 3,
        startHebRefId: 43003002,
        startPosition: 2,
        endPosition: 4,
      },
      bodyMarkdown: "Gedanke",
    });
  });

  it("offers a verse-range note for a click and shift-click across verses", async () => {
    const fetchMock = stubFetch();
    const { container } = renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    const user = userEvent.setup();
    await user.click(wordButton(container.querySelector("#v1") as HTMLElement, "Es"));
    await user.keyboard("{Shift>}");
    await user.click(wordButton(container.querySelector("#v3") as HTMLElement, "Jesus"));
    await user.keyboard("{/Shift}");
    await user.click(screen.getByRole("button", { name: "Notiz zu V. 1–3" }));
    expect(await screen.findByText("Neue Notiz zu Johannes 3,1–3")).toBeInTheDocument();
    await user.type(await screen.findByLabelText("Notiztext"), "Bogen");
    await user.click(screen.getByRole("button", { name: "Speichern" }));
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, i]) => i?.method === "POST")).toBe(true),
    );
    expect(bodyOf(fetchMock, "POST").anchor).toEqual({
      kind: "verses",
      bookId: 43,
      chapter: 3,
      startHebRefId: 43003001,
      endHebRefId: 43003003,
    });
  });

  it("turns the last verse note action into a range on shift-click of a second verse's action", async () => {
    stubFetch();
    renderAt("/bible/43/3");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Notiz zum Vers 1" }));
    expect(await screen.findByText("Neue Notiz zu Johannes 3,1")).toBeInTheDocument();
    // the overlay is modal: close it, then shift-click the second verse's action
    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).toBeNull();
    await user.keyboard("{Shift>}");
    await user.click(screen.getByRole("button", { name: "Notiz zum Vers 3" }));
    await user.keyboard("{/Shift}");
    expect(await screen.findByText("Neue Notiz zu Johannes 3,1–3")).toBeInTheDocument();
  });

  it("starts a selection with a long press and ends it with a tap on touch", async () => {
    stubFetch();
    const { container } = renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    const verse2 = container.querySelector("#v2") as HTMLElement;
    const user = userEvent.setup();
    await user.pointer([{ keys: "[TouchA>]", target: wordButton(verse2, "Dieser") }]);
    await new Promise((resolve) => setTimeout(resolve, 600));
    await user.pointer([{ keys: "[/TouchA]", target: wordButton(verse2, "Dieser") }]);
    expect(await within(verse2).findByText("Letztes Wort antippen")).toBeInTheDocument();
    await user.pointer([{ keys: "[TouchA]", target: wordButton(verse2, "kam") }]);
    expect(verse2.querySelectorAll("[data-selected]")).toHaveLength(2);
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(within(verse2).getByRole("button", { name: "Notiz" })).toBeInTheDocument();
  });

  it("places markers at the end word, at the first verse of a range with a bracket, and at the verse when drifted", async () => {
    stubFetch({
      notes: [
        note("w", {
          kind: "words",
          startHebRefId: 43003002,
          startPosition: 2,
          endPosition: 3,
          quotedText: "kam zu",
        }),
        note("r", {
          kind: "verses",
          startHebRefId: 43003001,
          endHebRefId: 43003003,
          startVerse: 1,
          endVerse: 3,
        }),
        note(
          "d",
          {
            kind: "words",
            startHebRefId: 43003003,
            startPosition: 1,
            endPosition: 1,
            quotedText: "Jesu",
          },
          { anchorState: "drifted", isOwn: false, authorDisplayName: "Bert" },
        ),
      ],
    });
    const { container } = renderAt("/bible/43/3");
    const marker = await screen.findByRole("button", { name: "Notiz zu „kam zu“ öffnen" });
    const verse2 = container.querySelector("#v2") as HTMLElement;
    expect(verse2).toContainElement(marker);
    expect(marker.previousElementSibling).toBe(wordButton(verse2, "zu"));
    const verse1 = container.querySelector("#v1") as HTMLElement;
    expect(
      within(verse1).getByRole("button", { name: "Notiz zu Vers 1 öffnen" }),
    ).toBeInTheDocument();
    expect(verse1.querySelector('[data-note-bracket="start"]')).not.toBeNull();
    expect(verse2.querySelector('[data-note-bracket="mid"]')).not.toBeNull();
    const verse3 = container.querySelector("#v3") as HTMLElement;
    expect(verse3.querySelector('[data-note-bracket="end"]')).not.toBeNull();
    // drifted: at the verse number, with the old words as hint in the overlay
    await userEvent.click(within(verse3).getByRole("button", { name: "Notiz zu Vers 3 öffnen" }));
    const overlay = await screen.findByRole("dialog", { name: "Johannes 3,3" });
    expect(
      within(overlay).getByText("Die markierten Wörter haben sich geändert: „Jesu“"),
    ).toBeInTheDocument();
    // "Diese Stelle": every note touching verse 3, i.e. also the range 1–3
    expect(within(overlay).getByRole("button", { name: /Johannes 3,1–3/ })).toBeInTheDocument();
    expect(within(overlay).queryByRole("button", { name: /„kam zu“ · Johannes 3,2/ })).toBeNull();
    await userEvent.click(within(overlay).getByRole("button", { name: /^Kapitel\d/ }));
    expect(
      within(overlay).getByRole("button", { name: /„kam zu“ · Johannes 3,2/ }),
    ).toBeInTheDocument();
    // a foreign drifted note has no re-anchor action
    expect(within(overlay).queryByRole("button", { name: "Neu verankern" })).toBeNull();
  });

  it("keeps the marker on the mapped variant words at a revision level", async () => {
    const fetchMock = stubFetch({
      revised: true,
      notes: [
        note(
          "w",
          {
            kind: "words",
            startHebRefId: 43003002,
            startPosition: 2,
            endPosition: 3,
            quotedText: "kam zu",
          },
          {
            mappedWords: [
              { revisionWordId: 502, positionInVerse: 2, basePositionInVerse: 2 },
              { revisionWordId: 503, positionInVerse: 3, basePositionInVerse: 3 },
            ],
            revisionVerseId: 9,
          },
        ),
      ],
    });
    const { container } = renderAt("/bible/43/3");
    const marker = await screen.findByRole("button", { name: "Notiz zu „kam zu“ öffnen" });
    const verse2 = container.querySelector("#v2") as HTMLElement;
    expect(marker.previousElementSibling).toBe(wordButton(verse2, "zu"));
    expect(
      fetchMock.mock.calls.some(([url]) => String(url).includes("/notes/chapter/43/3?level=300")),
    ).toBe(true);
  });

  it("offers a verse note for a variant-only word and words for mapped variant words", async () => {
    stubFetch({ revised: true });
    const { container } = renderAt("/bible/43/3");
    await screen.findByText("Jener");
    const verse2 = container.querySelector("#v2") as HTMLElement;
    const user = userEvent.setup();
    await user.click(wordButton(verse2, "Jener"));
    await user.keyboard("{Shift>}");
    await user.click(wordButton(verse2, "kam"));
    await user.keyboard("{/Shift}");
    expect(within(verse2).getByRole("button", { name: "Notiz zu V. 2" })).toBeInTheDocument();
    await user.click(wordButton(verse2, "kam"));
    await user.keyboard("{Shift>}");
    await user.click(wordButton(verse2, "zu"));
    await user.keyboard("{/Shift}");
    await user.click(within(verse2).getByRole("button", { name: "Notiz" }));
    expect(await screen.findByText("Neue Notiz zu „kam zu“ · Johannes 3,2")).toBeInTheDocument();
  });

  it("re-anchors an own drifted note to newly selected words", async () => {
    const drifted = note(
      "d",
      {
        kind: "words",
        startHebRefId: 43003002,
        startPosition: 4,
        endPosition: 4,
        quotedText: "Jesus",
      },
      { anchorState: "drifted" },
    );
    const fetchMock = stubFetch({ notes: [drifted] });
    const { container } = renderAt("/bible/43/3");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Notiz zu Vers 2 öffnen" }));
    await user.click(screen.getByRole("button", { name: "Neu verankern" }));
    expect(screen.getByRole("status")).toHaveTextContent(
      "Markiere die neue Stelle für die Notiz zu Johannes 3,2.",
    );
    const verse2 = container.querySelector("#v2") as HTMLElement;
    await user.click(wordButton(verse2, "Jesu"));
    expect(screen.queryByRole("dialog")).toBeNull();
    await user.click(within(verse2).getByRole("button", { name: "Hier verankern" }));
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, i]) => i?.method === "PUT")).toBe(true),
    );
    const [url] = fetchMock.mock.calls.find(([, i]) => i?.method === "PUT") ?? [];
    expect(String(url)).toContain("/notes/d");
    expect(bodyOf(fetchMock, "PUT")).toEqual({
      bodyMarkdown: "Text d",
      anchor: {
        kind: "words",
        bookId: 43,
        chapter: 3,
        startHebRefId: 43003002,
        startPosition: 4,
        endPosition: 4,
      },
    });
    await waitFor(() => expect(screen.queryByText(/Markiere die neue Stelle/)).toBeNull());
  });

  it("closes the notes overlay on Escape", async () => {
    stubFetch({ notes: [note("v", { kind: "verse", startHebRefId: 43003002, startVerse: 2 })] });
    renderAt("/bible/43/3");
    await userEvent.click(await screen.findByRole("button", { name: "Notiz zu Vers 2 öffnen" }));
    expect(await screen.findByRole("dialog", { name: "Johannes 3,2" })).toBeInTheDocument();
    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).toBeNull();
  });
});
