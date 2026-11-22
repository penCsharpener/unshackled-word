import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { BibleStructureBook } from "@/lib/bible/types";
import type { Note } from "@/lib/notes/api";
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
    if (url.includes("/notes/chapter/43/3")) return new Response(JSON.stringify({ notes }));
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
afterEach(() => vi.unstubAllGlobals());

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

  it("opens the notes sheet at the clicked note", async () => {
    stubFetch(true, [note("c", undefined, "Kapitel", true), note("a", 2, "Erste", false)]);
    renderAt("/bible/43/3");
    await userEvent.click(await screen.findByRole("button", { name: "Notiz zu Vers 2 öffnen" }));
    const panel = screen.getByRole("region", { name: "Notizen zu Johannes 3" });
    expect(await within(panel).findByTestId("note-body")).toHaveTextContent("Erste");
    expect(
      within(panel).getByRole("button", { name: /Johannes 3,2/, expanded: true }),
    ).toBeVisible();
    await userEvent.click(within(panel).getByRole("button", { name: "Schließen" }));
    expect(screen.queryByRole("region", { name: "Notizen zu Johannes 3" })).toBeNull();
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
});
