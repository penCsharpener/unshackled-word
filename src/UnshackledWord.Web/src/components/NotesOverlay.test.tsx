import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { VerseWords } from "@/lib/bible/types";
import type { Note } from "@/lib/notes/api";
import { NotesOverlay, type NotesOverlayProps, notesAtPassage } from "./NotesOverlay";

vi.mock("@mdxeditor/editor", () => import("@/test/mdxEditorMock"));

const note = (id: string, verse: number | undefined, body: string, isOwn = true): Note => ({
  id,
  anchor:
    verse === undefined
      ? { kind: "chapter", bookId: 45, chapter: 8 }
      : {
          kind: "verse",
          bookId: 45,
          chapter: 8,
          startHebRefId: 45008000 + verse,
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

const words: Note = {
  ...note("w", 28, "Über **alle Dinge**"),
  anchor: {
    kind: "words",
    bookId: 45,
    chapter: 8,
    startHebRefId: 45008028,
    startVerse: 28,
    startPosition: 9,
    endPosition: 10,
    quotedText: "alle Dinge",
  },
};

const notes = [
  note("c1", undefined, "# Überblick\n\nDas ganze Kapitel"),
  note("v28", 28, "Alle Dinge **wirken** zum Guten"),
  note("v28b", 28, "Fremder Gedanke", false),
  note("v30", 30, "Berufen und gerecht"),
];

const verse28: VerseWords = {
  verse: 28,
  words: "Wir wissen aber, dass denen, die Gott lieben, alle Dinge zum Guten mitwirken"
    .split(" ")
    .map((text, index) => ({
      id: 100 + index,
      text,
      senses: [],
      position: index + 1,
      basePosition: index + 1,
    })),
};

function stubFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.endsWith("/notes") && init?.method === "POST") {
      const body = JSON.parse(String(init.body)) as { bodyMarkdown: string };
      return new Response(JSON.stringify(note("new", 28, body.bodyMarkdown)), { status: 201 });
    }
    if (url.includes("/notes/") && init?.method === "PUT") {
      return new Response(JSON.stringify(notes[1]), { status: 200 });
    }
    if (url.includes("/notes/") && init?.method === "DELETE") {
      return new Response(null, { status: 204 });
    }
    return new Response(JSON.stringify({ notes }), { status: 200 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderOverlay(props: Partial<NotesOverlayProps> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const all: NotesOverlayProps = {
    request: { passage: { kind: "verse", verse: 28 } },
    onClose: vi.fn(),
    bookId: 45,
    chapter: 8,
    chapterLabel: "Römer 8",
    verses: [verse28],
    notes,
    status: "success",
    onRetry: vi.fn(),
    signedIn: true,
    showOthers: true,
    onShowOthersChange: vi.fn(),
    ...props,
  };
  const view = render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <NotesOverlay {...all} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { ...view, props: all, client };
}

const calls = (fetchMock: ReturnType<typeof stubFetch>, method: string) =>
  fetchMock.mock.calls.filter(([, init]) => init?.method === method);
const rows = () =>
  within(screen.getByRole("list", { name: "Notizen" }))
    .getAllByRole("listitem")
    .map((li) => within(li).getByRole("button"));
const chip = (name: RegExp) => screen.getByRole("button", { name });

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("notesAtPassage", () => {
  it("keeps chapter notes for the chapter and overlapping verse notes for a verse range", () => {
    const range: Note = {
      ...note("r", 27, "Bogen"),
      anchor: { ...note("r", 27, "").anchor, kind: "verses", endVerse: 29 },
    };
    const all = [...notes, range];
    expect(notesAtPassage(all, { kind: "chapter" }).map((n) => n.id)).toEqual(["c1"]);
    expect(notesAtPassage(all, { kind: "verse", verse: 28 }).map((n) => n.id)).toEqual([
      "v28",
      "v28b",
      "r",
    ]);
    expect(
      notesAtPassage(all, { kind: "verses", verse: 29, endVerse: 30 }).map((n) => n.id),
    ).toEqual(["v30", "r"]);
  });
});

describe("NotesOverlay", () => {
  it("opens focused on the requested note with the reference and the verse excerpt", async () => {
    stubFetch();
    renderOverlay({ request: { passage: { kind: "verse", verse: 28 }, noteId: "v28b" } });
    const dialog = await screen.findByRole("dialog", { name: "Römer 8,28" });
    expect(await within(dialog).findByTestId("note-body")).toHaveTextContent("Fremder Gedanke");
    expect(within(dialog).getByTestId("notes-excerpt")).toHaveTextContent(/^Wir wissen aber/);
    expect(rows().map((row) => row.getAttribute("aria-current"))).toEqual([null, "true"]);
    // others' notes: author in the row (marker colour) and in the note, no owner actions
    expect(within(rows()[1] as HTMLElement).getByText("Bert")).toHaveClass("text-note-other");
    expect(within(rows()[0] as HTMLElement).getByText("Du")).toHaveClass("text-accent");
    expect(screen.getByText(/^Bert · /)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Bearbeiten" })).toBeNull();
  });

  it("highlights the anchored words of a words note in the excerpt", async () => {
    stubFetch();
    renderOverlay({
      notes: [words],
      request: { passage: { kind: "verse", verse: 28 }, noteId: "w" },
    });
    const excerpt = await screen.findByTestId("notes-excerpt");
    const marked = Array.from(excerpt.querySelectorAll(".bg-mark-note")).map((m) => m.textContent);
    expect(marked).toEqual(["alle", "Dinge"]);
    expect(excerpt).toHaveTextContent(/^… denen, die Gott/);
    expect(screen.getByText("„alle Dinge“ · Römer 8,28", { selector: "p" })).toBeInTheDocument();
  });

  it("filters by passage and chapter with counts", async () => {
    stubFetch();
    renderOverlay();
    expect(chip(/^Diese Stelle\d/)).toHaveAttribute("aria-pressed", "true");
    expect(chip(/^Diese Stelle\d/)).toHaveTextContent("Diese Stelle2");
    expect(chip(/^Kapitel\d/)).toHaveTextContent("Kapitel4");
    expect(rows()).toHaveLength(2);
    await userEvent.click(chip(/^Kapitel\d/));
    expect(chip(/^Kapitel\d/)).toHaveAttribute("aria-pressed", "true");
    expect(rows().map((row) => row.textContent)).toEqual([
      expect.stringContaining("Römer 8 (Kapitel)"),
      expect.stringContaining("Römer 8,28"),
      expect.stringContaining("Römer 8,28"),
      expect.stringContaining("Römer 8,30"),
    ]);
    expect(
      within(rows()[0] as HTMLElement).getByText("Das ganze Kapitel", { exact: false }),
    ).toBeInTheDocument();
  });

  it("starts on the chapter filter when asked", async () => {
    stubFetch();
    renderOverlay({ request: { passage: { kind: "chapter" }, filter: "chapter" } });
    expect(chip(/^Kapitel\d/)).toHaveAttribute("aria-pressed", "true");
    expect(rows()).toHaveLength(4);
    expect(await screen.findByRole("dialog", { name: "Römer 8" })).toBeInTheDocument();
  });

  it("moves through the rows with the arrow keys", async () => {
    stubFetch();
    renderOverlay({ request: { passage: { kind: "chapter" }, filter: "chapter" } });
    (rows()[0] as HTMLElement).focus();
    await userEvent.keyboard("{ArrowDown}");
    expect(rows()[1]).toHaveAttribute("aria-current", "true");
    expect(rows()[1]).toHaveFocus();
    await userEvent.keyboard("{ArrowDown}{ArrowDown}{ArrowDown}");
    expect(rows()[3]).toHaveAttribute("aria-current", "true");
    await userEvent.keyboard("{ArrowUp}");
    expect(rows()[2]).toHaveAttribute("aria-current", "true");
    expect(await screen.findByTestId("note-body")).toHaveTextContent("Fremder Gedanke");
  });

  it("edits an own note and saves it with Ctrl+S", async () => {
    const fetchMock = stubFetch();
    renderOverlay({ request: { passage: { kind: "verse", verse: 28 }, noteId: "v28" } });
    await userEvent.click(await screen.findByRole("button", { name: "Bearbeiten" }));
    const field = await screen.findByLabelText("Notiztext");
    await userEvent.clear(field);
    await userEvent.type(field, "Geändert");
    fireEvent.keyDown(window, { key: "s", metaKey: true });
    await waitFor(() => expect(calls(fetchMock, "PUT")).toHaveLength(1));
    const [url, init] = calls(fetchMock, "PUT")[0] ?? [];
    expect(String(url)).toMatch(/\/notes\/v28$/);
    expect(JSON.parse(String(init?.body))).toEqual({ bodyMarkdown: "Geändert" });
    await waitFor(() => expect(screen.queryByLabelText("Notiztext")).toBeNull());
    expect(screen.getByTestId("note-body")).toBeInTheDocument();
  });

  it("changes only the visibility when the body is unchanged", async () => {
    const fetchMock = stubFetch();
    renderOverlay({ request: { passage: { kind: "verse", verse: 28 }, noteId: "v28" } });
    await userEvent.click(await screen.findByRole("button", { name: "Bearbeiten" }));
    await userEvent.selectOptions(await screen.findByLabelText("Sichtbarkeit:"), "internal");
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    await waitFor(() => expect(calls(fetchMock, "PUT")).toHaveLength(1));
    const [url, init] = calls(fetchMock, "PUT")[0] ?? [];
    expect(String(url)).toMatch(/\/notes\/v28\/visibility$/);
    expect(JSON.parse(String(init?.body))).toEqual({ visibility: "internal" });
  });

  it("opens in write mode for the passage and creates the note", async () => {
    const fetchMock = stubFetch();
    renderOverlay({ request: { passage: { kind: "verse", verse: 28 }, write: true } });
    expect(await screen.findByText("Neue Notiz zu Römer 8,28")).toBeInTheDocument();
    const save = screen.getByRole("button", { name: "Speichern" });
    expect(save).toBeDisabled();
    await userEvent.type(screen.getByLabelText("Notiztext"), "Neuer Gedanke");
    await userEvent.click(save);
    await waitFor(() => expect(calls(fetchMock, "POST")).toHaveLength(1));
    const [url, init] = calls(fetchMock, "POST")[0] ?? [];
    expect(String(url)).toMatch(/\/notes$/);
    expect(JSON.parse(String(init?.body))).toEqual({
      anchor: { kind: "verse", bookId: 45, chapter: 8, startHebRefId: 45008028 },
      bodyMarkdown: "Neuer Gedanke",
    });
    await waitFor(() => expect(screen.queryByLabelText("Notiztext")).toBeNull());
  });

  it("creates a public note: the body first, then the visibility request", async () => {
    const fetchMock = stubFetch();
    renderOverlay({ request: { passage: { kind: "chapter" }, write: true, filter: "chapter" } });
    await userEvent.type(await screen.findByLabelText("Notiztext"), "Für alle");
    await userEvent.selectOptions(screen.getByLabelText("Sichtbarkeit:"), "public");
    expect(screen.getByText(/erst, wenn ein Maintainer sie freigegeben hat/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    await waitFor(() => expect(calls(fetchMock, "PUT")).toHaveLength(1));
    const post = JSON.parse(String(calls(fetchMock, "POST")[0]?.[1]?.body));
    expect(post.anchor).toEqual({ kind: "chapter", bookId: 45, chapter: 8 });
    const [url, init] = calls(fetchMock, "PUT")[0] ?? [];
    expect(String(url)).toMatch(/\/notes\/new\/visibility$/);
    expect(JSON.parse(String(init?.body))).toEqual({ visibility: "public" });
  });

  it("writes a new note for the passage from the list button", async () => {
    stubFetch();
    renderOverlay({ request: { passage: { kind: "verses", verse: 28, endVerse: 30 } } });
    await userEvent.click(screen.getByRole("button", { name: "Neue Notiz" }));
    expect(await screen.findByText("Neue Notiz zu Römer 8,28–30")).toBeInTheDocument();
    expect(await screen.findByRole("dialog", { name: "Römer 8,28–30" })).toBeInTheDocument();
  });

  it("asks signed-out readers to sign in instead of opening the editor", async () => {
    stubFetch();
    renderOverlay({
      request: { passage: { kind: "verse", verse: 28 }, write: true },
      signedIn: false,
    });
    expect(screen.getByText("Melde dich an, um Notizen zu schreiben.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Anmelden" })).toHaveAttribute("href", "/login");
    expect(screen.queryByLabelText("Notiztext")).toBeNull();
  });

  it("deletes an own note after the confirm; Esc in the confirm keeps the overlay", async () => {
    const fetchMock = stubFetch();
    const { props } = renderOverlay({
      request: { passage: { kind: "verse", verse: 28 }, noteId: "v28" },
    });
    await userEvent.click(await screen.findByRole("button", { name: "Löschen" }));
    let confirm = screen.getByRole("dialog", { name: "Notiz löschen?" });
    // the browser moves focus into the confirm (jsdom's showModal stand-in does not)
    within(confirm).getByRole("button", { name: "Abbrechen" }).focus();
    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("dialog", { name: "Notiz löschen?" })).toBeNull();
    expect(props.onClose).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole("button", { name: "Löschen" }));
    confirm = screen.getByRole("dialog", { name: "Notiz löschen?" });
    expect(calls(fetchMock, "DELETE")).toHaveLength(0);
    await userEvent.click(within(confirm).getByRole("button", { name: "Endgültig löschen" }));
    await waitFor(() => expect(calls(fetchMock, "DELETE")).toHaveLength(1));
    expect(String(calls(fetchMock, "DELETE")[0]?.[0])).toMatch(/\/notes\/v28$/);
  });

  it("shows the review state of an own note and the drift hint with re-anchoring", async () => {
    stubFetch();
    const rejected: Note = {
      ...words,
      anchorState: "drifted",
      visibility: "public",
      publicState: "rejected",
      reviewNote: "Bitte Quelle angeben",
    };
    const onReanchor = vi.fn();
    renderOverlay({
      notes: [rejected],
      request: { passage: { kind: "verse", verse: 28 }, noteId: "w" },
      onReanchor,
    });
    expect(await screen.findByText("Öffentlich · abgelehnt")).toBeInTheDocument();
    expect(screen.getByText("Abgelehnt: Bitte Quelle angeben")).toBeInTheDocument();
    expect(screen.getByText(/Die markierten Wörter haben sich geändert/)).toBeInTheDocument();
    // drifted: no highlight on the changed words
    expect(screen.getByTestId("notes-excerpt").querySelector(".bg-mark-note")).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: "Neu verankern" }));
    expect(onReanchor).toHaveBeenCalledWith(rejected);
  });

  it("says when the passage has no notes and keeps the others toggle in the list column", async () => {
    stubFetch();
    const { props } = renderOverlay({ request: { passage: { kind: "verse", verse: 5 } } });
    expect(screen.getByText("Zu dieser Stelle gibt es noch keine Notizen.")).toBeInTheDocument();
    const toggle = screen.getByRole("switch", { name: "Notizen anderer anzeigen" });
    await userEvent.click(toggle);
    expect(props.onShowOthersChange).toHaveBeenCalledWith(false);
  });

  it("closes with the close button", async () => {
    stubFetch();
    const { props } = renderOverlay();
    await userEvent.click(screen.getByRole("button", { name: "Schließen" }));
    expect(props.onClose).toHaveBeenCalled();
  });
});
