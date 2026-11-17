import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { BibleStructureBook, GetVerseMappingResponse } from "@/lib/bible/types";
import { estimate, profiles } from "@/test/agentFixtures";
import { me } from "@/test/authHelpers";
import { fixtureBooks, fixtureChapterWords } from "@/test/fixtures/bibleStructure";
import { ChapterPage } from "./ChapterPage";

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

const mapping: GetVerseMappingResponse = {
  bibleBookId: 43,
  chapter: 3,
  verse: 1,
  language: "greek",
  editingEnabled: true,
  germanWords: [
    { id: 1, positionInVerse: 1, text: "Es" },
    { id: 4, positionInVerse: 2, text: "Gerstenernte" },
  ],
  originalWords: [
    { id: 100, position: 1, original: "ἦν" },
    { id: 101, position: 2, original: "θερισμός" },
  ],
  links: [
    { elbWordId: 4, stepWordId: 100, isAddedWord: false, partOrder: 1, germanWordPart: "Gersten" },
    { elbWordId: 4, stepWordId: 101, isAddedWord: false, partOrder: 2, germanWordPart: "ernte" },
  ],
};

function stubFetch(roles: string[] | null) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.includes("/auth/me")) {
      return roles
        ? new Response(JSON.stringify({ ...me, roles }))
        : new Response("", { status: 401 });
    }
    if (url.includes("/meta-bible/structure")) return new Response(JSON.stringify({ books }));
    if (url.includes("/revision/levels")) return new Response("[]");
    if (url.includes("/maintainer/agent-runs/profiles"))
      return new Response(JSON.stringify(profiles));
    if (url.includes("/maintainer/agent-runs/estimate"))
      return new Response(JSON.stringify(estimate));
    if (url.includes("/verseMapping/")) {
      if (init?.method === "PUT") return new Response(JSON.stringify(mapping));
      return new Response(JSON.stringify({ ...mapping, verse: Number(url.split("/verseId/")[1]) }));
    }
    if (url.includes("chapterWords")) return new Response(JSON.stringify(fixtureChapterWords));
    return new Response("{}", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const callsTo = (fetchMock: ReturnType<typeof stubFetch>, part: string, method?: string) =>
  fetchMock.mock.calls.filter(
    ([url, init]) => String(url).includes(part) && (!method || init?.method === method),
  );

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [{ path: "/bible/:bookId/:chapter", element: <ChapterPage /> }],
    { initialEntries: ["/bible/43/3"] },
  );
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

const openAlign = (verse: number) =>
  fireEvent.click(
    screen.getByRole("button", { name: new RegExp(`Grundtext von Vers ${verse}\\b`) }),
    {
      ctrlKey: true,
    },
  );

beforeEach(async () => {
  localStorage.clear();
  localStorage.setItem("uw.readerLevel", "200");
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  await i18n.changeLanguage("de");
});
afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("ChapterPage: KI neu zuordnen", () => {
  it("is not offered to anonymous readers, neither in the header nor in the mapping editor", async () => {
    stubFetch(null);
    renderPage();
    await screen.findByText("Dieser");
    expect(screen.queryByRole("button", { name: /KI neu zuordnen/ })).toBeNull();
    openAlign(1);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    expect(screen.queryByRole("button", { name: /KI neu zuordnen/ })).toBeNull();
  });

  it("a maintainer starts it for the whole chapter from the header", async () => {
    const fetchMock = stubFetch(["maintainer"]);
    renderPage();
    await screen.findByText("Dieser");
    await userEvent.click(await screen.findByRole("button", { name: "KI neu zuordnen …" }));
    const dialog = await screen.findByRole("dialog", { name: "KI neu zuordnen" });
    expect(within(dialog).getByLabelText("Von Kapitel")).toHaveValue(3);
    expect(within(dialog).getByLabelText("Von Vers")).toHaveValue(1);
    expect(within(dialog).getByLabelText("Bis Kapitel")).toHaveValue(3);
    expect(within(dialog).getByLabelText("Bis Vers")).toHaveValue(null);
    await waitFor(
      () => expect(callsTo(fetchMock, "/agent-runs/estimate", "POST")).toHaveLength(1),
      {
        timeout: 2000,
      },
    );
    expect(
      JSON.parse(String(callsTo(fetchMock, "/agent-runs/estimate")[0]?.[1]?.body)),
    ).toMatchObject({
      book: 43,
      fromChapter: 3,
      fromVerse: 1,
      toChapter: 3,
    });
    await userEvent.click(within(dialog).getByRole("button", { name: "Abbrechen" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("a maintainer starts it for one verse from the mapping editor", async () => {
    const fetchMock = stubFetch(["maintainer"]);
    renderPage();
    await screen.findByText("Dieser");
    await screen.findByRole("button", { name: "KI neu zuordnen …" });
    openAlign(2);
    await screen.findByRole("region", { name: "Zuordnung Johannes 3,2" });
    await userEvent.click(screen.getByRole("button", { name: /KI neu zuordnen \(dieser Vers/ }));
    const dialog = await screen.findByRole("dialog", { name: "KI neu zuordnen" });
    expect(within(dialog).getByLabelText("Von Vers")).toHaveValue(2);
    expect(within(dialog).getByLabelText("Bis Vers")).toHaveValue(2);
    await waitFor(
      () => expect(callsTo(fetchMock, "/agent-runs/estimate", "POST")).toHaveLength(1),
      {
        timeout: 2000,
      },
    );
    expect(
      JSON.parse(String(callsTo(fetchMock, "/agent-runs/estimate")[0]?.[1]?.body)),
    ).toMatchObject({
      fromVerse: 2,
      toVerse: 2,
    });
  });
});

describe("ChapterPage: compound parts", () => {
  it("shows the parts as joined tiles and sends them back on save without clearParts", async () => {
    const fetchMock = stubFetch(["maintainer"]);
    renderPage();
    await screen.findByText("Dieser");
    openAlign(1);
    const editor = await screen.findByRole("region", { name: "Zuordnung Johannes 3,1" });
    expect(editor.querySelector('[data-node="g4.1"]')).toHaveTextContent("Gersten");
    expect(editor.querySelector('[data-node="g4.2"]')).toHaveTextContent("ernte");
    await userEvent.click(editor.querySelector('[data-node="g1"]') as HTMLElement);
    await userEvent.click(editor.querySelector('[data-node="o100"]') as HTMLElement);
    await userEvent.click(screen.getByRole("button", { name: "Speichern (1)" }));
    await waitFor(() => expect(callsTo(fetchMock, "/verseMapping/", "PUT")).toHaveLength(1));
    const body = JSON.parse(String(callsTo(fetchMock, "/verseMapping/", "PUT")[0]?.[1]?.body));
    expect(body.clearParts).toBeUndefined();
    expect(body.links).toContainEqual({
      elbWordId: 4,
      stepWordId: 101,
      isAddedWord: false,
      partOrder: 2,
      germanWordPart: "ernte",
    });
  });
});
