import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { Note } from "@/lib/notes/api";
import { NotesPanel, type NotesPanelProps } from "./NotesPanel";

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

const notes = [
  note("c1", undefined, "# Überblick\n\nDas ganze Kapitel"),
  note("v28", 28, "Alle Dinge **wirken** zum Guten"),
];

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
      return new Response("", { status: 204 });
    }
    return new Response(JSON.stringify({ notes }), { status: 200 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderPanel(props: Partial<NotesPanelProps> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const all: NotesPanelProps = {
    variant: "sheet",
    bookId: 45,
    chapter: 8,
    chapterLabel: "Römer 8",
    notes,
    status: "success",
    onRetry: vi.fn(),
    onFocus: vi.fn(),
    onDraftDone: vi.fn(),
    onClose: vi.fn(),
    signedIn: true,
    ...props,
  };
  const view = render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <NotesPanel {...all} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { ...view, props: all, client };
}

const calls = (fetchMock: ReturnType<typeof stubFetch>, method: string) =>
  fetchMock.mock.calls.filter(([, init]) => init?.method === method);

beforeEach(async () => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("NotesPanel", () => {
  it("lists the notes in reading order, collapsed to an excerpt", () => {
    stubFetch();
    renderPanel();
    expect(screen.getByRole("heading", { name: "Notizen zu Römer 8" })).toBeInTheDocument();
    const items = screen.getAllByRole("listitem");
    expect(items.map((li) => within(li).getAllByRole("button")[0]?.textContent)).toEqual([
      "Römer 8 (Kapitel)",
      "Römer 8,28",
    ]);
    expect(within(items[1] as HTMLElement).getByText("Alle Dinge wirken zum Guten")).toBeVisible();
    expect(screen.queryByTestId("note-body")).toBeNull();
  });

  it("expands and scrolls to the focused note", async () => {
    stubFetch();
    renderPanel({ focus: { id: "v28", token: 1 } });
    expect(await screen.findByTestId("note-body")).toHaveTextContent("Alle Dinge **wirken**");
    expect(screen.getByRole("button", { name: "Römer 8,28", expanded: true })).toBeInTheDocument();
    expect(window.HTMLElement.prototype.scrollIntoView).toHaveBeenCalled();
    expect(screen.getByText(/^Ada · /)).toBeInTheDocument();
  });

  it("creates a verse note and focuses it", async () => {
    const fetchMock = stubFetch();
    const { props } = renderPanel({ draft: { kind: "verse", verse: 28 } });
    expect(await screen.findByText("Neue Notiz zu Römer 8,28")).toBeInTheDocument();
    const save = screen.getByRole("button", { name: "Speichern" });
    expect(save).toBeDisabled();
    await userEvent.type(screen.getByLabelText("Notiztext"), "Neuer Gedanke");
    await userEvent.click(save);
    await waitFor(() => expect(props.onFocus).toHaveBeenCalledWith("new"));
    expect(props.onDraftDone).toHaveBeenCalled();
    const [url, init] = calls(fetchMock, "POST")[0] ?? [];
    expect(String(url)).toMatch(/\/notes$/);
    expect(JSON.parse(String(init?.body))).toEqual({
      anchor: { kind: "verse", bookId: 45, chapter: 8, startHebRefId: 45008028 },
      bodyMarkdown: "Neuer Gedanke",
    });
  });

  it("creates a chapter note without a verse", async () => {
    const fetchMock = stubFetch();
    renderPanel({ draft: { kind: "chapter" } });
    await userEvent.type(await screen.findByLabelText("Notiztext"), "Kapitel");
    fireEvent.keyDown(window, { key: "s", ctrlKey: true });
    await waitFor(() => expect(calls(fetchMock, "POST")).toHaveLength(1));
    const body = JSON.parse(String(calls(fetchMock, "POST")[0]?.[1]?.body));
    expect(body.anchor).toEqual({ kind: "chapter", bookId: 45, chapter: 8 });
  });

  it("edits an own note with Ctrl+S", async () => {
    const fetchMock = stubFetch();
    renderPanel({ focus: { id: "v28", token: 1 } });
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
  });

  it("creates a public note: the body first, then the visibility request", async () => {
    const fetchMock = stubFetch();
    const { props } = renderPanel({ draft: { kind: "verse", verse: 28 } });
    await userEvent.type(await screen.findByLabelText("Notiztext"), "Für alle");
    const select = screen.getByLabelText("Sichtbarkeit:");
    expect(select).toHaveValue("private");
    await userEvent.selectOptions(select, "public");
    expect(screen.getByText(/erst, wenn ein Maintainer sie freigegeben hat/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    await waitFor(() => expect(calls(fetchMock, "PUT")).toHaveLength(1));
    expect(calls(fetchMock, "POST")).toHaveLength(1);
    const [url, init] = calls(fetchMock, "PUT")[0] ?? [];
    expect(String(url)).toMatch(/\/notes\/new\/visibility$/);
    expect(JSON.parse(String(init?.body))).toEqual({ visibility: "public" });
    expect(props.onFocus).toHaveBeenCalledWith("new");
  });

  it("changes only the visibility when the body is unchanged", async () => {
    const fetchMock = stubFetch();
    renderPanel({ focus: { id: "v28", token: 1 } });
    await userEvent.click(await screen.findByRole("button", { name: "Bearbeiten" }));
    await userEvent.selectOptions(await screen.findByLabelText("Sichtbarkeit:"), "internal");
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    await waitFor(() => expect(calls(fetchMock, "PUT")).toHaveLength(1));
    const [url, init] = calls(fetchMock, "PUT")[0] ?? [];
    expect(String(url)).toMatch(/\/notes\/v28\/visibility$/);
    expect(JSON.parse(String(init?.body))).toEqual({ visibility: "internal" });
    await waitFor(() => expect(screen.queryByLabelText("Notiztext")).toBeNull());
  });

  it("shows the review state and the rejection reason of an own note", async () => {
    stubFetch();
    const rejected: Note = {
      ...note("r", 5, "Abgelehnt"),
      visibility: "public",
      publicState: "rejected",
      reviewNote: "Bitte Quelle angeben",
    };
    renderPanel({ notes: [rejected], focus: { id: "r", token: 1 } });
    expect(await screen.findByText("Öffentlich · abgelehnt")).toBeInTheDocument();
    expect(screen.getByText("Abgelehnt: Bitte Quelle angeben")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Bearbeiten" }));
    expect(await screen.findByLabelText("Sichtbarkeit:")).toHaveValue("public");
    expect(screen.getByText("Abgelehnt: Bitte Quelle angeben")).toBeInTheDocument();
  });

  it("shows no state badge on others' notes", async () => {
    stubFetch();
    const other: Note = { ...note("o", 3, "Fremd", false), visibility: "internal" };
    renderPanel({ notes: [other], focus: { id: "o", token: 1 } });
    expect(await screen.findByTestId("note-body")).toHaveTextContent("Fremd");
    expect(screen.getByText(/^Bert · /)).toBeInTheDocument();
    expect(screen.queryByText("Angemeldete Nutzer")).toBeNull();
  });

  it("deletes an own note after the confirm", async () => {
    const fetchMock = stubFetch();
    renderPanel({ focus: { id: "v28", token: 1 } });
    await userEvent.click(await screen.findByRole("button", { name: "Bearbeiten" }));
    await userEvent.click(await screen.findByRole("button", { name: "Löschen" }));
    expect(calls(fetchMock, "DELETE")).toHaveLength(0);
    const dialog = screen.getByRole("dialog", { name: "Notiz löschen?" });
    await userEvent.click(within(dialog).getByRole("button", { name: "Endgültig löschen" }));
    await waitFor(() => expect(calls(fetchMock, "DELETE")).toHaveLength(1));
    expect(String(calls(fetchMock, "DELETE")[0]?.[0])).toMatch(/\/notes\/v28$/);
  });

  it("offers no edit for others' notes and shows their author", async () => {
    stubFetch();
    renderPanel({ notes: [note("x", 3, "Fremd", false)], focus: { id: "x", token: 1 } });
    expect(await screen.findByTestId("note-body")).toHaveTextContent("Fremd");
    expect(screen.getByRole("button", { name: /Bert/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Bearbeiten" })).toBeNull();
  });

  it("asks signed-out readers to sign in instead of opening the editor", () => {
    stubFetch();
    renderPanel({ draft: { kind: "verse", verse: 28 }, signedIn: false });
    expect(screen.getByText("Melde dich an, um Notizen zu schreiben.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Anmelden" })).toHaveAttribute("href", "/login");
    expect(screen.queryByLabelText("Notiztext")).toBeNull();
  });

  it("says when the chapter has no notes", () => {
    stubFetch();
    renderPanel({ notes: [] });
    expect(screen.getByText("In diesem Kapitel gibt es noch keine Notizen.")).toBeInTheDocument();
  });
});
