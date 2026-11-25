import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type {
  VerseSimilarityEvaluation,
  VerseSimilarityRun,
  VerseSimilarityStatus,
} from "@/lib/maintainer/verseSimilarity";
import { json, stubApi } from "@/test/authHelpers";
import { VerseSimilaritySection } from "./VerseSimilaritySection";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

const defaults = { stopDfShare: 0.02, topK: 30, minScore: 0.1, phraseWeight: 0.5 };

function run(partial: Partial<VerseSimilarityRun> = {}): VerseSimilarityRun {
  return {
    id: 5,
    startedAt: "2026-11-25T10:00:00Z",
    status: "succeeded",
    startedBy: 7,
    startedByName: "Maria",
    parameters: defaults,
    pairCount: 914983,
    stopWordCount: 192,
    durationMs: 12200,
    ...partial,
  };
}

function evaluation(ntRecall: number, psalmRank: number | undefined): VerseSimilarityEvaluation {
  return {
    topK: 30,
    testaments: [
      {
        testament: "old",
        links: 198510,
        hitsAt10: 25771,
        hitsAt30: 41252,
        recallAt10: 0.1298,
        recallAt30: 0.2078,
      },
      {
        testament: "new",
        links: 91680,
        hitsAt10: 11820,
        hitsAt30: 19508,
        recallAt10: 0.1289,
        recallAt30: ntRecall,
      },
    ],
    goldenPairs: [
      {
        source: "Hebr 4,12",
        sourceVerseId: 58004012,
        target: "Offb 1,16",
        targetVerseId: 66001016,
      },
      {
        source: "Hebr 4,12",
        sourceVerseId: 58004012,
        target: "Offb 2,12",
        targetVerseId: 66002012,
        rank: 13,
      },
      {
        source: "Ps 149,6",
        sourceVerseId: 19149006,
        target: "Jes 41,15",
        targetVerseId: 23041015,
        rank: psalmRank,
        reverseRank: 4,
      },
    ],
  };
}

const status = (partial: Partial<VerseSimilarityStatus> = {}): VerseSimilarityStatus => ({
  defaults,
  runs: [run()],
  running: false,
  ...partial,
});

function renderSection() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidate = vi.spyOn(client, "invalidateQueries");
  render(
    <QueryClientProvider client={client}>
      <VerseSimilaritySection />
    </QueryClientProvider>,
  );
  return { invalidate };
}

describe("VerseSimilaritySection", () => {
  it("shows the last run, prefilled defaults and the run history", async () => {
    stubApi({
      "GET /maintainer/verse-similarity": () =>
        json(
          status({
            runs: [
              run(),
              run({ id: 4, status: "failed", error: "boom", startedByName: undefined }),
            ],
          }),
        ),
    });
    renderSection();

    expect((await screen.findAllByText("Maria")).length).toBe(2);
    expect(screen.getAllByText("12.2 s").length).toBeGreaterThan(0);
    expect(screen.getByLabelText("Phrase weight")).toHaveValue("0.5");
    expect(screen.getByLabelText("Hits per verse")).toHaveValue("30");
    const table = screen.getByRole("table");
    expect(within(table).getAllByRole("row")).toHaveLength(3);
    expect(within(table).getByText("SeedDb")).toBeInTheDocument();
    expect(within(table).getByText("failed")).toBeInTheDocument();
  });

  it("posts the edited parameters and shows the progress while the run is polled", async () => {
    let current = status();
    const fetchMock = stubApi({
      "GET /maintainer/verse-similarity": () => json(current),
      "POST /maintainer/verse-similarity/rebuild": () => {
        current = status({
          running: true,
          job: {
            id: "j",
            operation: "verseSimilarity",
            kind: "all",
            status: "running",
            startedAt: "2026-11-25T10:05:00Z",
            log: ["10:05:00 Started verseSimilarity (all)"],
          },
        });
        return json({ job: current.job }, 202);
      },
    });
    renderSection();
    await screen.findAllByText("Maria");
    const phrase = screen.getByLabelText("Phrase weight");
    await userEvent.clear(phrase);
    await userEvent.type(phrase, "0.8");
    await userEvent.click(screen.getByRole("button", { name: "Recompute" }));

    expect(await screen.findByText("10:05:00 Started verseSimilarity (all)")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Running …" })).toBeDisabled();
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ ...defaults, phraseWeight: 0.8 });
  });

  it("invalidates the reader's similar verses when a running rebuild finishes", async () => {
    let current = status({ running: true });
    stubApi({ "GET /maintainer/verse-similarity": () => json(current) });
    const { invalidate } = renderSection();
    await screen.findByText("Running …", { selector: "button" });

    current = status({ runs: [run({ id: 6, durationMs: 15000 })] });
    await waitFor(
      () =>
        expect(invalidate).toHaveBeenCalledWith({ queryKey: ["bible", "chapterSimilarVerses"] }),
      { timeout: 4000 },
    );
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["bible", "similarVersesOfVerse"] });
  });

  it("blocks an out-of-range parameter and explains a 409", async () => {
    stubApi({
      "GET /maintainer/verse-similarity": () => json(status()),
      "POST /maintainer/verse-similarity/rebuild": () => json({ errors: {} }, 409),
    });
    renderSection();
    await screen.findAllByText("Maria");
    const topK = screen.getByLabelText("Hits per verse");
    await userEvent.clear(topK);
    await userEvent.type(topK, "0");
    expect(screen.getByRole("button", { name: "Recompute" })).toBeDisabled();
    expect(screen.getByText(/outside the allowed range/)).toBeInTheDocument();

    await userEvent.clear(topK);
    await userEvent.type(topK, "10");
    await userEvent.click(screen.getByRole("button", { name: "Recompute" }));
    expect(await screen.findByText(/already running/)).toBeInTheDocument();
  });

  it("shows the error of a failed last run", async () => {
    stubApi({
      "GET /maintainer/verse-similarity": () =>
        json(status({ runs: [run({ status: "failed", error: "boom" })] })),
    });
    renderSection();
    expect(await screen.findByRole("alert")).toHaveTextContent("boom");
  });
  it("shows the evaluation of the last run with recall per testament and golden pair ranks", async () => {
    stubApi({
      "GET /maintainer/verse-similarity": () =>
        json(status({ runs: [run({ evaluationResult: evaluation(0.2128, 4) })] })),
    });
    renderSection();

    const region = await screen.findByRole("region", { name: "Evaluation" });
    expect(within(region).getByText("Old Testament")).toBeInTheDocument();
    expect(within(region).getByText(/Recall@30 21\.3%, recall@10 12\.9%/)).toBeInTheDocument();
    expect(within(region).getByText(/91,680 TSK links/)).toBeInTheDocument();
    const missingPair = within(region).getByText("Hebr 4,12 → Offb 1,16").closest("li");
    expect(missingPair).toHaveTextContent("missing");
    expect(missingPair).toHaveTextContent("back missing");
    const psalm = within(region).getByText("Ps 149,6 → Jes 41,15").closest("li");
    expect(psalm).toHaveTextContent("rank 4");
    expect(psalm).toHaveTextContent("back 4");
  });

  it("compares NT recall and the Ps 149:6 rank across runs in the history table", async () => {
    stubApi({
      "GET /maintainer/verse-similarity": () =>
        json(
          status({
            runs: [
              run({ id: 9, evaluationResult: evaluation(0.2128, 4) }),
              run({ id: 8, evaluationResult: evaluation(0.2066, undefined) }),
              run({ id: 7 }),
            ],
          }),
        ),
    });
    renderSection();

    const table = await screen.findByRole("table");
    expect(within(table).getByRole("columnheader", { name: "NT R@30" })).toBeInTheDocument();
    const rows = within(table).getAllByRole("row");
    const [newest, older, oldest] = [rows[1], rows[2], rows[3]] as [
      HTMLElement,
      HTMLElement,
      HTMLElement,
    ];
    expect(newest).toHaveTextContent("21.3%");
    expect(older).toHaveTextContent("20.7%");
    expect(within(newest).getByText("Ps 149,6 → Jes 41,15: rank 4")).toBeInTheDocument();
    expect(within(older).getByText("Ps 149,6 → Jes 41,15: rank missing")).toBeInTheDocument();
    expect(within(oldest).queryByText(/Ps 149,6/)).toBeNull();
  });

  it("says when the evaluation of a succeeded run failed", async () => {
    stubApi({
      "GET /maintainer/verse-similarity": () =>
        json(
          status({
            runs: [
              run({
                evaluationResult: { topK: 30, testaments: [], goldenPairs: [], error: "boom" },
              }),
            ],
          }),
        ),
    });
    renderSection();

    expect(
      await screen.findByText("The evaluation failed (the run itself finished): boom"),
    ).toBeInTheDocument();
  });

  it("names the evaluation in German", async () => {
    await i18n.changeLanguage("de");
    stubApi({
      "GET /maintainer/verse-similarity": () =>
        json(status({ runs: [run({ evaluationResult: evaluation(0.2128, undefined) })] })),
    });
    renderSection();

    const region = await screen.findByRole("region", { name: "Auswertung" });
    expect(within(region).getByText("Neues Testament")).toBeInTheDocument();
    expect(within(region).getByText("Ps 149,6 → Jes 41,15").closest("li")).toHaveTextContent(
      "fehlt",
    );
  });
});
