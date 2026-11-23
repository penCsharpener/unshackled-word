import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { SIDE_PANEL_KEY } from "@/lib/bible/sidePanel";
import type { BibleStructureBook } from "@/lib/bible/types";
import type { Note } from "@/lib/notes/api";
import { SHOW_OTHERS_KEY } from "@/lib/notes/showOthers";
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

const note = (id: string, verse: number | undefined, body: string, isOwn: boolean): Note => ({
  id,
  anchor:
    verse === undefined
      ? { kind: "chapter", bookId: 43, chapter: 3 }
      : {
          kind: "verse",
          bookId: 43,
          chapter: 3,
          startHebRefId: 43003000 + verse,
          startVerse: verse,
        },
  anchorState: "ok",
  bodyMarkdown: body,
  bodyLinked: body,
  visibility: "private",
  publicState: "none",
  isOwn,
  authorDisplayName: isOwn ? "Ada" : "Bert",
  createdAt: "2026-11-22T10:00:00Z",
});

function stubFetch(signedIn: boolean, notes: Note[]) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes("/meta-bible/structure")) return new Response(JSON.stringify({ books }));
    if (url.includes("/auth/me")) {
      return signedIn
        ? new Response(JSON.stringify({ userId: 1, email: "a@b.c", displayName: "Ada" }))
        : new Response("", { status: 401 });
    }
    if (url.includes("/revision/levels")) return new Response(JSON.stringify([]));
    if (url.includes("/chapterCrossReferences/") || url.includes("/chapterOriginals/")) {
      return new Response(JSON.stringify({ verses: [] }));
    }
    if (url.includes("/notes/chapter/43/3")) {
      const others = new URL(url, "http://x").searchParams.get("others") !== "false";
      return new Response(JSON.stringify({ notes: others ? notes : notes.filter((n) => n.isOwn) }));
    }
    return new Response(JSON.stringify(fixtureChapterWords));
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

beforeEach(async () => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  await i18n.changeLanguage("de");
});
afterEach(() => {
  vi.unstubAllGlobals();
  localStorage.removeItem(SHOW_OTHERS_KEY);
  localStorage.removeItem(SIDE_PANEL_KEY);
});

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

describe("ChapterPage notes", () => {
  it("shows markers at the heading and the verse numbers with notes", async () => {
    stubFetch(true, [
      note("c", undefined, "Kapitel", true),
      note("a", 2, "Erste", true),
      note("b", 2, "Zweite", false),
    ]);
    const { container } = renderAt("/bible/43/3");
    expect(
      await screen.findByRole("button", { name: "Notiz zu Johannes 3 öffnen" }),
    ).toBeInTheDocument();
    const verse2 = container.querySelector("#v2") as HTMLElement;
    const marker = within(verse2).getByRole("button", { name: "2 Notizen zu Vers 2 öffnen" });
    expect(marker).toHaveTextContent("2");
    expect(marker.className).toContain("text-accent");
    expect(
      within(container.querySelector("#v1") as HTMLElement).queryByRole("button", {
        name: /Notiz.*öffnen/,
      }),
    ).toBeNull();
    // the heading keeps its own accessible name
    expect(screen.getByRole("heading", { level: 1, name: "Johannes 3" })).toBeInTheDocument();
  });

  it("opens the notes overlay focused on the clicked marker's note", async () => {
    stubFetch(true, [
      note("c", undefined, "Kapitel", true),
      note("a", 2, "Erste", false),
      note("b", 2, "Zweite", true),
    ]);
    renderAt("/bible/43/3");
    await userEvent.click(
      await screen.findByRole("button", { name: "2 Notizen zu Vers 2 öffnen" }),
    );
    const overlay = await screen.findByRole("dialog", { name: "Johannes 3,2" });
    expect(await within(overlay).findByTestId("note-body")).toHaveTextContent("Erste");
    expect(within(overlay).getByTestId("notes-excerpt")).toHaveTextContent(/^Dieser kam zu Jesu/);
    const rows = within(within(overlay).getByRole("list", { name: "Notizen" })).getAllByRole(
      "button",
    );
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveAttribute("aria-current", "true");
    await userEvent.click(within(overlay).getByRole("button", { name: "Schließen" }));
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("opens the chapter's notes on the chapter filter from the chapter action", async () => {
    stubFetch(true, [note("c", undefined, "Kapitel", true), note("a", 2, "Erste", true)]);
    renderAt("/bible/43/3");
    await screen.findByRole("button", { name: "Notiz zu Vers 2 öffnen" });
    await userEvent.click(screen.getByRole("button", { name: "Notiz zum Kapitel" }));
    const overlay = await screen.findByRole("dialog", { name: "Johannes 3" });
    expect(await within(overlay).findByText("Neue Notiz zu Johannes 3 (Kapitel)")).toBeVisible();
    expect(within(overlay).getByRole("button", { name: /^Kapitel\d/ })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(within(overlay).queryByTestId("notes-excerpt")).toBeNull();
  });

  it("opens the overlay for the chapter with N", async () => {
    stubFetch(true, [note("c", undefined, "Kapitel", true), note("a", 2, "Erste", true)]);
    renderAt("/bible/43/3");
    await screen.findByRole("button", { name: "Notiz zu Vers 2 öffnen" });
    await userEvent.keyboard("n");
    const overlay = await screen.findByRole("dialog", { name: "Johannes 3" });
    expect(within(overlay).getByRole("button", { name: /^Kapitel\d/ })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(
      within(within(overlay).getByRole("list", { name: "Notizen" })).getAllByRole("button"),
    ).toHaveLength(2);
    expect(await within(overlay).findByTestId("note-body")).toHaveTextContent("Kapitel");
  });

  it("cycles G through TSK, Grundtext and off without a notes step", async () => {
    stubWideScreen();
    localStorage.setItem(SIDE_PANEL_KEY, "tsk");
    stubFetch(true, []);
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    const pressed = () =>
      within(screen.getByRole("group", { name: "Inhalt der Randspalte" }))
        .getAllByRole("button")
        .filter((b) => b.getAttribute("aria-pressed") === "true")
        .map((b) => b.getAttribute("aria-label"));
    expect(
      within(screen.getByRole("group", { name: "Inhalt der Randspalte" })).getAllByRole("button"),
    ).toHaveLength(3);
    expect(pressed()).toEqual(["Querverweise (TSK) zeigen (G)"]);
    await userEvent.keyboard("g");
    expect(pressed()).toEqual(["Grundtext zeigen (G)"]);
    await userEvent.keyboard("g");
    expect(pressed()).toEqual(["Randspalte ausblenden (G)"]);
    await userEvent.keyboard("g");
    expect(pressed()).toEqual(["Querverweise (TSK) zeigen (G)"]);
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("opens the editor for a verse note when signed in", async () => {
    stubFetch(true, []);
    renderAt("/bible/43/3");
    await userEvent.click(await screen.findByRole("button", { name: "Notiz zum Vers 1" }));
    expect(await screen.findByText("Neue Notiz zu Johannes 3,1")).toBeInTheDocument();
    expect(await screen.findByLabelText("Notiztext")).toBeInTheDocument();
  });

  it("asks signed-out readers to sign in for a chapter note", async () => {
    stubFetch(false, []);
    renderAt("/bible/43/3");
    await screen.findByText("Dieser");
    await userEvent.click(screen.getByRole("button", { name: "Notiz zum Kapitel" }));
    expect(screen.getByText("Melde dich an, um Notizen zu schreiben.")).toBeInTheDocument();
    expect(screen.queryByLabelText("Notiztext")).toBeNull();
  });

  it("opens the notes overlay at the note named by ?note= and scrolls to its verse", async () => {
    stubFetch(true, [
      note("c", undefined, "Kapitel", true),
      note("a", 2, "Erste", true),
      note("b", 3, "Zweite", false),
    ]);
    renderAt("/bible/43/3?v=3&note=b#v3");
    const overlay = await screen.findByRole("dialog", { name: "Johannes 3,3" });
    expect(await within(overlay).findByTestId("note-body")).toHaveTextContent("Zweite");
    expect(within(overlay).getAllByTestId("note-body")).toHaveLength(1);
    expect(within(overlay).getByRole("button", { name: /Johannes 3,3/ })).toHaveAttribute(
      "aria-current",
      "true",
    );
    expect(window.HTMLElement.prototype.scrollIntoView).toHaveBeenCalled();
  });

  it("keeps the overlay closed when the ?note= id is not in the chapter", async () => {
    stubFetch(true, [note("a", 2, "Erste", true)]);
    renderAt("/bible/43/3?note=gone");
    await screen.findByRole("button", { name: "Notiz zu Vers 2 öffnen" });
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("hides other people's notes with the toggle and remembers the choice", async () => {
    const fetchMock = stubFetch(true, [
      note("a", 2, "Eigene", true),
      note("b", 3, "Fremde", false),
    ]);
    const { container, unmount } = renderAt("/bible/43/3");
    const verse3 = () => container.querySelector("#v3") as HTMLElement;
    const other = await screen.findByRole("button", { name: "Notiz zu Vers 3 öffnen" });
    expect(verse3()).toContainElement(other);
    expect(other.className).toContain("text-note-other");
    // the toggle lives in the notes overlay
    expect(screen.queryByRole("switch", { name: "Notizen anderer anzeigen" })).toBeNull();
    await userEvent.keyboard("n");
    const toggle = await screen.findByRole("switch", { name: "Notizen anderer anzeigen" });
    expect(toggle).toHaveAttribute("aria-checked", "true");

    await userEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-checked", "false");
    await userEvent.click(screen.getByRole("button", { name: "Schließen" }));
    await waitFor(() =>
      expect(within(verse3()).queryByRole("button", { name: /Notiz.*öffnen/ })).toBeNull(),
    );
    expect(screen.getByRole("button", { name: "Notiz zu Vers 2 öffnen" })).toBeInTheDocument();
    const chapterUrls = () =>
      fetchMock.mock.calls
        .map(([url]) => String(url))
        .filter((url) => url.includes("/notes/chapter/"));
    expect(chapterUrls().at(-1)).toContain("others=false");
    expect(localStorage.getItem(SHOW_OTHERS_KEY)).toBe("false");

    unmount();
    fetchMock.mockClear();
    renderAt("/bible/43/3");
    expect(
      await screen.findByRole("button", { name: "Notiz zu Vers 2 öffnen" }),
    ).toBeInTheDocument();
    await userEvent.keyboard("n");
    expect(await screen.findByRole("switch", { name: "Notizen anderer anzeigen" })).toHaveAttribute(
      "aria-checked",
      "false",
    );
    expect(chapterUrls().every((url) => url.includes("others=false"))).toBe(true);
  });
});
