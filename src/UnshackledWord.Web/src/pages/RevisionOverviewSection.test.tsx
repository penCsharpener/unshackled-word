import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me, stubApi } from "@/test/authHelpers";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { MaintainerPage } from "./MaintainerPage";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <AuthProvider>
          <MaintainerPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const overview = {
  levels: [
    {
      level: 200,
      code: "revision",
      name: "Revised Elberfelder",
      revisedVerses: 2,
      totalVerses: 31102,
      books: [{ bibleBookId: 43, revisedVerses: 2 }],
    },
  ],
  counts: { unreviewed: 1, incomplete: 0, stale: 0 },
  list: {
    name: "unreviewed",
    total: 1,
    page: 1,
    pageSize: 50,
    items: [
      {
        revisionVerseId: 7,
        level: 200,
        version: 1,
        bibleBookId: 43,
        chapter: 3,
        verse: 16,
        createdAt: "2026-11-13T10:00:00Z",
        unlinkedGermanWords: 0,
        unlinkedOriginalWords: 0,
      },
    ],
  },
};

function stub(roles: string[], body: unknown = overview) {
  return stubApi({
    "GET /auth/me": () => json({ ...me, roles }),
    "GET /meta-bible/structure": () => json(fixtureStructure),
    "GET /revision/overview?page=1&pageSize=50&list=unreviewed": () => json(body),
  });
}

describe("RevisionOverviewSection", () => {
  it("shows a tile per level and links an unreviewed row to the verse with the revision open", async () => {
    stub(["maintainer"]);
    renderPage();

    expect(await screen.findByText("2 / 31,102")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Unreviewed mapping 1/ })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    const links = await screen.findAllByRole("link", { name: /Open verse/ });
    expect(links[0]).toHaveAttribute("href", "/bible/43/3?map=16&revise=200#v16");
  });

  it("lets a reviewer read the overview but not export the seed", async () => {
    stub(["reviewer"]);
    renderPage();

    expect(await screen.findByText("2 / 31,102")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Export seed" })).not.toBeInTheDocument();
  });

  it("shows the empty text of a list without entries", async () => {
    stub(["admin"], {
      ...overview,
      counts: { unreviewed: 0, incomplete: 0, stale: 0 },
      list: { ...overview.list, total: 0, items: [] },
    });
    renderPage();

    expect(
      await screen.findByText("Every revised verse has a reviewed mapping."),
    ).toBeInTheDocument();
  });

  it("switches the list when a tile is clicked", async () => {
    const fetchMock = stub(["maintainer"]);
    renderPage();
    await userEvent.click(await screen.findByRole("button", { name: /Words without a link/ }));

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("list=incomplete"),
      expect.anything(),
    );
  });
});
