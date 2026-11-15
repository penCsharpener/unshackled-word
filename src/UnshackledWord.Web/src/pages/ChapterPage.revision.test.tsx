import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { BibleStructureBook, GetWordsOfChapterResponse } from "@/lib/bible/types";
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

const revisedResponse: GetWordsOfChapterResponse = {
  ...fixtureChapterWords,
  words: [
    ...fixtureChapterWords.words.filter((w) => w.verse !== 2),
    { id: 0, verse: 2, positionInVerse: 1, wordInContext: "Jener", revisionWordId: 501 },
    { id: 0, verse: 2, positionInVerse: 2, wordInContext: "kam", revisionWordId: 502 },
    { id: 7, verse: 2, positionInVerse: 3, wordInContext: "zu", revisionWordId: 503 },
  ],
  revisedVerses: [{ verse: 2, level: 300, revisionVerseId: 9 }],
};

function stubFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes("/meta-bible/structure")) return new Response(JSON.stringify({ books }));
    if (url.includes("/auth/me")) return new Response("", { status: 401 });
    if (url.includes("/revision/levels")) return new Response(JSON.stringify(levels));
    if (url.includes("chapterWords")) {
      return new Response(
        JSON.stringify(url.includes("level=300") ? revisedResponse : fixtureChapterWords),
      );
    }
    if (url.includes("/revision/wordDetail/wordId/502")) {
      return new Response(
        JSON.stringify({
          wordId: 502,
          wordInContext: "kam",
          positionInVerse: 2,
          readings: [],
          revision: { level: 300, change: "changed", baseWord: "ging" },
        }),
      );
    }
    if (url.includes("/revision/verseMapping/9")) {
      return new Response(
        JSON.stringify({
          revisionVerseId: 9,
          level: 300,
          version: 1,
          bibleBookId: 43,
          chapter: 3,
          verse: 2,
          language: "greek",
          germanWords: [{ id: 501, positionInVerse: 1, text: "Jener", change: "new" }],
          originalWords: [{ id: 10, position: 1, original: "ἐκεῖνος" }],
          links: [],
        }),
      );
    }
    return new Response("{}", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const wordCalls = (fetchMock: ReturnType<typeof stubFetch>) =>
  fetchMock.mock.calls
    .map(([url]) => String(url))
    .filter((url) => url.includes("chapterWords/bookId/43/chapterId/3"));

function renderPage(entry = "/bible/43/3") {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [{ path: "/bible/:bookId/:chapter", element: <ChapterPage /> }],
    {
      initialEntries: [entry],
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

beforeEach(async () => {
  localStorage.clear();
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  window.HTMLElement.prototype.showPopover ??= vi.fn();
  await i18n.changeLanguage("de");
});
afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("ChapterPage reader level", () => {
  it("requests the default level and marks the revised verse", async () => {
    const fetchMock = stubFetch();
    const { container } = renderPage();
    await screen.findByText("Jener");
    expect(wordCalls(fetchMock)).toHaveLength(1);
    expect(wordCalls(fetchMock)[0]).toContain("?level=300");
    const revised = container.querySelector("#v2") as HTMLElement;
    expect(revised.className).toContain("before:bg-accent");
    expect(revised.getAttribute("title")).toBe("revidiert: Revision");
    expect(revised.getAttribute("aria-label")).toBe("revidiert: Revision");
    const plain = container.querySelector("#v1") as HTMLElement;
    expect(plain.className).not.toContain("before:bg-accent");
    expect(plain.getAttribute("title")).toBeNull();
  });

  it("uses a stored known level and sends no level parameter for the base text", async () => {
    localStorage.setItem("uw.readerLevel", "200");
    const fetchMock = stubFetch();
    const { container } = renderPage();
    await screen.findByText("Dieser");
    expect(wordCalls(fetchMock)[0]).not.toContain("level=");
    expect(container.querySelector(".border-accent")).toBeNull();
  });

  it("ignores a stored level that is not a preset", async () => {
    localStorage.setItem("uw.readerLevel", "777");
    const fetchMock = stubFetch();
    renderPage();
    await screen.findByText("Jener");
    expect(wordCalls(fetchMock)[0]).toContain("level=300");
  });

  it("falls back to the default preset when localStorage throws", async () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    const fetchMock = stubFetch();
    renderPage();
    await screen.findByText("Jener");
    expect(wordCalls(fetchMock)[0]).toContain("level=300");
    fireEvent.change(screen.getByRole("slider", { name: "Lesefassung" }), {
      target: { value: "0" },
    });
    await screen.findByText("Dieser");
    expect(wordCalls(fetchMock)).toHaveLength(2);
  });

  it("refetches with the new level when the slider moves and stores it", async () => {
    const fetchMock = stubFetch();
    const { container } = renderPage();
    await screen.findByText("Jener");
    const slider = screen.getByRole("slider", { name: "Lesefassung" });
    expect(slider.getAttribute("aria-valuetext")).toBe("Revision");
    fireEvent.change(slider, { target: { value: "0" } });
    await screen.findByText("Dieser");
    expect(wordCalls(fetchMock)).toHaveLength(2);
    expect(wordCalls(fetchMock)[1]).not.toContain("level=");
    expect(container.querySelector(".border-accent")).toBeNull();
    expect(localStorage.getItem("uw.readerLevel")).toBe("200");
    expect(screen.getByRole("slider", { name: "Lesefassung" }).getAttribute("aria-valuetext")).toBe(
      "Elb 1871",
    );
  });

  it("opens the word popover for words of a revised verse that have no base word", async () => {
    stubFetch();
    renderPage();
    await screen.findByText("Jener");
    await userEvent.click(screen.getByText("kam"));
    expect(await screen.findByRole("dialog")).toBeInTheDocument();
  });

  it("?revise=<level> switches the reader to that level and opens the revision mapping of ?map=<verse>", async () => {
    localStorage.setItem("uw.readerLevel", "200");
    const fetchMock = stubFetch();
    renderPage("/bible/43/3?map=2&revise=300");
    expect(await screen.findByRole("region", { name: /Zuordnung/ })).toBeInTheDocument();
    expect(wordCalls(fetchMock).at(-1)).toContain("level=300");
    expect(
      fetchMock.mock.calls.some(([url]) => String(url).includes("/revision/verseMapping/9")),
    ).toBe(true);
    expect(screen.getByRole("button", { name: /Jener.*prüfen/ })).toBeInTheDocument();
  });
});
