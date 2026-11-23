import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { TskReviewProposal } from "@/lib/bible/types";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { TskReviewPage } from "./TskReviewPage";

/** B's suggestion of the 0024.6 scenario: hide Ps 104,30 under 1Mo 1,2 (TSK link, no curation yet). */
const hidePs = (over: Partial<TskReviewProposal> = {}): TskReviewProposal => ({
  id: 12,
  sourceHebRefId: 1001002,
  targetStartHebRefId: 19104030,
  inTsk: true,
  proposed: { origin: "tsk", rating: "hide" },
  authorUserId: "b",
  authorDisplayName: "Berta",
  createdAt: "2026-11-23T09:00:00Z",
  stale: false,
  ...over,
});

/** A comment change on another link of the same verse, already curated and changed since (stale). */
const commentPs = hidePs({
  id: 13,
  targetStartHebRefId: 19033006,
  targetEndHebRefId: 19033009,
  current: {
    origin: "tsk",
    rating: "highlight",
    comment: "Schöpfung durch das Wort",
    updatedAt: "2026-11-23T10:00:00Z",
  },
  proposed: {
    origin: "tsk",
    rating: "highlight",
    comment: "Schöpfung durch Gottes Wort",
    word: { startPosition: 16, endPosition: 16, quotedText: "Geist", state: "ok" },
  },
  stale: true,
});

/** A new link under another source verse. */
const addMal = hidePs({
  id: 14,
  sourceHebRefId: 2003014,
  targetStartHebRefId: 39003006,
  inTsk: false,
  proposed: { origin: "added", comment: "Ich bin" },
});

type Call = { url: string; method: string; body?: unknown };

function stubFetch(roles: string[] | undefined, calls: Call[] = [], acceptStatus = 200) {
  let proposals = [hidePs(), commentPs, addMal];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(String(init.body)) : undefined });
    if (url.includes("/meta-bible/structure")) {
      return new Response(JSON.stringify({ books: fixtureBooks }));
    }
    if (url.includes("/auth/me")) {
      return roles
        ? new Response(JSON.stringify({ userId: 9, email: "m@b.c", displayName: "Max", roles }))
        : new Response("", { status: 401 });
    }
    if (url.endsWith("/maintainer/tsk/proposals") && method === "GET") {
      return new Response(JSON.stringify({ proposals }));
    }
    const decided = /\/maintainer\/tsk\/proposals\/(\d+)\/(accept|reject)$/.exec(url);
    if (decided && method === "POST") {
      if (decided[2] === "accept" && acceptStatus !== 200) {
        return new Response(JSON.stringify({ message: "stale" }), { status: acceptStatus });
      }
      const id = Number(decided[1]);
      proposals = proposals.filter((p) => p.id !== id);
      return new Response(
        JSON.stringify({
          proposal: { id, state: decided[2] === "accept" ? "accepted" : "rejected" },
        }),
      );
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
      { path: "/edit/tsk", element: <TskReviewPage /> },
      { path: "/login", element: <p>Anmeldeseite</p> },
    ],
    { initialEntries: ["/edit/tsk"] },
  );
  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

const row = async (id: number) => {
  await screen.findAllByRole("listitem");
  const item = document.querySelector(`[data-proposal-id="${id}"]`);
  if (!(item instanceof HTMLElement)) throw new Error(`no proposal row ${id}`);
  return item;
};
const posts = (calls: Call[]) => calls.filter((c) => c.method === "POST");
const listLoads = (calls: Call[]) =>
  calls.filter((c) => c.method === "GET" && c.url.endsWith("/maintainer/tsk/proposals")).length;

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("TskReviewPage (roadmap 0024.6f)", () => {
  it("refuses users without a maintainer role and does not load the list", async () => {
    const calls = stubFetch(["User"]);
    renderPage();
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Du hast keinen Zugriff auf diese Seite.",
    );
    expect(screen.queryByRole("heading", { name: "TSK-Vorschläge" })).toBeNull();
    expect(listLoads(calls)).toBe(0);
  });

  it("sends anonymous visitors to the sign-in page", async () => {
    stubFetch(undefined);
    renderPage();
    expect(await screen.findByText("Anmeldeseite")).toBeInTheDocument();
  });

  it("groups by source verse and shows current against proposed state with the comment diff", async () => {
    stubFetch(["Maintainer"]);
    renderPage();
    expect(await screen.findByRole("heading", { name: "TSK-Vorschläge" })).toBeInTheDocument();
    const gen = await screen.findByRole("region", { name: "1. Mose 1,2" });
    expect(within(gen).getByRole("link", { name: "1. Mose 1,2" })).toHaveAttribute(
      "href",
      "/bible/1/1#v2",
    );
    expect(screen.getByRole("region", { name: "2. Mose 3,14" })).toBeInTheDocument();

    const hide = await row(12);
    expect(gen).toContainElement(hide);
    expect(within(hide).getByRole("link", { name: "Psalmen 104,30" })).toHaveAttribute(
      "href",
      "/bible/19/104#v30",
    );
    expect(hide).toHaveTextContent("von Berta");
    expect(hide).toHaveTextContent("AktuellBewertung: Neutral");
    expect(hide).toHaveTextContent("VorgeschlagenBewertung: Ausblenden");
    expect(within(hide).queryByTestId("tsk-stale")).toBeNull();

    const comment = await row(13);
    expect(within(comment).getByRole("link", { name: "Psalmen 33,6–9" })).toBeInTheDocument();
    expect(within(comment).getByTestId("tsk-stale")).toHaveTextContent(
      "Der Querverweis wurde seit dem Vorschlag geändert.",
    );
    const diff = within(comment).getByTestId("tsk-comment-diff");
    expect(diff.querySelector("del")).toHaveTextContent("das");
    expect(diff.querySelector("ins")).toHaveTextContent("Gottes");
    expect(comment).toHaveTextContent("Wort: „Geist“");

    const added = await row(14);
    expect(added).toHaveTextContent("Neuer Querverweis");
    expect(added).toHaveTextContent("kein Querverweis");
  });

  it("rejects with a reason („zu schwach“) and reloads the list", async () => {
    const calls = stubFetch(["Maintainer"]);
    const user = userEvent.setup();
    renderPage();
    const hide = await row(12);
    await user.click(within(hide).getByRole("button", { name: "Ablehnen" }));
    const dialog = screen.getByRole("dialog", { name: "Vorschlag ablehnen" });
    await user.type(within(dialog).getByRole("textbox"), "  zu schwach ");
    await user.click(within(dialog).getByRole("button", { name: "Ablehnen" }));
    expect(await screen.findByText("Vorschlag abgelehnt.")).toBeInTheDocument();
    expect(posts(calls)).toEqual([
      expect.objectContaining({
        url: "/api/maintainer/tsk/proposals/12/reject",
        body: { reason: "zu schwach" },
      }),
    ]);
    await waitFor(() => expect(document.querySelector('[data-proposal-id="12"]')).toBeNull());
    expect(listLoads(calls)).toBeGreaterThanOrEqual(2);
  });

  it("accepts a fresh suggestion directly without force", async () => {
    const calls = stubFetch(["Admin"]);
    const user = userEvent.setup();
    renderPage();
    await user.click(within(await row(12)).getByRole("button", { name: "Annehmen" }));
    expect(await screen.findByText("Vorschlag angenommen.")).toBeInTheDocument();
    expect(posts(calls)).toEqual([
      expect.objectContaining({ url: "/api/maintainer/tsk/proposals/12/accept", body: {} }),
    ]);
    await waitFor(() => expect(document.querySelector('[data-proposal-id="12"]')).toBeNull());
  });

  it("asks before accepting a stale suggestion and then sends force", async () => {
    const calls = stubFetch(["Maintainer"]);
    const user = userEvent.setup();
    renderPage();
    await user.click(within(await row(13)).getByRole("button", { name: "Annehmen" }));
    const dialog = screen.getByRole("dialog", { name: "Veralteten Vorschlag annehmen?" });
    expect(posts(calls)).toEqual([]);
    await user.click(within(dialog).getByRole("button", { name: "Abbrechen" }));
    expect(posts(calls)).toEqual([]);

    await user.click(within(await row(13)).getByRole("button", { name: "Annehmen" }));
    await user.click(
      within(screen.getByRole("dialog", { name: "Veralteten Vorschlag annehmen?" })).getByRole(
        "button",
        { name: "Trotzdem annehmen" },
      ),
    );
    expect(await screen.findByText("Vorschlag angenommen.")).toBeInTheDocument();
    expect(posts(calls)).toEqual([
      expect.objectContaining({
        url: "/api/maintainer/tsk/proposals/13/accept",
        body: { force: true },
      }),
    ]);
  });

  it("reports a stale refusal of the server and reloads", async () => {
    const calls = stubFetch(["Maintainer"], [], 400);
    const user = userEvent.setup();
    renderPage();
    await user.click(within(await row(12)).getByRole("button", { name: "Annehmen" }));
    expect(
      await screen.findByText("Der Vorschlag ist veraltet und wurde nicht übernommen."),
    ).toBeInTheDocument();
    await waitFor(() => expect(listLoads(calls)).toBeGreaterThanOrEqual(2));
  });
});
