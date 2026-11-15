import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { BULK_REPLACE_KEY } from "@/lib/revision/bulkReplace";
import { json, me, stubApi } from "@/test/authHelpers";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { BulkReplacePage } from "./BulkReplacePage";

beforeEach(() => {
  localStorage.clear();
  i18n.changeLanguage("en");
});
afterEach(() => vi.unstubAllGlobals());

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <AuthProvider>
          <BulkReplacePage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const levels = [
  { level: 200, code: "elb1871", name: "Elberfelder 1871", isBase: true, isDefault: false },
  { level: 200, code: "elb2027", name: "Elberfelder 2027", isBase: false, isDefault: true },
];

const hit = (hebRefId: number, found: string, over: object = {}) => ({
  hebRefId,
  occurrence: 1,
  positionInVerse: 4,
  found,
  contextBefore: "… zu einem",
  contextAfter: "und brachte",
  strongs: ["H802"],
  targetDiffers: false,
  ...over,
});

const searchResponse = {
  lemmas: [],
  total: 2,
  capped: false,
  facets: [{ strongs: "H802", count: 2 }],
  rows: [hit(1002022, "Weibe", { articleBefore: "einem" }), hit(1003006, "Weib")],
};

const versePath = "/revision/verse/bookId/1/chapterId/2/verseId/22";

function stub(roles: string[] = ["maintainer"]) {
  return stubApi({
    "GET /auth/me": () => json({ ...me, roles }),
    "GET /meta-bible/structure": () => json(fixtureStructure),
    "GET /revision/levels": () => json(levels),
    "POST /revision/bulkReplace/search": () => json(searchResponse),
    [`GET ${versePath}`]: () =>
      json({ baseText: "und baute zu einem Weibe und brachte", variants: [] }),
    [`POST ${versePath}`]: () =>
      json({
        revisionVerseId: 9,
        level: 200,
        version: 1,
        carry: { same: 5, changed: 1, new: 0, lostOriginalWordIds: [] },
      }),
    "GET /revision/verseMapping/9": () =>
      json({
        revisionVerseId: 9,
        level: 200,
        version: 1,
        bibleBookId: 1,
        chapter: 2,
        verse: 22,
        language: "hebrew",
        germanWords: [],
        originalWords: [],
        links: [],
      }),
  });
}

describe("BulkReplacePage", () => {
  it("asks for sign-in or the write role", async () => {
    stub(["reviewer"]);
    renderPage();
    expect(await screen.findByRole("alert")).toBeInTheDocument();
  });

  it("searches, lists the hits and keeps only checked verses for the review", async () => {
    const fetchMock = stub();
    const user = userEvent.setup();
    renderPage();
    await user.type(await screen.findByRole("searchbox", { name: "Word" }), "Weib");
    await user.type(screen.getByRole("textbox", { name: "Suggested replacement" }), "Frau");
    await user.click(screen.getByRole("button", { name: "Search" }));

    expect(await screen.findByText("2 hits · 2 selected")).toBeInTheDocument();
    const call = fetchMock.mock.calls.find((c) => String(c[0]).includes("bulkReplace/search"));
    expect(JSON.parse(String(call?.[1]?.body))).toEqual({
      word: "Weib",
      sameLemma: false,
      sourceRevision: "elb2027",
      targetRevision: "elb2027",
    });
    expect(screen.getByText(/check the article before it: “einem”/)).toBeInTheDocument();

    await user.click(screen.getByRole("checkbox", { name: /Gen 3,6, hit 1/ }));
    expect(screen.getByText("2 hits · 1 selected")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Review (1 verses)" }));

    // step 2: the suggestion is inserted into the full verse text
    const text = await screen.findByRole("textbox", { name: "Verse text (Elberfelder 2027)" });
    await waitFor(() => expect(text).toHaveValue("und baute zu einem Frau und brachte"));
  });

  it("saves the edited text, then shows the mapping step and marks the verse reviewed", async () => {
    const fetchMock = stub();
    const user = userEvent.setup();
    renderPage();
    await user.type(await screen.findByRole("searchbox", { name: "Word" }), "Weib");
    await user.type(screen.getByRole("textbox", { name: "Suggested replacement" }), "Frau");
    await user.click(screen.getByRole("button", { name: "Search" }));
    await screen.findByText("2 hits · 2 selected");
    await user.click(screen.getByRole("checkbox", { name: /Gen 3,6, hit 1/ }));
    await user.click(screen.getByRole("button", { name: "Review (1 verses)" }));

    const text = await screen.findByRole("textbox", { name: "Verse text (Elberfelder 2027)" });
    await waitFor(() => expect(text).toHaveValue("und baute zu einem Frau und brachte"));
    await user.clear(text);
    await user.type(text, "und baute zu einer Frau und brachte");
    await user.click(screen.getByRole("button", { name: "Continue to mapping" }));

    expect(
      await screen.findByText(/saved as a new version of “Elberfelder 2027”/),
    ).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(
      (c) => c[1]?.method === "POST" && String(c[0]).endsWith(versePath),
    );
    expect(JSON.parse(String(post?.[1]?.body))).toMatchObject({
      level: 200,
      text: "und baute zu einer Frau und brachte",
      basedOnLevel: 0,
    });
    const stored = JSON.parse(localStorage.getItem(BULK_REPLACE_KEY) ?? "{}");
    expect(stored.queue[0].status).toBe("saved");
  });

  it("offers to continue an open queue from an earlier session", async () => {
    localStorage.setItem(
      BULK_REPLACE_KEY,
      JSON.stringify({
        filter: {
          word: "Weib",
          sameLemma: false,
          source: "elb2027",
          target: "elb2027",
          suggestion: "Frau",
        },
        queue: [
          { hebRefId: 1002022, forms: ["Weibe"], occurrences: [1], strongs: [], status: "pending" },
          { hebRefId: 1003006, forms: ["Weib"], occurrences: [1], strongs: [], status: "reviewed" },
        ],
      }),
    );
    stub();
    renderPage();
    expect(
      await screen.findByText("1 verses are still open from an earlier session."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Continue" })).toBeInTheDocument();
  });
});
