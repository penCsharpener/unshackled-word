import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { VerseSimilarityRun, VerseSimilarityStatus } from "@/lib/maintainer/verseSimilarity";
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
});
