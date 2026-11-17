import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { GetStrongsSummaryResponse, GetWordStatsResponse } from "@/lib/bible/types";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import {
  fixtureSnapshotSummary,
  fixtureSummary,
  fixtureWordStats,
} from "@/test/fixtures/strongsStats";
import { StrongsStatsHeader, type StrongsStatsHeaderProps } from "./StrongsStatsHeader";

interface Responses {
  me?: () => Response;
  refresh?: () => Response;
  summary?: () => Response;
  wordStats?: (url: string) => Response;
}

function stubFetch({ summary, wordStats, me, refresh }: Responses = {}) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.includes("/auth/me")) return me?.() ?? new Response("", { status: 401 });
    if (url.includes("/dataQuality/refresh") && init?.method === "POST") {
      return refresh?.() ?? new Response(JSON.stringify({ refreshing: true }), { status: 202 });
    }
    if (url.includes("/meta-bible/structure"))
      return new Response(JSON.stringify(fixtureStructure));
    if (url.includes("/strongsSummary/")) {
      return summary?.() ?? new Response(JSON.stringify(fixtureSummary));
    }
    if (url.includes("/wordStats")) {
      return wordStats?.(url) ?? new Response(JSON.stringify(fixtureWordStats));
    }
    return new Response("", { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

const json = (body: GetStrongsSummaryResponse | GetWordStatsResponse) =>
  new Response(JSON.stringify(body));

function renderHeader(props: Partial<StrongsStatsHeaderProps> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const handlers = { onSelectBook: vi.fn(), onSelectRendering: vi.fn() };
  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <StrongsStatsHeader strongs="G26" {...handlers} {...props} />
      </AuthProvider>
    </QueryClientProvider>,
  );
  return handlers;
}

const statsCalls = (fetchMock: ReturnType<typeof stubFetch>) =>
  fetchMock.mock.calls.map(([url]) => String(url)).filter((url) => url.includes("/wordStats"));

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("StrongsStatsHeader", () => {
  it("shows the roles notice until the backfill ran and lists the renderings with the contraction row", async () => {
    stubFetch({
      summary: () =>
        json({
          ...fixtureSummary,
          renderings: [
            ...fixtureSummary.renderings,
            { word: "", key: "", count: 4, noOwnLemma: true },
          ],
        }),
    });
    renderHeader();
    expect(await screen.findByTestId("roles-incomplete")).toHaveTextContent(
      "Rollen unvollständig – Zählungen enthalten noch Hilfsverben und Präfixe",
    );
    expect(screen.getByTestId("strongs-renderings")).toHaveTextContent(
      "Liebe (90) · Lieben (20) · ohne eigenes Lemma (4)",
    );
  });

  it("hides the roles notice once linkRolesAppliedAt is set", async () => {
    stubFetch({
      summary: () => json({ ...fixtureSummary, linkRolesAppliedAt: "2026-11-16T10:00:00Z" }),
    });
    renderHeader();
    await screen.findByTestId("strongs-renderings");
    expect(screen.queryByTestId("roles-incomplete")).not.toBeInTheDocument();
  });

  it("shows heading, glosses, stat line and the formed-from placeholder", async () => {
    const fetchMock = stubFetch();
    renderHeader();

    await screen.findByText("Griechisch", { exact: false });
    const heading = screen.getByRole("heading", { level: 1 });
    expect(heading).toHaveTextContent("Strong G26 – ἀγάπη – agapē");
    expect(within(heading).getByText("ἀγάπη")).toHaveAttribute("lang", "el");
    expect(screen.getByText(/^Griechisch/)).toHaveTextContent("Griechisch – Liebe (love)");
    expect(screen.getByText("love")).toHaveAttribute("lang", "en");
    expect(screen.getByText("116 Vorkommen · 110 Verse · 2 von 2 Büchern")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Gebildet aus" })).toBeInTheDocument();
    expect(screen.getByText("Die Wortbildung wird noch aufbereitet …")).toBeInTheDocument();
    expect(
      fetchMock.mock.calls.some(([url]) =>
        String(url).endsWith("/elberfelder/strongsSummary/strongs/G26"),
      ),
    ).toBe(true);
  });

  it("charts the books and the renderings and loads the originals per testament", async () => {
    const fetchMock = stubFetch();
    renderHeader();

    expect(
      await screen.findByRole("region", { name: "Vorkommen im Neuen Testament" }),
    ).toBeVisible();
    const columns = screen.getByRole("list", { name: "Neues Testament: Vorkommen je Buch" });
    expect(within(columns).getByText("Matthäus: 80")).toBeInTheDocument();

    expect(await screen.findByRole("heading", { name: "G26 – ἀγάπη" })).toBeVisible();
    expect(screen.getByText("116 Vorkommen in 110 Versen")).toBeVisible();
    expect(screen.getByText("3 Vorkommen ohne Zuordnung zum Elberfelder Text.")).toBeVisible();

    expect(
      await screen.findByRole("heading", { name: "»Liebe« im Neuen Testament" }),
    ).toBeVisible();
    expect(screen.getByRole("heading", { name: "»Liebe« im Alten Testament" })).toBeVisible();
    await waitFor(() =>
      expect(statsCalls(fetchMock).sort()).toEqual([
        expect.stringMatching(/wordStats\?word=Liebe&testament=nt$/),
        expect.stringMatching(/wordStats\?word=Liebe&testament=ot$/),
      ]),
    );
    expect((await screen.findAllByText("H160")).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/von 116 Mal/).length).toBeGreaterThan(0);
  });

  it("follows the selected rendering and reports bar and slice clicks", async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch();
    const handlers = renderHeader({ rendering: "lieben" });

    expect(
      await screen.findByRole("heading", { name: "»Lieben« im Neuen Testament" }),
    ).toBeVisible();
    expect(statsCalls(fetchMock).every((url) => url.includes("word=Lieben"))).toBe(true);

    const columns = screen.getByRole("list", { name: "Neues Testament: Vorkommen je Buch" });
    await user.click(within(columns).getByRole("button", { name: "Offenbarung: 36" }));
    expect(handlers.onSelectBook).toHaveBeenCalledWith(66);

    await user.click(screen.getByRole("button", { name: "Liebe" }));
    expect(handlers.onSelectRendering).toHaveBeenCalledWith("liebe");
    await user.click(screen.getByRole("button", { name: "Lieben" }));
    expect(handlers.onSelectRendering).toHaveBeenLastCalledWith(null);
  });

  it("marks the book filter as pressed", async () => {
    stubFetch();
    renderHeader({ bookId: 40 });
    const columns = await screen.findByRole("list", { name: "Neues Testament: Vorkommen je Buch" });
    expect(within(columns).getByRole("button", { name: "Matthäus: 80" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("sets Hebrew text right-to-left", async () => {
    stubFetch({
      summary: () =>
        json({ ...fixtureSummary, strongs: "H430", language: "hebrew", lemma: "אֱלֹהִים" }),
    });
    renderHeader({ strongs: "H430" });
    expect((await screen.findAllByText("אֱלֹהִים"))[0]).toHaveAttribute("dir", "rtl");
    expect(screen.getByText("Hebräisch", { exact: false })).toBeInTheDocument();
  });

  it("shows a loading state", () => {
    stubFetch({ summary: () => new Response(new ReadableStream()) });
    renderHeader();
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Strong G26");
    expect(screen.getByText("Statistik wird geladen …")).toBeInTheDocument();
  });

  it("shows an error and retries", async () => {
    const user = userEvent.setup();
    let calls = 0;
    stubFetch({
      summary: () => (++calls === 1 ? new Response("", { status: 500 }) : json(fixtureSummary)),
    });
    renderHeader();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Die Statistik konnte nicht geladen werden.",
    );
    await user.click(screen.getByRole("button", { name: "Erneut versuchen" }));
    expect(await screen.findByRole("heading", { level: 1, name: /ἀγάπη/ })).toBeInTheDocument();
  });

  it("shows an empty state for a number without occurrences", async () => {
    stubFetch({
      summary: () =>
        json({
          ...fixtureSummary,
          occurrenceCount: 0,
          verseCount: 0,
          mappedCount: 0,
          unmappedCount: 0,
          byBook: [],
          renderings: [],
        }),
    });
    renderHeader();
    expect(
      await screen.findByText("Zu dieser Nummer gibt es keine Vorkommen im Bibeltext."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: /Vorkommen im/ })).not.toBeInTheDocument();
  });

  it("shows an empty donut for a testament without the word", async () => {
    stubFetch({
      wordStats: (url) =>
        url.endsWith("testament=ot")
          ? json({ ...fixtureWordStats, testament: "ot", occurrenceCount: 0, originals: [] })
          : json(fixtureWordStats),
    });
    renderHeader();
    expect(await screen.findByText("Kein Vorkommen von »Liebe« im Alten Testament.")).toBeVisible();
    expect(
      await screen.findByRole("heading", { name: "»Liebe« im Neuen Testament" }),
    ).toBeVisible();
  });

  it("shows an error with retry for one donut", async () => {
    const user = userEvent.setup();
    let otCalls = 0;
    stubFetch({
      wordStats: (url) => {
        if (!url.endsWith("testament=ot")) return json(fixtureWordStats);
        return ++otCalls === 1
          ? new Response("", { status: 500 })
          : json({ ...fixtureWordStats, testament: "ot" });
      },
    });
    renderHeader();

    expect(
      await screen.findByText("Die Statistik zu »Liebe« konnte nicht geladen werden."),
    ).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Erneut versuchen" }));
    expect(
      await screen.findByRole("heading", { name: "»Liebe« im Alten Testament" }),
    ).toBeVisible();
  });

  it("speaks English", async () => {
    await i18n.changeLanguage("en");
    stubFetch();
    renderHeader();
    expect(await screen.findByRole("heading", { name: "Formed from" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Strong's G26");
    expect(
      await screen.findByRole("heading", { name: "“Liebe” in the New Testament" }),
    ).toBeVisible();
  });
});

const asRole = (role: string) => () =>
  json({ userId: 1, email: "m@example.org", displayName: "M", roles: [role] } as never);

describe("StrongsStatsHeader snapshot stats", () => {
  it("shows lemma and part of speech per rendering and the snapshot date", async () => {
    stubFetch({ summary: () => json(fixtureSnapshotSummary) });
    renderHeader();

    expect(await screen.findByRole("button", { name: "Liebe, Nomen" })).toBeVisible();
    expect(screen.getByRole("button", { name: "lieben, Verb" })).toBeVisible();
    // The same lemma with two parts of speech is two rows.
    expect(screen.getByRole("button", { name: "lieben, Nomen" })).toBeVisible();
    expect(screen.getByText(/^Statistik: Stand /)).toBeVisible();
    expect(screen.queryByRole("button", { name: "Neu berechnen" })).not.toBeInTheDocument();
  });

  it("reports the lemma key for a slice and loads the word stats with its part of speech", async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch({ summary: () => json(fixtureSnapshotSummary) });
    const handlers = renderHeader({ rendering: "lieben" });

    await user.click(await screen.findByRole("button", { name: "Liebe, Nomen" }));
    expect(handlers.onSelectRendering).toHaveBeenCalledWith("liebe");
    await waitFor(() =>
      expect(
        statsCalls(fetchMock).some((url) => url.includes("word=lieben&testament=nt&pos=VERB")),
      ).toBe(true),
    );
  });

  it("states the resolved lemma and switches the part of speech", async () => {
    const user = userEvent.setup();
    const fetchMock = stubFetch({
      summary: () => json(fixtureSnapshotSummary),
      wordStats: (url) =>
        json({
          ...fixtureWordStats,
          word: "Gang",
          lemma: "gehen",
          pos: url.includes("pos=VERB") ? "VERB" : "NOUN",
          candidates: [
            { lemma: "gehen", pos: "VERB", occurrences: 900 },
            { lemma: "Gang", pos: "NOUN", occurrences: 40 },
          ],
        }),
    });
    renderHeader();

    const lines = await screen.findAllByTestId("resolved-lemma");
    expect(lines[0]).toHaveTextContent("Liebe → gehen, Nomen");

    await user.click(
      (await screen.findAllByRole("button", { name: /^Verb · 900$/ }))[0] as HTMLElement,
    );
    await waitFor(() =>
      expect(statsCalls(fetchMock).some((url) => url.endsWith("&pos=VERB"))).toBe(true),
    );
  });

  it("offers one switch button per part of speech even when two lemmas share it", async () => {
    stubFetch({
      summary: () => json(fixtureSnapshotSummary),
      wordStats: () =>
        json({
          ...fixtureWordStats,
          word: "ging",
          lemma: "gehen",
          pos: "VERB",
          candidates: [
            { lemma: "gehen", pos: "VERB", occurrences: 1513 },
            { lemma: "Ging", pos: "VERB", occurrences: 1 },
            { lemma: "Gang", pos: "NOUN", occurrences: 6 },
          ],
        }),
    });
    renderHeader();

    const verbs = await screen.findAllByRole("button", { name: /^Verb · / });
    expect(verbs[0]).toHaveTextContent("Verb · 1513");
    expect(verbs[0]).toHaveAttribute("aria-pressed", "true");
    expect(screen.queryByRole("button", { name: "Verb · 1" })).not.toBeInTheDocument();
    expect((await screen.findAllByRole("button", { name: /^Nomen · 6$/ })).length).toBeGreaterThan(
      0,
    );
  });

  it("hides the refresh button from anonymous users and readers", async () => {
    stubFetch({ summary: () => json(fixtureSnapshotSummary), me: asRole("reader") });
    renderHeader();
    await screen.findByText(/^Statistik: Stand /);
    expect(screen.queryByRole("button", { name: "Neu berechnen" })).not.toBeInTheDocument();
  });

  it("refreshes: 202, then 'wird berechnet', then the new timestamp", async () => {
    const user = userEvent.setup();
    let summaryCalls = 0;
    const fetchMock = stubFetch({
      me: asRole("Maintainer"),
      summary: () => {
        summaryCalls += 1;
        if (summaryCalls === 1) return json(fixtureSnapshotSummary);
        if (summaryCalls === 2) {
          return json({ ...fixtureSnapshotSummary, statsRefreshing: true });
        }
        return json({ ...fixtureSnapshotSummary, statsRefreshedAt: "2026-11-13T08:00:00Z" });
      },
    });
    renderHeader();

    await user.click(await screen.findByRole("button", { name: "Neu berechnen" }));
    expect(await screen.findByText("Statistik wird berechnet …")).toBeVisible();
    expect(
      fetchMock.mock.calls.some(
        ([url, init]) => String(url).endsWith("/dataQuality/refresh") && init?.method === "POST",
      ),
    ).toBe(true);
    // Polls every 5 s until the timestamp changes.
    await waitFor(() => expect(screen.getByText(/^Statistik: Stand 13\.11\./)).toBeVisible(), {
      timeout: 8000,
    });
    expect(screen.getByRole("button", { name: "Neu berechnen" })).toBeEnabled();
  }, 12000);

  it("treats 409 as already running", async () => {
    const user = userEvent.setup();
    stubFetch({
      me: asRole("admin"),
      summary: () => json(fixtureSnapshotSummary),
      refresh: () => new Response("", { status: 409 }),
    });
    renderHeader();
    await user.click(await screen.findByRole("button", { name: "Neu berechnen" }));
    expect(await screen.findByText("Statistik wird berechnet …")).toBeVisible();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("hides the button after a 403", async () => {
    const user = userEvent.setup();
    stubFetch({
      me: asRole("maintainer"),
      summary: () => json(fixtureSnapshotSummary),
      refresh: () => new Response("", { status: 403 }),
    });
    renderHeader();
    await user.click(await screen.findByRole("button", { name: "Neu berechnen" }));
    await waitFor(() =>
      expect(screen.queryByRole("button", { name: "Neu berechnen" })).not.toBeInTheDocument(),
    );
  });
});
