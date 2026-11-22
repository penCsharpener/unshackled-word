import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me, stubApi } from "@/test/authHelpers";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { VocabDetailPage } from "./VocabDetailPage";

const logos = {
  entry: {
    id: 56,
    track: "G",
    rank: 87,
    lemma: "λόγος",
    transliteration: "logos",
    dictionaryForm: "λόγος, -ου, ὁ",
    occurrences: 330,
    isProperName: false,
    reviewed: true,
    hasAudio: false,
  },
  strongsKeys: [{ strongsKey: "G3056", occurrences: 330 }],
  meanings: [],
  renderings: [
    {
      germanKey: "wort",
      germanDisplay: "Wort",
      linkCount: 220,
      fromRevisionCount: 200,
      share: 66.7,
      posTags: ["NN"],
      keyIsForm: false,
    },
  ],
  renderingsTotal: 1,
  totalLinks: 330,
  refreshedAt: "2026-11-21T09:14:00",
  sampleVerses: [
    {
      bibleBookId: 43,
      chapter: 1,
      verse: 1,
      fromRevision: true,
      humanMade: false,
      original: [
        { position: 1, text: "Ἐν", isHit: false },
        { position: 5, text: "λόγος", isHit: true },
      ],
      german: [
        { position: 1, text: "Im", isHit: false },
        { position: 4, text: "Wort", isHit: true },
      ],
    },
  ],
};

function renderAt(route = "/vocab/56") {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <MemoryRouter initialEntries={[route]}>
          <Routes>
            <Route path="/vocab/:id" element={<VocabDetailPage />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("VocabDetailPage", () => {
  it("shows the lemma and how often each German rendering occurs", async () => {
    stubApi({
      "GET /vocab/entries/56": () => json(logos),
      "GET /meta-bible/structure": () => json(fixtureStructure),
    });
    renderAt();
    const heading = await screen.findByRole("heading", { level: 1 });
    expect(heading).toHaveTextContent("λόγος");
    expect(screen.getByText("220")).toBeInTheDocument();
    expect(screen.getAllByText("Wort").length).toBeGreaterThan(0);
    expect(screen.getByText("200 of them from 2027")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "G3056" })).toHaveAttribute(
      "href",
      "/bible/strongs/G3056",
    );
    expect(screen.getByRole("link", { name: /1,1$/ })).toHaveAttribute("href", "/bible/43/1#v1");
  });

  it("lists the accepted meanings in rank order with the derivation note", async () => {
    stubApi({
      "GET /vocab/entries/56": () =>
        json({
          ...logos,
          meanings: [
            { german: "Wort", rank: 1 },
            { german: "Rede", rank: 2 },
          ],
        }),
      "GET /meta-bible/structure": () => json(fixtureStructure),
    });
    renderAt();
    const section = (await screen.findByRole("heading", { name: "Meanings to learn" })).closest(
      "section",
    ) as HTMLElement;
    expect(
      within(section)
        .getAllByRole("listitem")
        .map((item) => item.textContent),
    ).toEqual(["Wort", "Rede"]);
    expect(
      within(section).getByText(
        "Derived from the Elberfelder (2027, otherwise 1871). All meanings count as correct in the quiz.",
      ),
    ).toBeInTheDocument();
  });

  it("offers the editor link only to maintainers", async () => {
    const stubs = (roles: string[]) =>
      stubApi({
        "GET /auth/me": () => json({ ...me, roles }),
        "GET /vocab/entries/56": () => json(logos),
        "GET /meta-bible/structure": () => json(fixtureStructure),
      });
    stubs(["maintainer"]);
    const { unmount } = renderAt();
    expect(await screen.findByRole("link", { name: "Edit" })).toHaveAttribute(
      "href",
      "/edit/vocab/56",
    );
    unmount();
    stubs([]);
    renderAt();
    await screen.findByRole("heading", { level: 1 });
    expect(screen.queryByRole("link", { name: "Edit" })).not.toBeInTheDocument();
  });

  it("says so when the entry does not exist", async () => {
    stubApi({
      "GET /vocab/entries/999": () => new Response("", { status: 404 }),
    });
    renderAt("/vocab/999");
    expect(await screen.findByText("This entry does not exist.")).toBeInTheDocument();
  });
});
