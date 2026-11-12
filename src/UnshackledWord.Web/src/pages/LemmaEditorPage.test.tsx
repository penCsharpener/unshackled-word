import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import { json, me, stubApi } from "@/test/authHelpers";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { chapterWords, valueMaps } from "@/test/fixtures/lemmas";
import { LemmaEditorPage } from "./LemmaEditorPage";

const chapter = { bookId: 1, chapter: 1, words: chapterWords };
const CHAPTER_URL = "GET /lemmas/chapter?bookId=1&chapter=1";

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter(
    [{ path: "/edit/lemmas/:bookId/:chapter", element: <LemmaEditorPage /> }],
    { initialEntries: ["/edit/lemmas/1/1"] },
  );
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

const baseHandlers = {
  "GET /meta-bible/structure": () => json(fixtureStructure),
  "GET /lemmas/value-maps": () => json(valueMaps),
  [CHAPTER_URL]: () => json(chapter),
};
const cell = (wordId: number, col: string) =>
  document.querySelector<HTMLElement>(`[data-cell="${wordId}:${col}"]`) as HTMLElement;

describe("LemmaEditorPage", () => {
  beforeAll(() => {
    const size = { configurable: true, get: () => 800 };
    Object.defineProperty(HTMLElement.prototype, "clientHeight", size);
    Object.defineProperty(HTMLElement.prototype, "clientWidth", size);
  });
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });
  afterEach(() => vi.unstubAllGlobals());

  it("asks logged-out users to sign in", async () => {
    stubApi({ ...baseHandlers, "GET /auth/me": () => json({}, 401) });
    renderPage();
    expect(await screen.findByText("Anmeldung erforderlich")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Anmelden" })).toBeInTheDocument();
  });

  it("shows the grid read-only without a save bar for other roles", async () => {
    stubApi({ ...baseHandlers, "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }) });
    renderPage();
    await waitFor(() => expect(cell(1, "lemma")).not.toBeNull());
    expect(screen.getByText(/Nur Administratoren und Maintainer/)).toBeInTheDocument();
    cell(1, "lemma").focus();
    await userEvent.keyboard("{Enter}");
    expect(screen.queryByRole("combobox", { name: /Lemma/ })).toBeNull();
    expect(screen.queryByRole("region", { name: "Änderungen" })).toBeNull();
  });

  it("lets a maintainer edit and save a chapter", async () => {
    const fetchMock = stubApi({
      ...baseHandlers,
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "PUT /lemmas/chapter": () =>
        json({ updated: 1, words: [{ ...chapterWords[0], lemma: "Lichter" }] }),
    });
    renderPage();
    await waitFor(() => expect(cell(1, "lemma")).not.toBeNull());
    cell(1, "lemma").focus();
    await userEvent.keyboard("{Enter}");
    const input = await screen.findByRole("combobox", { name: /Lemma/ });
    await userEvent.clear(input);
    await userEvent.type(input, "Lichter{Enter}");

    const bar = await screen.findByRole("region", { name: "Änderungen" });
    expect(within(bar).getByText(/1 Wort geändert/)).toBeInTheDocument();
    await userEvent.click(within(bar).getByRole("button", { name: "Speichern" }));

    await waitFor(() => expect(screen.getByText(/1 Änderung gespeichert/)).toBeInTheDocument());
    const put = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(JSON.parse(put?.[1]?.body as string)).toEqual({
      changes: [{ wordId: 1, lemma: "Lichter", partOfSpeech: "NOUN", number: "S", case: "Dat" }],
    });
    expect(within(cell(1, "lemma")).getByText("Lichter")).toBeInTheDocument();
  });

  it("opens the bulk replace dialog from the toolbar and the Lemma header, for maintainers only", async () => {
    stubApi({ ...baseHandlers, "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }) });
    renderPage();
    await waitFor(() => expect(cell(1, "lemma")).not.toBeNull());
    const buttons = screen.getAllByRole("button", { name: "Lemma ersetzen …" });
    expect(buttons).toHaveLength(2);
    const pencil = buttons.find((b) => b.closest('[role="columnheader"]')) as HTMLElement;
    expect(pencil).toHaveAttribute("tabindex", "-1");
    cell(1, "lemma").focus();
    await userEvent.click(buttons.find((b) => b !== pencil) as HTMLElement);
    const dialog = await screen.findByRole("dialog", { name: "Lemma ersetzen" });
    expect(within(dialog).getByText("Licht")).toBeInTheDocument();
  });

  it("offers no bulk replace without edit rights", async () => {
    stubApi({ ...baseHandlers, "GET /auth/me": () => json({ ...me, roles: ["reviewer"] }) });
    renderPage();
    await waitFor(() => expect(cell(1, "lemma")).not.toBeNull());
    expect(screen.queryByRole("button", { name: "Lemma ersetzen …" })).toBeNull();
  });

  it("keeps the changes and shows an error when saving fails", async () => {
    stubApi({
      ...baseHandlers,
      "GET /auth/me": () => json({ ...me, roles: ["admin"] }),
      "PUT /lemmas/chapter": () => new Response("boom", { status: 500 }),
    });
    renderPage();
    await waitFor(() => expect(cell(3, "lemma")).not.toBeNull());
    cell(3, "lemma").focus();
    await userEvent.keyboard("i");
    await userEvent.keyboard("n{Enter}");
    await userEvent.click(await screen.findByRole("button", { name: "Speichern" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Speichern fehlgeschlagen");
    expect(screen.getByRole("button", { name: "Erneut versuchen" })).toBeInTheDocument();
  });

  it("blocks saving words that have no lemma", async () => {
    stubApi({ ...baseHandlers, "GET /auth/me": () => json({ ...me, roles: ["admin"] }) });
    renderPage();
    await waitFor(() => expect(cell(3, "partOfSpeech")).not.toBeNull());
    cell(3, "partOfSpeech").focus();
    await userEvent.keyboard("v");
    expect(await screen.findByText(/hat kein Lemma oder keine Wortart/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Speichern" })).toBeDisabled();
  });
});
