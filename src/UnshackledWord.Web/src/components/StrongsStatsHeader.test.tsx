import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { GetStrongsSummaryResponse, GetWordStatsResponse } from "@/lib/bible/types";
import { fixtureStructure } from "@/test/fixtures/bibleStructure";
import { fixtureSummary, fixtureWordStats } from "@/test/fixtures/strongsStats";
import { StrongsStatsHeader, type StrongsStatsHeaderProps } from "./StrongsStatsHeader";

interface Responses {
  summary?: () => Response;
  wordStats?: (url: string) => Response;
}

function stubFetch({ summary, wordStats }: Responses = {}) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
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
      <StrongsStatsHeader strongs="G26" {...handlers} {...props} />
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
