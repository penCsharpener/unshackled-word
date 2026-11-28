import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "@/components/Toast";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { LxxReviewUnit } from "@/lib/maintainer/lxxReview";
import { estimate, profiles, run } from "@/test/agentFixtures";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { booksResponse, stubDesktop, unitA, unitB, unitC, unitD } from "@/test/lxxReviewFixtures";
import { LxxReviewPage } from "./LxxReviewPage";

type Call = { url: string; method: string; body?: unknown };

interface Options {
  units?: LxxReviewUnit[];
  confirmStatus?: number;
  bibleBookId?: number;
  morphology?: (code: string) => unknown;
}

function stubFetch(opts: Options = {}) {
  const calls: Call[] = [];
  let runCount = 0;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? "GET";
      const body = init?.body ? JSON.parse(String(init.body)) : undefined;
      calls.push({ url, method, body });
      const json = (value: unknown, status = 200) =>
        new Response(JSON.stringify(value), { status });
      if (url.includes("/meta-bible/structure")) return json({ books: fixtureBooks });
      if (url.includes("/auth/me")) {
        return json({ userId: 9, email: "m@b.c", displayName: "Max", roles: ["Maintainer"] });
      }
      if (url.endsWith("/review/books")) return json(booksResponse);
      if (/\/review\/\d+\/\d+$/.test(url)) {
        return json({
          bibleBookId: opts.bibleBookId ?? 8,
          chapter: 1,
          units: opts.units ?? [unitA, unitB, unitC, unitD],
        });
      }
      if (url.endsWith("/review/confirm") && method === "POST") {
        if (opts.confirmStatus && opts.confirmStatus !== 200) {
          return json({ message: "stale" }, opts.confirmStatus);
        }
        return json({
          lxxVerseId: (body as { lxxVerseId: number }).lxxVerseId,
          action: "confirm",
          confirmedWords: 2,
          proposalStatus: "merged",
        });
      }
      if (url.endsWith("/review/confirm-chapter-safe")) {
        return json({
          units: 1,
          confirmedWords: 1,
          skippedLow: 2,
          skippedOther: 0,
          staleUnits: [],
          mergedProposals: 1,
        });
      }
      if (url.includes("/maintainer/lxx/morphology")) {
        const code = decodeURIComponent(url.split("code=")[1] ?? "");
        return json(
          opts.morphology?.(code) ??
            (code === "XXX"
              ? { code, valid: false, error: "unbekannte Bestimmung" }
              : { code, valid: true, german: "Konjunktion", english: "conjunction" }),
        );
      }
      if (url.includes("/maintainer/lxx/lexicon")) return json({ entries: [] });
      if (url.endsWith("/maintainer/agent-runs/profiles")) return json(profiles);
      if (url.endsWith("/maintainer/agent-runs/estimate")) return json(estimate);
      if (url.endsWith("/maintainer/agent-runs") && method === "POST") {
        runCount += 1;
        return json({ ...run, id: 6 + runCount, status: "completed" }, 202);
      }
      const runGet = /\/maintainer\/agent-runs\/(\d+)$/.exec(url);
      if (runGet) return json({ ...run, id: Number(runGet[1]), status: "completed" });
      return json({});
    }),
  );
  return calls;
}

function renderPage(path = "/edit/lxx/8/1") {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: "/edit/lxx/:bookId?/:chapter?", element: <LxxReviewPage /> },
      { path: "/login", element: <p>Anmeldeseite</p> },
    ],
    { initialEntries: [path] },
  );
  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
  return router;
}

const card = async (ref: string) => {
  const heading = await screen.findByRole("heading", { name: ref });
  const article = heading.closest("article");
  if (!article) throw new Error(`no card ${ref}`);
  return within(article);
};
const row = (id: number) => {
  const el = document.getElementById(`lxx-word-${id}`);
  if (!el) throw new Error(`no word row ${id}`);
  return el;
};
const posts = (calls: Call[], suffix: string) =>
  calls.filter((c) => c.method === "POST" && c.url.endsWith(suffix));

beforeEach(async () => {
  await i18n.changeLanguage("de");
  stubDesktop();
});
afterEach(() => vi.unstubAllGlobals());

describe("LxxReviewPage (roadmap 0028.7f)", () => {
  it("refuses a user without a maintainer role", async () => {
    stubFetch();
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) =>
        String(input).includes("/auth/me")
          ? new Response(
              JSON.stringify({ userId: 1, email: "a@b.c", displayName: "A", roles: ["User"] }),
            )
          : new Response("{}"),
      ),
    );
    renderPage();
    expect(await screen.findByRole("alert")).toHaveTextContent("Du hast keinen Zugriff");
  });

  it("redirects to the first open chapter, lists the open units and hides confirmed ones by default", async () => {
    stubFetch();
    const router = renderPage("/edit/lxx");
    expect(await screen.findByRole("heading", { name: "Rut 1:1" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/edit/lxx/8/1");
    expect(screen.getByRole("heading", { name: "Rut 1:3" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Rut 1:5" })).toBeNull();
    expect(screen.getAllByText(/KI · Lauf 3/)).toHaveLength(3);
    expect(screen.getByText("Wort 2: unsicher")).toBeInTheDocument();
    expect(screen.getByRole("link", { current: "page" })).toHaveTextContent("1");
  });

  it("filters: low shows only units with an unsure word, all shows confirmed ones too", async () => {
    stubFetch();
    renderPage("/edit/lxx/8/1?f=low");
    await screen.findByRole("heading", { name: "Rut 1:1" });
    expect(screen.queryByRole("heading", { name: "Rut 1:2" })).toBeNull();
    await userEvent.selectOptions(screen.getByLabelText(/Anzeigen/), "all");
    expect(await screen.findByRole("heading", { name: "Rut 1:5" })).toBeInTheDocument();
  });

  it("validates an edited parse code live and blocks confirm until it is valid", async () => {
    stubFetch();
    renderPage();
    const a = await card("Rut 1:1");
    await userEvent.click(row(1002));
    await userEvent.keyboard("p");
    const code = await a.findByLabelText("Bestimmung");
    expect(code).toHaveFocus();
    await userEvent.clear(code);
    await userEvent.type(code, "XXX");
    expect(await a.findByText(/Ungültige Bestimmung\. unbekannte Bestimmung/)).toBeInTheDocument();
    expect(a.getByRole("button", { name: "Vers bestätigen" })).toBeDisabled();
    expect(a.getByRole("button", { name: "Vers bestätigen" })).toHaveAttribute(
      "title",
      "1 Wörter sind fehlerhaft.",
    );

    await userEvent.clear(code);
    await userEvent.type(code, "conj");
    await userEvent.tab();
    await waitFor(() => expect(code).toHaveValue("CONJ"));
    expect(await a.findByText("Konjunktion")).toBeInTheDocument();
    await waitFor(() => expect(a.getByRole("button", { name: "Vers bestätigen" })).toBeEnabled());
  });

  it("confirm sends every word with the shown state and toasts the result", async () => {
    const calls = stubFetch();
    renderPage();
    const a = await card("Rut 1:1");
    await userEvent.click(a.getByRole("button", { name: "Vers bestätigen" }));
    await waitFor(() => expect(posts(calls, "/review/confirm")).toHaveLength(1));
    expect(posts(calls, "/review/confirm")[0]?.body).toEqual({
      lxxVerseId: 101,
      proposalId: 11,
      words: [
        {
          lxxWordId: 1001,
          lemma: "καί",
          strongs: "G2532",
          morphology: "CONJ",
          hebrewWordIds: [501],
        },
        {
          lxxWordId: 1002,
          lemma: "γίνομαι",
          strongs: "G1096",
          morphology: "V-2ADI-3S",
          hebrewWordIds: [502],
        },
      ],
    });
    expect(await screen.findByText("Vers bestätigt (2 Wörter).")).toBeInTheDocument();
    expect(await a.findByText("gespeichert")).toBeInTheDocument();
  });

  it("a Hebrew click toggles the link of the active word and confirm sends it", async () => {
    const calls = stubFetch();
    renderPage();
    const b = await card("Rut 1:2");
    await userEvent.click(row(1003));
    const hebrew = b.getByRole("button", { name: /שֵׁם/ });
    expect(hebrew).toHaveAttribute("aria-pressed", "true");
    await userEvent.click(hebrew);
    expect(hebrew).toHaveAttribute("aria-pressed", "false");
    await userEvent.click(b.getByRole("button", { name: "Vers bestätigen" }));
    await waitFor(() => expect(posts(calls, "/review/confirm")).toHaveLength(1));
    const sent = posts(calls, "/review/confirm")[0]?.body as { words: unknown[] };
    expect(sent.words).toEqual([
      { lxxWordId: 1003, lemma: "ὄνομα", strongs: "G3686", morphology: "N-NSN", hebrewWordIds: [] },
    ]);
  });

  it("shows the conflict with a reload button when the server answers 409", async () => {
    stubFetch({ confirmStatus: 409 });
    renderPage();
    const a = await card("Rut 1:1");
    await userEvent.click(a.getByRole("button", { name: "Vers bestätigen" }));
    expect(
      await a.findByText("Der KI-Vorschlag wurde inzwischen ersetzt oder geschlossen."),
    ).toBeInTheDocument();
    expect(a.getByRole("button", { name: "Neu laden" })).toBeInTheDocument();
    expect(a.getByRole("button", { name: "Vers bestätigen" })).toBeDisabled();
  });

  it("safe confirm leaves out low words and dirty verses and posts only the safe words", async () => {
    const calls = stubFetch();
    renderPage();
    const b = await card("Rut 1:2");
    await userEvent.click(row(1003));
    await userEvent.click(b.getByRole("button", { name: /שֵׁם/ })); // verse B is now dirty
    await userEvent.click(screen.getByRole("button", { name: "Kapitel bestätigen (sichere)" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("1 sichere Wörter in 1 Versen bestätigen.");
    expect(dialog).toHaveTextContent("2 unsichere Wörter bleiben offen.");
    expect(dialog).toHaveTextContent("1 Verse mit ungespeicherten Änderungen werden ausgelassen.");
    await userEvent.click(within(dialog).getByRole("button", { name: "Bestätigen" }));
    await waitFor(() => expect(posts(calls, "/review/confirm-chapter-safe")).toHaveLength(1));
    expect(posts(calls, "/review/confirm-chapter-safe")[0]?.body).toEqual({
      bibleBookId: 8,
      chapter: 1,
      units: [{ lxxVerseId: 101, proposalId: 11, lxxWordIds: [1001] }],
    });
  });

  it("a selection with a gap becomes one estimate and one run per block, started one after the other", async () => {
    const calls = stubFetch();
    renderPage();
    await userEvent.click(await screen.findByRole("checkbox", { name: "Rut 1:1" }));
    await userEvent.click(screen.getByRole("checkbox", { name: "Rut 1:3" }));
    expect(screen.getByText(/2 Verse gewählt, 2 Lauf/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "KI neu für Auswahl (2)" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("2 zusammenhängende Verse-Blöcke");
    await waitFor(() => expect(posts(calls, "/maintainer/agent-runs/estimate")).toHaveLength(2), {
      timeout: 2500,
    });
    expect(posts(calls, "/maintainer/agent-runs/estimate").map((c) => c.body)).toEqual([
      {
        book: 8,
        fromChapter: 1,
        fromVerse: 1,
        toChapter: 1,
        toVerse: 1,
        profile: "ollama-cloud",
        mode: "lxx-tag",
      },
      {
        book: 8,
        fromChapter: 1,
        fromVerse: 3,
        toChapter: 1,
        toVerse: 3,
        profile: "ollama-cloud",
        mode: "lxx-tag",
      },
    ]);
    // the summed estimate: two blocks of 3 packages
    expect(await within(dialog).findByText("6")).toBeInTheDocument();

    await userEvent.click(within(dialog).getByLabelText("auch bestätigte Verse neu taggen"));
    await waitFor(() => expect(posts(calls, "/maintainer/agent-runs/estimate")).toHaveLength(4), {
      timeout: 2500,
    });
    expect(posts(calls, "/maintainer/agent-runs/estimate")[3]?.body).toMatchObject({
      includeConfirmed: true,
      fromVerse: 3,
    });
    const start = within(dialog).getByRole("button", { name: "Lauf starten" });
    await waitFor(() => expect(start).toBeEnabled(), { timeout: 2500 });
    await userEvent.click(start);
    await waitFor(() => expect(posts(calls, "/maintainer/agent-runs")).toHaveLength(2), {
      timeout: 4000,
    });
    expect(posts(calls, "/maintainer/agent-runs").map((c) => c.body)).toMatchObject([
      { fromVerse: 1, toVerse: 1, includeConfirmed: true, mode: "lxx-tag" },
      { fromVerse: 3, toVerse: 3, includeConfirmed: true, mode: "lxx-tag" },
    ]);
  }, 15_000);

  it("a Daniel run carries the text form shown on screen", async () => {
    const calls = stubFetch({
      bibleBookId: 27,
      units: [
        { ...unitA, textForm: "og", ref: "Dan 1:1 (og)" },
        { ...unitB, textForm: "th", ref: "Dan 1:1 (th)", verse: 1 },
      ],
    });
    renderPage("/edit/lxx/27/1?form=og");
    expect(await screen.findByRole("heading", { name: "Dan 1:1 (og)" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Dan 1:1 (th)" })).toBeNull();
    await userEvent.click(screen.getByRole("checkbox", { name: "Dan 1:1 (og)" }));
    await userEvent.click(screen.getByRole("button", { name: /KI neu für Auswahl/ }));
    await waitFor(() => expect(posts(calls, "/maintainer/agent-runs/estimate")).toHaveLength(1), {
      timeout: 2500,
    });
    expect(posts(calls, "/maintainer/agent-runs/estimate")[0]?.body).toMatchObject({
      book: 27,
      mode: "lxx-tag",
      textForm: "og",
    });
  });

  it("moves the active word with j and toggles Hebrew words with digit keys", async () => {
    stubFetch();
    renderPage();
    const a = await card("Rut 1:1");
    await userEvent.click(row(1001));
    expect(document.getElementById("lxx-word-1001")).toHaveAttribute("aria-current", "true");
    await userEvent.keyboard("j");
    await waitFor(() =>
      expect(document.getElementById("lxx-word-1002")).toHaveAttribute("aria-current", "true"),
    );
    await userEvent.keyboard("1");
    await waitFor(() =>
      expect(a.getByRole("button", { name: /וַיְהִי/ })).toHaveAttribute("aria-pressed", "true"),
    );
    await userEvent.keyboard("x");
    expect(a.getByRole("checkbox", { name: "Rut 1:1" })).toBeChecked();
  });

  it("shows the Elberfelder verse under the Hebrew and marks the words of the hovered Hebrew word", async () => {
    stubFetch();
    renderPage();
    const a = await card("Rut 1:1");
    const german = a.getByTestId("lxx-german-verse");
    expect(german).toHaveTextContent("Elberfelder 1871: Und es geschah in den Tagen,");
    expect(german.querySelectorAll("mark")).toHaveLength(0);

    await userEvent.hover(a.getByRole("button", { name: /וַיְהִי/ }));
    expect(await screen.findByRole("tooltip")).toHaveTextContent("Deutsch: Und es geschah");
    expect([...german.querySelectorAll("mark")].map((m) => m.textContent)).toEqual([
      "Und",
      "es",
      "geschah",
    ]);

    await userEvent.unhover(a.getByRole("button", { name: /וַיְהִי/ }));
    await waitFor(() => expect(german.querySelectorAll("mark")).toHaveLength(0));
  });

  it("underlines the linked Hebrew and German words while a Greek row is hovered", async () => {
    stubFetch();
    renderPage();
    const a = await card("Rut 1:1");
    const german = a.getByTestId("lxx-german-verse");
    const underlined = () => german.querySelectorAll("span.underline");
    expect(underlined()).toHaveLength(0);

    const row = document.getElementById("lxx-word-1001") as HTMLElement;
    await userEvent.hover(row);
    await waitFor(() =>
      expect(a.getByRole("button", { name: /וַיְהִי/ })).toHaveClass("border-accent"),
    );
    expect([...underlined()].map((m) => m.textContent)).toEqual(["Und", "es", "geschah"]);

    await userEvent.unhover(row);
    await waitFor(() => expect(underlined()).toHaveLength(0));
  });

  it("shows no German verse for a unit without one", async () => {
    stubFetch();
    renderPage();
    const b = await card("Rut 1:2");
    expect(b.queryByTestId("lxx-german-verse")).toBeNull();
  });
});
