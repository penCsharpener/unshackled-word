import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { BibleStructureBook } from "@/lib/bible/types";
import type { Note } from "@/lib/notes/api";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { MyNotesPage } from "./MyNotesPage";

const romans: BibleStructureBook = {
  id: 45,
  testament: "nt",
  nameDe: "Römer",
  nameEn: "Romans",
  abbreviationDe: "Röm",
  abbreviationEn: "Rom",
  versesPerChapter: Array<number>(16).fill(30),
};
const books = [...fixtureBooks, romans];

const note = (id: string, overrides: Partial<Note> = {}, anchor: Partial<Note["anchor"]> = {}) =>
  ({
    id,
    anchor: {
      kind: "verse",
      bookId: 45,
      chapter: 8,
      startHebRefId: 45008028,
      startVerse: 28,
      ...anchor,
    },
    anchorState: "ok",
    bodyMarkdown: `**${id}** Text`,
    bodyLinked: `**${id}** Text mit [Joh 3,16](/bible/43/3?v=16#v16)`,
    visibility: "private",
    publicState: "none",
    isOwn: true,
    authorDisplayName: "Ada",
    createdAt: "2026-11-22T10:00:00Z",
    ...overrides,
  }) as Note;

type MineCall = URLSearchParams;

function stubFetch(
  signedIn: boolean,
  respond: (query: MineCall) => { total: number; notes: Note[] },
  mineCalls: MineCall[] = [],
) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.includes("/meta-bible/structure")) return new Response(JSON.stringify({ books }));
    if (url.includes("/auth/me")) {
      return signedIn
        ? new Response(JSON.stringify({ userId: 1, email: "a@b.c", displayName: "Ada" }))
        : new Response("", { status: 401 });
    }
    if (url.includes("/notes/mine/export")) {
      return new Response('{"format":"unshackled-word.notes"}', {
        headers: { "Content-Disposition": 'attachment; filename="notes-20261122-180000.json"' },
      });
    }
    if (url.includes("/notes/mine")) {
      const query = new URL(url, "http://x").searchParams;
      mineCalls.push(query);
      return new Response(JSON.stringify(respond(query)));
    }
    return new Response("{}");
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      { path: "/notes", element: <MyNotesPage /> },
      { path: "/login", element: <p>Anmeldeseite</p> },
    ],
    { initialEntries: [path] },
  );
  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
  return router;
}

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("MyNotesPage", () => {
  it("sends signed-out visitors to the sign-in page", async () => {
    stubFetch(false, () => ({ total: 0, notes: [] }));
    renderAt("/notes");
    expect(await screen.findByText("Anmeldeseite")).toBeInTheDocument();
  });

  it("lists notes with reference, badge, excerpt and a link that opens the note in the reader", async () => {
    stubFetch(true, () => ({
      total: 4,
      notes: [
        note("verse"),
        note(
          "words",
          { visibility: "public", publicState: "pending" },
          { kind: "words", quotedText: "denen, die Gott lieben", startPosition: 4, endPosition: 8 },
        ),
        note("chapter", { visibility: "internal" }, { kind: "chapter", startHebRefId: undefined }),
        note(
          "range",
          { visibility: "public", publicState: "approved" },
          { kind: "verses", startHebRefId: 45008028, endHebRefId: 45008030, endVerse: 30 },
        ),
      ],
    }));
    renderAt("/notes");

    const verse = (await screen.findByText("Römer 8,28")).closest("li") as HTMLElement;
    expect(within(verse).getByRole("link", { name: "Römer 8,28" })).toHaveAttribute(
      "href",
      "/bible/45/8?v=28&note=verse#v28",
    );
    expect(within(verse).getByText("Privat")).toBeInTheDocument();
    // excerpt: plain text, link reduced to its text, no Markdown markers
    expect(within(verse).getByText("verse Text mit Joh 3,16")).toBeInTheDocument();

    const words = screen.getByRole("link", { name: "„denen, die Gott lieben“ · Römer 8,28" });
    expect(words).toHaveAttribute("href", "/bible/45/8?v=28&note=words#v28");
    expect(screen.getByText("Öffentlich · wartet auf Freigabe")).toBeInTheDocument();

    const chapter = screen.getByRole("link", { name: "Römer 8" });
    expect(chapter).toHaveAttribute("href", "/bible/45/8?note=chapter");
    expect(
      within(chapter.closest("li") as HTMLElement).getByText("Angemeldete Nutzer"),
    ).toBeInTheDocument();

    expect(screen.getByRole("link", { name: "Römer 8,28–30" })).toHaveAttribute(
      "href",
      "/bible/45/8?v=28-30&note=range#v28",
    );
    expect(screen.getByText("Öffentlich · freigegeben")).toBeInTheDocument();
    expect(screen.getByText("4 Notizen")).toBeInTheDocument();
  });

  it("filters by book and visibility and starts again at the first page", async () => {
    const calls: MineCall[] = [];
    stubFetch(true, () => ({ total: 1, notes: [note("a")] }), calls);
    renderAt("/notes?page=3");
    await screen.findByText("Römer 8,28");
    expect(calls.at(-1)?.get("skip")).toBe("100");
    expect(calls.at(-1)?.get("take")).toBe("50");

    await userEvent.selectOptions(screen.getByLabelText("Buch:"), "45");
    await waitFor(() => expect(calls.at(-1)?.get("book")).toBe("45"));
    expect(calls.at(-1)?.get("skip")).toBe("0");

    await userEvent.selectOptions(screen.getByLabelText("Sichtbarkeit:"), "internal");
    await waitFor(() => expect(calls.at(-1)?.get("visibility")).toBe("internal"));
    expect(calls.at(-1)?.get("book")).toBe("45");
  });

  it("searches the text debounced and shows the no-match message", async () => {
    const calls: MineCall[] = [];
    stubFetch(
      true,
      (query) =>
        query.get("q") === "nichts" ? { total: 0, notes: [] } : { total: 1, notes: [note("a")] },
      calls,
    );
    renderAt("/notes");
    await screen.findByText("Römer 8,28");
    const callsBefore = calls.length;

    await userEvent.type(screen.getByLabelText("Suche im Text"), "nichts");
    expect(await screen.findByText("Keine Notiz passt zu diesen Filtern.")).toBeInTheDocument();
    expect(calls.at(-1)?.get("q")).toBe("nichts");
    // typed in one go: one request for the debounced text, not one per letter
    expect(calls.length - callsBefore).toBe(1);
  });

  it("pages through 120 notes with 50 per page", async () => {
    const all = Array.from({ length: 120 }, (_, i) => note(`n${i}`));
    const calls: MineCall[] = [];
    stubFetch(
      true,
      (query) => {
        const skip = Number(query.get("skip"));
        return { total: all.length, notes: all.slice(skip, skip + Number(query.get("take"))) };
      },
      calls,
    );
    renderAt("/notes");
    expect(await screen.findByText("1–50 von 120")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Zurück" })).toBeDisabled();

    await userEvent.click(screen.getByRole("button", { name: "Weiter" }));
    expect(await screen.findByText("51–100 von 120")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Weiter" }));
    expect(await screen.findByText("101–120 von 120")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Weiter" })).toBeDisabled();
    expect(calls.map((c) => c.get("skip"))).toEqual(["0", "50", "100"]);
  });

  it("shows the empty message when there are no notes", async () => {
    stubFetch(true, () => ({ total: 0, notes: [] }));
    renderAt("/notes");
    expect(await screen.findByText(/Du hast noch keine Notizen/)).toBeInTheDocument();
  });

  it("downloads the export file under the server's file name", async () => {
    const fetchMock = stubFetch(true, () => ({ total: 1, notes: [note("a")] }));
    const saved: string[] = [];
    vi.stubGlobal("URL", {
      ...URL,
      createObjectURL: vi.fn(() => "blob:x"),
      revokeObjectURL: vi.fn(),
    });
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      saved.push(this.download);
    });
    renderAt("/notes");
    await userEvent.click(
      await screen.findByRole("button", { name: "Meine Notizen herunterladen" }),
    );
    await waitFor(() => expect(saved).toEqual(["notes-20261122-180000.json"]));
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("/notes/mine/export"))).toBe(
      true,
    );
    click.mockRestore();
  });
});
