import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { BibleStructureBook, GetVerseMappingResponse } from "@/lib/bible/types";
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

function Probe() {
  return <output data-testid="loc">{useLocation().pathname}</output>;
}

function stubFetch(chapterStatus = 200) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes("/meta-bible/structure")) {
      return new Response(JSON.stringify({ books }), { status: 200 });
    }
    return new Response(JSON.stringify(fixtureChapterWords), { status: chapterStatus });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const chapterCalls = (fetchMock: ReturnType<typeof stubFetch>) =>
  fetchMock.mock.calls.filter(([url]) => String(url).includes("chapterWords"));

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      {
        path: "/bible/:bookId/:chapter",
        element: (
          <>
            <ChapterPage />
            <Probe />
          </>
        ),
      },
    ],
    { initialEntries: [path] },
  );
  return render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(async () => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  window.HTMLElement.prototype.showPopover ??= vi.fn();
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("ChapterPage", () => {
  it("renders heading and verses", async () => {
    stubFetch();
    const { container } = renderAt("/bible/43/3");
    expect(
      await screen.findByRole("heading", { level: 1, name: "Johannes 3" }),
    ).toBeInTheDocument();
    await screen.findByText("Dieser");
    expect(container.querySelector("#v1")).not.toBeNull();
    expect(container.querySelector("#v3")).not.toBeNull();
    expect(container.querySelector("article")?.getAttribute("lang")).toBe("de");
  });

  it("highlights words sharing a Strong's number on hover", async () => {
    stubFetch();
    renderAt("/bible/43/3");
    const [war] = await screen.findAllByText("war");
    await userEvent.hover(war as HTMLElement);
    const same = screen.getAllByText("war");
    expect(same.every((el) => el.className.includes("bg-mark-same"))).toBe(true);
    expect(screen.getByText("ist").className).toContain("bg-mark-related");
    expect(screen.getByText("Mensch").className).not.toContain("bg-mark");
    await userEvent.unhover(war as HTMLElement);
    expect(screen.getByText("ist").className).not.toContain("bg-mark");
  });

  it("links to previous and next chapter", async () => {
    stubFetch();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    const prev = screen.getAllByRole("link", { name: /Vorheriges Kapitel/ });
    const next = screen.getAllByRole("link", { name: /Nächstes Kapitel/ });
    expect(prev[0]?.getAttribute("href")).toBe("/bible/43/2");
    expect(next[0]?.getAttribute("href")).toBe("/bible/43/4");
  });

  it("disables previous at Genesis 1", async () => {
    stubFetch();
    renderAt("/bible/1/1");
    await screen.findByRole("heading", { level: 1, name: "1. Mose 1" });
    expect(screen.queryByRole("link", { name: /Vorheriges Kapitel/ })).toBeNull();
    expect(document.querySelector('span[aria-disabled="true"]')).not.toBeNull();
  });

  it.each(["/bible/1/51", "/bible/abc/1"])(
    "shows not-found for %s without fetching",
    async (path) => {
      const fetchMock = stubFetch();
      renderAt(path);
      expect(await screen.findByText("Dieses Kapitel gibt es nicht.")).toBeInTheDocument();
      expect(chapterCalls(fetchMock)).toHaveLength(0);
    },
  );

  it("shows an error and retries", async () => {
    const fetchMock = stubFetch(500);
    renderAt("/bible/43/3");
    expect(await screen.findByText("Das Kapitel konnte nicht geladen werden.")).toBeInTheDocument();
    expect(chapterCalls(fetchMock)).toHaveLength(1);
    await userEvent.click(screen.getByRole("button", { name: "Erneut versuchen" }));
    await waitFor(() => expect(chapterCalls(fetchMock).length).toBeGreaterThanOrEqual(2));
  });

  it("navigates with ArrowRight, but not from an input", async () => {
    stubFetch();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    const input = document.createElement("input");
    document.body.appendChild(input);
    input.focus();
    await userEvent.keyboard("{ArrowRight}");
    expect(screen.getByTestId("loc").textContent).toBe("/bible/43/3");
    input.remove();
    await userEvent.keyboard("{ArrowRight}");
    await waitFor(() => expect(screen.getByTestId("loc").textContent).toBe("/bible/43/4"));
  });

  describe("hover-intent prefetch", () => {
    afterEach(() => vi.useRealTimers());

    async function setup() {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      const spy = vi.spyOn(QueryClient.prototype, "prefetchQuery");
      vi.useFakeTimers();
      return spy;
    }
    const detailCalls = (spy: ReturnType<typeof setup> extends Promise<infer S> ? S : never) =>
      spy.mock.calls.filter(([options]) => options.queryKey[1] === "wordDetail");

    it("prefetches once after 200 ms of hovering", async () => {
      const spy = await setup();
      const mensch = screen.getByText("Mensch");
      fireEvent.mouseEnter(mensch);
      vi.advanceTimersByTime(199);
      expect(detailCalls(spy)).toHaveLength(0);
      vi.advanceTimersByTime(1);
      expect(detailCalls(spy)).toHaveLength(1);
      spy.mockRestore();
    });

    it("does not prefetch on a quick pass-over or for unmapped words", async () => {
      const spy = await setup();
      const mensch = screen.getByText("Mensch");
      fireEvent.mouseEnter(mensch);
      vi.advanceTimersByTime(100);
      fireEvent.mouseLeave(mensch);
      fireEvent.mouseEnter(screen.getByText("Es"));
      vi.advanceTimersByTime(500);
      expect(detailCalls(spy)).toHaveLength(0);
      spy.mockRestore();
    });
  });

  describe("word detail pop-up", () => {
    const trigger = (name: string) =>
      screen.getByRole("button", { name: `Grundtext zu „${name}“ anzeigen` });

    it("opens for a click on a word", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      await userEvent.click(screen.getByText("Mensch"));
      const dialog = await screen.findByRole("dialog", { name: "Grundtext zu „Mensch“" });
      expect(await within(dialog).findByText("G444")).toBeInTheDocument();
    });

    it("opens with Enter and shows the reading", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      const [war] = screen.getAllByText("war");
      (war as HTMLElement).focus();
      await userEvent.keyboard("{Enter}");
      const dialog = await screen.findByRole("dialog");
      expect(await within(dialog).findByText("ἦν")).toBeInTheDocument();
      expect(within(dialog).getByText("V-IAI-3S")).toBeInTheDocument();
      expect(war).toHaveAttribute("aria-expanded", "true");
    });

    it("closes with Escape and returns focus", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      const [war] = screen.getAllByText("war");
      await userEvent.click(war as HTMLElement);
      await screen.findByRole("dialog");
      await userEvent.keyboard("{Escape}");
      expect(screen.queryByRole("dialog")).toBeNull();
      expect(war).toHaveFocus();
    });

    it("toggles on the same word and switches on another", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      await userEvent.click(screen.getByText("Mensch"));
      await screen.findByRole("dialog", { name: "Grundtext zu „Mensch“" });
      await userEvent.click(trigger("Jesu"));
      await screen.findByRole("dialog", { name: "Grundtext zu „Jesu“" });
      await userEvent.click(trigger("Jesu"));
      expect(screen.queryByRole("dialog")).toBeNull();
    });

    it("closes on an outside press without moving focus", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      const [war] = screen.getAllByText("war");
      await userEvent.click(war as HTMLElement);
      await screen.findByRole("dialog");
      fireEvent.pointerDown(document.body);
      expect(screen.queryByRole("dialog")).toBeNull();
      expect(war).not.toHaveFocus();
    });

    it("ignores ArrowRight while open and navigates after closing", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      await userEvent.click(screen.getByText("Mensch"));
      await screen.findByRole("dialog");
      await userEvent.keyboard("{ArrowRight}");
      expect(screen.getByTestId("loc").textContent).toBe("/bible/43/3");
      await userEvent.keyboard("{Escape}");
      await userEvent.keyboard("{ArrowRight}");
      await waitFor(() => expect(screen.getByTestId("loc").textContent).toBe("/bible/43/4"));
    });

    it("keeps the selected group highlighted while open", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      const [war] = screen.getAllByText("war");
      await userEvent.click(war as HTMLElement);
      await screen.findByRole("dialog");
      fireEvent.mouseLeave(war as HTMLElement);
      fireEvent.blur(war as HTMLElement);
      const second = screen.getAllByText("war")[1] as HTMLElement;
      expect(second.className).toContain("bg-mark-same");
    });

    it("shows the empty state for an unmapped word", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      await userEvent.click(screen.getByText("Es"));
      expect(
        await screen.findByText("Dieses Wort ist noch keinem Grundtextwort zugeordnet."),
      ).toBeInTheDocument();
      expect(trigger("Es")).toHaveAttribute("aria-expanded", "true");
    });

    it("shows the mapping badge and underlines the words translated together", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      await userEvent.click(screen.getByText("ein"));
      const dialog = await screen.findByRole("dialog", { name: "Grundtext zu „ein“" });
      expect(
        await within(dialog).findByText(/der Übersetzer hat es zu „Mensch“ ergänzt/),
      ).toBeInTheDocument();
      expect(within(dialog).getByText("Manuell")).toBeInTheDocument();
      expect(trigger("Mensch").className).toContain("underline");
      expect(trigger("ein").className).toContain("underline");
      expect(trigger("Es").className).not.toContain("underline");
      await userEvent.keyboard("{Escape}");
      expect(trigger("Mensch").className).not.toContain("underline");
    });

    it("keeps the hover highlight next to the phrase underline", async () => {
      stubFetch();
      renderAt("/bible/43/3");
      await screen.findByText("Dieser");
      await userEvent.click(screen.getByText("Mensch"));
      const dialog = await screen.findByRole("dialog", { name: "Grundtext zu „Mensch“" });
      expect(await within(dialog).findByText("Gemeinsam übersetzt:")).toBeInTheDocument();
      expect(trigger("ein").className).toContain("underline");
      expect(trigger("Mensch").className).toContain("bg-mark-same");
    });
  });
});

describe("mapping editor", () => {
  const verseMapping: GetVerseMappingResponse = {
    bibleBookId: 43,
    chapter: 3,
    verse: 1,
    language: "greek",
    editingEnabled: true,
    germanWords: [
      { id: 1, positionInVerse: 1, text: "Es" },
      { id: 2, positionInVerse: 2, text: "war" },
      { id: 3, positionInVerse: 3, text: "ein" },
      { id: 4, positionInVerse: 4, text: "Mensch" },
    ],
    originalWords: [
      { id: 100, position: 1, original: "ἦν", strongs: "G1510" },
      { id: 101, position: 2, original: "ἄνθρωπος", strongs: "G0444" },
    ],
    links: [
      { elbWordId: 2, stepWordId: 100, isAddedWord: false },
      { elbWordId: 4, stepWordId: 101, isAddedWord: false },
    ],
  };

  function stubMapping(options: { editingEnabled?: boolean; putStatus?: number } = {}) {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.includes("/meta-bible/structure")) {
        return new Response(JSON.stringify({ books }), { status: 200 });
      }
      if (url.includes("/verseMapping/")) {
        const body = { ...verseMapping, editingEnabled: options.editingEnabled ?? true };
        if (init?.method === "PUT") {
          const status = options.putStatus ?? 200;
          return new Response(JSON.stringify(status === 200 ? body : { errors: {} }), { status });
        }
        const verse = Number(url.split("/verseId/")[1]);
        return new Response(JSON.stringify({ ...body, verse }), { status: 200 });
      }
      return new Response(JSON.stringify(fixtureChapterWords), { status: 200 });
    });
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
  }

  const puts = (fetchMock: ReturnType<typeof stubMapping>) =>
    fetchMock.mock.calls.filter(([, init]) => init?.method === "PUT");
  const trigger = (verse: number) =>
    screen.getByRole("button", { name: new RegExp(`Vers ${verse}\\b`) });
  /** Ctrl+click on the eye jumps straight to the mapping editor. */
  const openAlign = (verse: number) => fireEvent.click(trigger(verse), { ctrlKey: true });
  const editorNode = (key: string) =>
    document.querySelector(`[data-mapping-editor] [data-node="${key}"]`) as HTMLElement;

  it("opens the editor inline below the verse while the German text stays above", async () => {
    stubMapping();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    openAlign(1);
    const editor = await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    const verse = document.querySelector("#v1") as HTMLElement;
    expect(verse).toContainElement(editor);
    expect(verse.querySelector("p")).toHaveTextContent("Es war ein Mensch");
    expect(verse.querySelector("p")?.compareDocumentPosition(editor)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
    expect(trigger(1)).toHaveAttribute("aria-expanded", "true");
  });

  it("keeps one verse open at a time", async () => {
    stubMapping();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    openAlign(1);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    openAlign(2);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,2" });
    expect(screen.queryByRole("region", { name: "Zuordnung Johannes 3,1" })).toBeNull();
  });

  it("eye icon cycles original text, mapping, closed", async () => {
    stubMapping();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    fireEvent.click(trigger(1));
    await screen.findByRole("region", { name: "Grundtext Johannes 3,1" });
    expect(screen.queryByRole("region", { name: "Zuordnung Johannes 3,1" })).toBeNull();
    fireEvent.click(trigger(1));
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    fireEvent.click(trigger(1));
    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Zuordnung Johannes 3,1" })).toBeNull(),
    );
  });

  it("closes with Escape, returns focus to the trigger and does not navigate", async () => {
    stubMapping();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    openAlign(1);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    await userEvent.keyboard("{ArrowRight}");
    expect(screen.getByTestId("loc").textContent).toBe("/bible/43/3");
    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("region", { name: "Zuordnung Johannes 3,1" })).toBeNull();
    expect(trigger(1)).toHaveFocus();
    await userEvent.keyboard("{ArrowRight}");
    await waitFor(() => expect(screen.getByTestId("loc").textContent).toBe("/bible/43/4"));
  });

  it("still highlights words on hover while the editor is open", async () => {
    stubMapping();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    openAlign(1);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    await userEvent.hover(screen.getAllByText("war")[0] as HTMLElement);
    expect(screen.getAllByText("war")[0]?.className).toContain("bg-mark-same");
  });

  it("saves the whole verse mapping, closes and refreshes the chapter words", async () => {
    const fetchMock = stubMapping();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    openAlign(1);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    const before = chapterCalls(fetchMock).length;
    await userEvent.click(editorNode("g1"));
    await userEvent.click(editorNode("o100"));
    await userEvent.click(screen.getByRole("button", { name: "Speichern (1)" }));
    await waitFor(() =>
      expect(screen.queryByRole("region", { name: "Zuordnung Johannes 3,1" })).toBeNull(),
    );
    const [url, init] = puts(fetchMock)[0] ?? [];
    expect(String(url)).toContain("/elberfelder/verseMapping/bookId/43/chapterId/3/verseId/1");
    expect(JSON.parse(String(init?.body))).toEqual({
      links: [
        { elbWordId: 1, stepWordId: 100, isAddedWord: false },
        { elbWordId: 2, stepWordId: 100, isAddedWord: false },
        { elbWordId: 4, stepWordId: 101, isAddedWord: false },
      ],
    });
    await waitFor(() => expect(chapterCalls(fetchMock).length).toBeGreaterThan(before));
    expect(trigger(1)).toHaveFocus();
  });

  it("closes on Save without edits and sends nothing", async () => {
    const fetchMock = stubMapping();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    openAlign(1);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    expect(screen.queryByRole("region", { name: "Zuordnung Johannes 3,1" })).toBeNull();
    expect(puts(fetchMock)).toHaveLength(0);
  });

  it("keeps the editor open and shows an error when saving fails", async () => {
    stubMapping({ putStatus: 400 });
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    openAlign(1);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    await userEvent.click(editorNode("g1"));
    await userEvent.click(editorNode("o100"));
    await userEvent.click(screen.getByRole("button", { name: "Speichern (1)" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("ungültig");
    expect(screen.getByRole("region", { name: "Zuordnung Johannes 3,1" })).toBeInTheDocument();
  });

  it("is read-only when editing is disabled", async () => {
    stubMapping({ editingEnabled: false });
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    openAlign(1);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    expect(screen.getByText(/noch nicht freigeschaltet/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Speichern/ })).toBeNull();
  });

  it("opens from the word pop-up", async () => {
    stubMapping();
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    await userEvent.click(screen.getByText("Mensch"));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(
      await within(dialog).findByRole("button", { name: "Zuordnung bearbeiten" }),
    );
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
  });
});
