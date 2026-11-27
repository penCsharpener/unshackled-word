import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { BibleStructureBook } from "@/lib/bible/types";
import type { Note, ReviewNotesResponse } from "@/lib/notes/api";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { NoteReviewPage } from "./NoteReviewPage";

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

const note = (id: string, body: string, overrides: Partial<Note> = {}): Note => ({
  id,
  anchor: { kind: "verse", bookId: 45, chapter: 8, startHebRefId: 45008028, startVerse: 28 },
  anchorState: "ok",
  bodyMarkdown: body,
  bodyLinked: body,
  visibility: "public",
  publicState: "pending",
  isOwn: false,
  authorDisplayName: "Ada",
  createdAt: "2026-11-22T10:00:00Z",
  updatedAt: "2026-11-23T09:00:00Z",
  ...overrides,
});

const queue: ReviewNotesResponse = {
  pending: [
    {
      note: note("edited", "Gott ist treu. Er hält immer, was er sagt. Amen."),
      approvedBodyMarkdown: "Gott ist treu. Er hält, was er verspricht. Amen.",
      approvedAt: "2026-11-22T12:00:00Z",
    },
    { note: note("fresh", "Ganz neue Notiz.") },
  ],
  hidden: [
    {
      note: note("gone", "Werbung", {
        visibility: "internal",
        publicState: "none",
        isHidden: true,
      }),
      hiddenAt: "2026-11-21T08:00:00Z",
      reviewNote: "Spam",
    },
  ],
};

type Call = { url: string; method: string; body?: unknown };

function stubFetch(roles: string[], calls: Call[] = [], approveStatus = 204) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(String(init.body)) : undefined });
    if (url.includes("/meta-bible/structure")) return new Response(JSON.stringify({ books }));
    if (url.includes("/auth/me")) {
      return new Response(JSON.stringify({ userId: 9, email: "m@b.c", displayName: "Max", roles }));
    }
    if (url.includes("/maintainer/notes/review")) return new Response(JSON.stringify(queue));
    if (url.endsWith("/approve") && approveStatus !== 204) {
      return new Response(JSON.stringify({ errors: { generalErrors: ["changed"] } }), {
        status: approveStatus,
      });
    }
    if (url.includes("/maintainer/notes/") && method === "POST") {
      return new Response(null, { status: 204 });
    }
    return new Response("{}");
  });
  vi.stubGlobal("fetch", fetchMock);
  return calls;
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [
      { path: "/edit/notes", element: <NoteReviewPage /> },
      { path: "/login", element: <p>Anmeldeseite</p> },
    ],
    { initialEntries: ["/edit/notes"] },
  );
  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

const posts = (calls: Call[]) => calls.filter((c) => c.method === "POST");
const pendingItem = async (id: string) => {
  await screen.findAllByRole("listitem");
  const item = document.querySelector(`[data-review-id="${id}"]`);
  if (!(item instanceof HTMLElement)) throw new Error(`no review item ${id}`);
  return item;
};

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("NoteReviewPage", () => {
  it("refuses users without a maintainer role", async () => {
    stubFetch([]);
    renderPage();
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Notizen prüfen" })).toBeNull();
  });

  it("highlights the changed sentence of an edited note against the approved text", async () => {
    stubFetch(["Maintainer"]);
    renderPage();
    const item = await pendingItem("edited");
    expect(within(item).getByRole("link", { name: "Römer 8,28" })).toHaveAttribute(
      "href",
      "/bible/45/8?v=28&note=edited#v28",
    );
    expect(within(item).getByText("von Ada")).toBeInTheDocument();
    const changed = item.querySelectorAll("mark[data-changed]");
    expect(changed).toHaveLength(1);
    expect(changed[0]?.textContent).toContain("immer");
    expect(changed[0]?.textContent).not.toContain("Gott ist treu");
    expect([...item.querySelectorAll("ins")].map((e) => e.textContent?.trim())).toEqual([
      "hält immer,",
      "sagt.",
    ]);
    expect([...item.querySelectorAll("del")].map((e) => e.textContent?.trim())).toEqual([
      "hält,",
      "verspricht.",
    ]);
    const fresh = await pendingItem("fresh");
    expect(within(fresh).getByText("Erste Freigabe: noch kein freigegebener Text.")).toBeVisible();
    expect(fresh.querySelector("mark")).toBeNull();
  });

  it("approves a note and reloads the queue", async () => {
    const calls = stubFetch(["Maintainer"]);
    renderPage();
    const item = await pendingItem("edited");
    const loads = () => calls.filter((c) => c.url.includes("/maintainer/notes/review")).length;
    const before = loads();
    await userEvent.click(within(item).getByRole("button", { name: "Freigeben" }));
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(posts(calls)[0]?.url).toMatch(/\/maintainer\/notes\/edited\/approve$/);
    expect(posts(calls)[0]?.body).toEqual({ reviewedVersion: "2026-11-23T09:00:00Z" });
    expect(await screen.findByText("Notiz freigegeben.")).toBeInTheDocument();
    await waitFor(() => expect(loads()).toBeGreaterThan(before));
  });

  it("explains and reloads the queue when the note changed after the review (409)", async () => {
    const calls = stubFetch(["Maintainer"], [], 409);
    renderPage();
    const item = await pendingItem("edited");
    const loads = () => calls.filter((c) => c.url.includes("/maintainer/notes/review")).length;
    const before = loads();
    await userEvent.click(within(item).getByRole("button", { name: "Freigeben" }));
    expect(await screen.findByText(/wurde inzwischen geändert/)).toBeInTheDocument();
    await waitFor(() => expect(loads()).toBeGreaterThan(before));
    expect(screen.queryByText("Notiz freigegeben.")).toBeNull();
  });

  it("rejects a note with a reason", async () => {
    const calls = stubFetch(["Admin"]);
    renderPage();
    const item = await pendingItem("fresh");
    await userEvent.click(within(item).getByRole("button", { name: "Ablehnen" }));
    const dialog = await screen.findByRole("dialog", { name: "Notiz ablehnen" });
    await userEvent.type(within(dialog).getByRole("textbox"), "Bitte Quelle angeben");
    await userEvent.click(within(dialog).getByRole("button", { name: "Ablehnen" }));
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(posts(calls)[0]).toMatchObject({
      url: expect.stringMatching(/\/maintainer\/notes\/fresh\/reject$/),
      body: { reason: "Bitte Quelle angeben" },
    });
    expect(await screen.findByText("Notiz abgelehnt.")).toBeInTheDocument();
  });

  it("hides a pending note and unhides a hidden one", async () => {
    const calls = stubFetch(["Maintainer"]);
    renderPage();
    const item = await pendingItem("fresh");
    await userEvent.click(within(item).getByRole("button", { name: "Ausblenden" }));
    const dialog = await screen.findByRole("dialog", { name: "Notiz ausblenden" });
    await userEvent.click(within(dialog).getByRole("button", { name: "Ausblenden" }));
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(posts(calls)[0]).toMatchObject({
      url: expect.stringMatching(/\/fresh\/hide$/),
      body: {},
    });

    const hidden = document.querySelector('[data-hidden-id="gone"]');
    if (!(hidden instanceof HTMLElement)) throw new Error("no hidden item");
    expect(within(hidden).getByText("Grund: Spam")).toBeInTheDocument();
    await userEvent.click(within(hidden).getByRole("button", { name: "Wieder einblenden" }));
    await waitFor(() => expect(posts(calls)).toHaveLength(2));
    expect(posts(calls)[1]?.url).toMatch(/\/maintainer\/notes\/gone\/unhide$/);
  });
});
