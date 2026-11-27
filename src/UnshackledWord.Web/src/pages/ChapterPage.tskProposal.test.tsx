import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { SIDE_PANEL_KEY, TSK_ONLY_HIGHLIGHTED_KEY } from "@/lib/bible/sidePanel";
import type { CrossReference, TskPendingProposal, TskProposal } from "@/lib/bible/types";
import { fixtureBooks, fixtureChapterWords } from "@/test/fixtures/bibleStructure";
import { ChapterPage } from "./ChapterPage";

vi.mock("@mdxeditor/editor", () => import("@/test/mdxEditorMock"));

/** Ps 104,30 under 1Mo 1,2 (roadmap 0024.6 scenario), a plain TSK link. */
const ps10430: CrossReference = { bibleBookId: 19, chapter: 104, verse: 30 };

type Call = { url: string; method: string; body?: unknown };

/**
 * User B (role User) against an API that stores suggestions: `PUT tsk/curation` answers 202 `{ proposal }` and the
 * chapter list then carries B's `pendingProposal`; `mine` lists B's suggestions.
 */
function stubFetch(
  state: {
    pending?: TskPendingProposal;
    mine?: TskProposal[];
    extra?: CrossReference[];
  } = {},
  roles = ["User"],
) {
  const calls: Call[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    const body = init?.body ? JSON.parse(String(init.body)) : undefined;
    calls.push({ url, method, body });
    if (url.includes("/meta-bible/structure")) {
      return new Response(JSON.stringify({ books: fixtureBooks }));
    }
    if (url.includes("/auth/me")) {
      return new Response(
        JSON.stringify({ userId: 2, email: "b@b.c", displayName: "Berta", roles }),
      );
    }
    if (url.includes("/revision/levels")) return new Response(JSON.stringify([]));
    if (url.includes("/tsk/proposals/mine")) {
      return new Response(JSON.stringify({ proposals: state.mine ?? [] }));
    }
    if (url.endsWith("/tsk/curation") && method === "PUT") {
      state.pending = { id: 12, ...(body.rating ? { rating: body.rating } : {}) };
      const proposal: TskProposal = {
        id: 12,
        sourceHebRefId: body.sourceHebRefId,
        targetStartHebRefId: body.targetStartHebRefId,
        state: "pending",
        proposed: { origin: "tsk", ...(body.rating ? { rating: body.rating } : {}) },
        createdAt: "2026-11-23T09:00:00Z",
      };
      return new Response(JSON.stringify({ proposal }), { status: 202 });
    }
    if (url.includes("/chapterCrossReferences/")) {
      return new Response(
        JSON.stringify({
          bibleBookId: 1,
          chapter: 1,
          verses: [
            {
              verse: 2,
              references: [
                state.pending ? { ...ps10430, pendingProposal: state.pending } : ps10430,
                ...(state.extra ?? []),
              ],
            },
          ],
        }),
      );
    }
    if (url.includes("/chapterOriginals/")) return new Response(JSON.stringify({ verses: [] }));
    if (url.includes("/notes/chapter/")) return new Response(JSON.stringify({ notes: [] }));
    if (url.includes("/crossReferenceTexts/")) {
      return new Response(JSON.stringify({ references: [] }));
    }
    return new Response(JSON.stringify(fixtureChapterWords));
  });
  vi.stubGlobal("fetch", fetchMock);
  return { calls, state };
}

function renderAt(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [{ path: "/bible/:bookId/:chapter", element: <ChapterPage /> }],
    { initialEntries: [path] },
  );
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

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

const verse2 = () => screen.findByRole("group", { name: "Querverweise zu Vers 2" });
const pen = "Querverweis Psalmen 104,30 bearbeiten";

beforeEach(async () => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  await i18n.changeLanguage("de");
  stubWideScreen();
});
afterEach(() => {
  vi.unstubAllGlobals();
  localStorage.removeItem(SIDE_PANEL_KEY);
  localStorage.removeItem(TSK_ONLY_HIGHLIGHTED_KEY);
});

describe("ChapterPage TSK suggestions (roadmap 0024.6f)", () => {
  it("B suggests hiding Ps 104,30 under 1Mo 1,2: 202 → confirmation → „Vorschlag offen“", async () => {
    const { calls } = stubFetch();
    const user = userEvent.setup();
    renderAt("/bible/1/1");
    const panel = await verse2();
    expect(within(panel).queryByTestId("pending-proposal")).toBeNull();
    await user.click(screen.getByRole("switch", { name: "Bearbeiten" }));
    await user.click(within(panel).getByRole("link", { name: /^Psalmen 104,30/ }));
    const editor = screen.getByRole("dialog", { name: pen });
    await user.click(within(editor).getByRole("button", { name: "Ausblenden" }));
    await user.click(within(editor).getByRole("button", { name: "Vorschlag senden" }));

    expect(await within(editor).findByTestId("proposal-sent")).toHaveTextContent(
      "Vorschlag gesendet. Ein Maintainer prüft ihn.",
    );
    expect(calls.find((c) => c.method === "PUT")).toEqual(
      expect.objectContaining({
        url: "/api/tsk/curation",
        body: {
          sourceHebRefId: 1001002,
          targetStartHebRefId: 19104030,
          rating: "hide",
          comment: null,
        },
      }),
    );
    // the chapter list is refetched and carries B's suggestion
    await waitFor(() =>
      expect(within(panel).getByTestId("pending-proposal")).toHaveTextContent("Vorschlag offen"),
    );
    await user.click(within(editor).getByRole("button", { name: "Schließen" }));
    expect(screen.queryByRole("dialog", { name: pen })).toBeNull();

    // the editor shows current against proposed and starts at the proposed state
    await user.click(within(panel).getByRole("link", { name: /^Psalmen 104,30/ }));
    const again = screen.getByRole("dialog", { name: pen });
    const pending = within(again).getByTestId("editor-pending-proposal");
    expect(pending).toHaveTextContent("Vorschlag offen");
    expect(pending).toHaveTextContent("Aktuell: Neutral");
    expect(pending).toHaveTextContent("Vorgeschlagen: Ausblenden");
    expect(within(again).getByRole("button", { name: "Ausblenden" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("shows the reason of B's rejected suggestion in the editor of that link", async () => {
    stubFetch({
      mine: [
        {
          id: 12,
          sourceHebRefId: 1001002,
          targetStartHebRefId: 19104030,
          state: "rejected",
          proposed: { origin: "tsk", rating: "hide" },
          createdAt: "2026-11-23T09:00:00Z",
          decidedAt: "2026-11-23T10:00:00Z",
          reason: "zu schwach",
        },
      ],
    });
    const user = userEvent.setup();
    renderAt("/bible/1/1");
    const panel = await verse2();
    expect(within(panel).queryByTestId("pending-proposal")).toBeNull();
    await user.click(screen.getByRole("switch", { name: "Bearbeiten" }));
    await user.click(within(panel).getByRole("link", { name: /^Psalmen 104,30/ }));
    const editor = screen.getByRole("dialog", { name: pen });
    expect(await within(editor).findByTestId("editor-rejected")).toHaveTextContent(
      "Abgelehnt: zu schwach",
    );
  });

  it("does not show an old rejection of another link", async () => {
    stubFetch({
      mine: [
        {
          id: 3,
          sourceHebRefId: 1001002,
          targetStartHebRefId: 19033006,
          state: "rejected",
          proposed: { origin: "tsk", rating: "hide" },
          createdAt: "2026-11-23T09:00:00Z",
          reason: "nein",
        },
      ],
    });
    const user = userEvent.setup();
    renderAt("/bible/1/1");
    const panel = await verse2();
    await user.click(screen.getByRole("switch", { name: "Bearbeiten" }));
    await user.click(within(panel).getByRole("link", { name: /^Psalmen 104,30/ }));
    const editor = screen.getByRole("dialog", { name: pen });
    await waitFor(() => expect(within(editor).getByRole("button", { name: "Vorschlag senden" })));
    expect(within(editor).queryByTestId("editor-rejected")).toBeNull();
  });

  it("marks a proposed added link with the „+“ and the badge", async () => {
    stubFetch({
      extra: [
        {
          bibleBookId: 19,
          chapter: 33,
          verse: 6,
          origin: "added",
          pendingProposal: { id: 20, comment: "Wort" },
        },
      ],
    });
    renderAt("/bible/1/1");
    const panel = await verse2();
    const link = await within(panel).findByRole("link", { name: "Psalmen 33,6 (hinzugefügt)" });
    expect(within(link).getByTestId("added-marker")).toHaveTextContent("+");
    const item = link.closest("li") as HTMLElement;
    expect(within(item).getByTestId("pending-proposal")).toHaveTextContent("Vorschlag offen");
  });

  it("keeps „Speichern“ for maintainers", async () => {
    stubFetch({}, ["Maintainer"]);
    const user = userEvent.setup();
    renderAt("/bible/1/1");
    const panel = await verse2();
    await user.click(screen.getByRole("switch", { name: "Bearbeiten" }));
    await user.click(within(panel).getByRole("link", { name: /^Psalmen 104,30/ }));
    const editor = screen.getByRole("dialog", { name: pen });
    expect(within(editor).getByRole("button", { name: "Speichern" })).toBeInTheDocument();
    expect(within(editor).queryByRole("button", { name: "Vorschlag senden" })).toBeNull();
  });
});
