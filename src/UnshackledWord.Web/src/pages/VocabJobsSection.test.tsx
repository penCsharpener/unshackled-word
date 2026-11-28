import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { json, stubApi } from "@/test/authHelpers";
import { VocabJobsSection } from "./VocabJobsSection";

const job = (over: Record<string, unknown>) => ({
  id: "job-1",
  operation: "vocabMeanings",
  kind: "vocab",
  status: "succeeded",
  startedAt: "2026-11-21T09:00:00Z",
  finishedAt: "2026-11-21T09:00:05Z",
  log: [],
  ...over,
});

const dryRunReport = job({
  result: {
    steps: [
      {
        kind: "vocab",
        action: "meanings",
        counts: {
          dryRun: 1,
          entriesProcessed: 5412,
          entriesChanged: 412,
          topMeaningChanged: 2,
          meaningsInserted: 9,
          meaningsRemoved: 3,
          meaningsUpdated: 0,
        },
        lists: {
          topMeaningChanged: {
            total: 2,
            items: ["9 λέγω: sagen -> sprechen", "54 ἔρχομαι: kommen -> kommen, gehen"],
          },
        },
        reportFiles: [],
      },
    ],
  },
});

const rootsJob = job({
  operation: "vocabRootPrefill",
  result: {
    steps: [
      {
        kind: "vocab",
        action: "rootPrefill",
        counts: { greekWritten: 1025, greekUpdated: 0, hebrewWritten: 1511, greekUnresolved: 478 },
        lists: {},
        reportFiles: [],
      },
    ],
  },
});

const rootStatus: { current: Record<string, unknown> } = {
  current: {
    job: null,
    counts: { prefill: 0, agent: 1, manual: 2, missing: 13500, notApplicable: 1900 },
  },
};

function renderSection() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <VocabJobsSection />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/** Stubs the three vocab status endpoints; the meaning status reads `meaningStatus` so a run can change it. */
function stubJobs(meaningStatus: { current: unknown }, onStart?: (body: unknown) => unknown) {
  return stubApi({
    "GET /maintainer/vocab/renderings/status": () => json({}),
    "GET /maintainer/vocab/status": () => json({}),
    "GET /maintainer/vocab/meanings/status": () => json(meaningStatus.current),
    "GET /maintainer/vocab/roots/status": () => json(rootStatus.current),
    "POST /maintainer/vocab/roots/prefill": () => {
      rootStatus.current = { ...rootStatus.current, job: rootsJob };
      return json(rootsJob, 202);
    },
    "POST /maintainer/vocab/meanings/derive": () => {
      const result = onStart?.(undefined);
      return json(result ?? dryRunReport, 202);
    },
  });
}

/** The request body the derive endpoint got, parsed. */
function derivePayloads(fetchMock: ReturnType<typeof stubApi>) {
  return fetchMock.mock.calls
    .filter(
      ([url, init]) => url.endsWith("/maintainer/vocab/meanings/derive") && init?.method === "POST",
    )
    .map(([, init]) => JSON.parse(String(init?.body)) as Record<string, unknown>);
}

beforeEach(() => {
  rootStatus.current = {
    job: null,
    counts: { prefill: 0, agent: 1, manual: 2, missing: 13500, notApplicable: 1900 },
  };
  return i18n.changeLanguage("en");
});
afterEach(() => vi.unstubAllGlobals());

describe("VocabJobsSection meaning derivation", () => {
  it("runs a dry run for the whole vocabulary and shows its report with links to the entries", async () => {
    const status = { current: { job: null } as unknown };
    const fetchMock = stubJobs(status, () => {
      status.current = { job: dryRunReport };
      return dryRunReport;
    });
    renderSection();

    await userEvent.click(await screen.findByRole("button", { name: "Dry run" }));

    expect(derivePayloads(fetchMock)).toEqual([{ dryRun: true }]);
    expect(await screen.findByText("Dry run: nothing was written.")).toBeInTheDocument();
    expect(screen.getByText("5,412")).toBeInTheDocument();
    expect(screen.getByText("412")).toBeInTheDocument();
    const lemma = screen.getByRole("link", { name: "λέγω" });
    expect(lemma).toHaveAttribute("href", "/vocab/9");
    const row = lemma.closest("tr");
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText("sagen")).toBeInTheDocument();
    expect(within(row as HTMLElement).getByText("sprechen")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ἔρχομαι" })).toHaveAttribute("href", "/vocab/54");
  });

  it("enables apply only after a finished dry run of the selected scope", async () => {
    const status = { current: { job: null } as unknown };
    const fetchMock = stubJobs(status, () => {
      status.current = { job: dryRunReport };
      return dryRunReport;
    });
    renderSection();

    const apply = await screen.findByRole("button", { name: "Apply" });
    expect(apply).toBeDisabled();
    await userEvent.click(screen.getByRole("button", { name: "Dry run" }));
    await waitFor(() => expect(apply).toBeEnabled());

    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Scope:" }), "nt");
    expect(apply).toBeDisabled();

    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Scope:" }), "all");
    expect(apply).toBeEnabled();

    await userEvent.click(apply);
    expect(derivePayloads(fetchMock)).toEqual([{ dryRun: true }, { dryRun: false }]);
  });

  it("sends the course code of the selected scope", async () => {
    const status = { current: { job: null } as unknown };
    const fetchMock = stubJobs(status);
    renderSection();

    await userEvent.selectOptions(await screen.findByRole("combobox", { name: "Scope:" }), "at");
    await userEvent.click(screen.getByRole("button", { name: "Dry run" }));

    await waitFor(() =>
      expect(derivePayloads(fetchMock)).toEqual([{ course: "ot-hebrew", dryRun: true }]),
    );
  });
});

const profile = { name: "ollama", kind: "ollama", model: "qwen3", usable: true, maxParallel: 1 };

const formEstimate = {
  course: "nt-greek",
  profile: "ollama",
  model: "qwen3",
  promptVersion: "vocab-form-greek-v1",
  entries: 120,
  batches: 6,
  batchSize: 25,
  fromRank: 1,
  toRank: 200,
  estimatedInputTokens: 52000,
  estimatedOutputTokens: 3000,
  estimatedCostUsd: 0.12,
  warnings: [],
};

const formRun = {
  id: 7,
  profile: "ollama",
  model: "qwen3",
  promptVersion: "vocab-form-greek-v1",
  book: 0,
  fromRefId: 0,
  toRefId: 0,
  mode: "vocab-form",
  status: "completed",
  statusNote: "118 forms written, 2 kept",
  inputTokens: 52000,
  cachedInputTokens: 0,
  outputTokens: 3000,
  costUsd: 0.11,
  cacheRate: 0,
  createdAt: "2026-11-21T09:00:00Z",
  packages: 6,
  packagesCompleted: 6,
  packagesFailed: 0,
  proposedVerses: 0,
  flaggedVerses: 0,
  openProposals: 0,
  active: false,
};

/** The bodies of the POSTs to one endpoint of the form runs, parsed. */
function formPosts(fetchMock: ReturnType<typeof stubApi>, path: string) {
  return fetchMock.mock.calls
    .filter(([url, init]) => url.endsWith(path) && init?.method === "POST")
    .map(([, init]) => JSON.parse(String(init?.body)) as Record<string, unknown>);
}

describe("VocabJobsSection study-form run", () => {
  it("estimates the range first, then starts the run with the same body", async () => {
    const fetchMock = stubApi({
      "GET /maintainer/vocab/renderings/status": () => json({}),
      "GET /maintainer/vocab/status": () => json({}),
      "GET /maintainer/vocab/meanings/status": () => json({}),
      "GET /maintainer/agent-runs/profiles": () => json([profile]),
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [] }),
      "POST /maintainer/vocab/form-runs/estimate": () => json(formEstimate),
      "POST /maintainer/vocab/form-runs": () => json(formRun, 202),
      "GET /maintainer/agent-runs/7": () => json(formRun),
    });
    renderSection();

    const start = await screen.findByRole("button", { name: "Start run" });
    expect(start).toBeDisabled();
    expect(await screen.findByText("Ranks 1 to 200")).toBeInTheDocument();
    await waitFor(() => expect(start).toBeEnabled());

    await userEvent.click(start);

    const body = { course: "nt-greek", fromRank: 1, toRank: 200, batchSize: 25, profile: "ollama" };
    await waitFor(() =>
      expect(formPosts(fetchMock, "/maintainer/vocab/form-runs")).toEqual([body]),
    );
    expect(formPosts(fetchMock, "/maintainer/vocab/form-runs/estimate")).toContainEqual(body);
    expect(await screen.findByText("118 forms written, 2 kept")).toBeInTheDocument();
  });

  it("covers the whole range with the batch size typed in and blocks a batch size above 200", async () => {
    const fetchMock = stubApi({
      "GET /maintainer/vocab/renderings/status": () => json({}),
      "GET /maintainer/vocab/status": () => json({}),
      "GET /maintainer/vocab/meanings/status": () => json({}),
      "GET /maintainer/agent-runs/profiles": () => json([profile]),
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [] }),
      "POST /maintainer/vocab/form-runs/estimate": () =>
        json({ ...formEstimate, entries: 400, batches: 4, batchSize: 100, toRank: 400 }),
      "POST /maintainer/vocab/form-runs": () => json(formRun, 202),
      "GET /maintainer/agent-runs/7": () => json(formRun),
    });
    renderSection();

    const card = await waitFor(() => {
      const element = document.querySelector<HTMLElement>('[data-run-kind="form"]');
      if (!element) throw new Error("form run card missing");
      return element;
    });
    const start = within(card).getByRole("button", { name: "Start run" });
    const batch = within(card).getByLabelText("Entries per call");
    const to = within(card).getByLabelText("to");

    await userEvent.clear(batch);
    await userEvent.type(batch, "201");
    expect(
      await within(card).findByText("Entries per call: a whole number from 1 to 200."),
    ).toBeInTheDocument();
    expect(start).toBeDisabled();

    await userEvent.clear(batch);
    await userEvent.type(batch, "100");
    await userEvent.clear(to);
    await userEvent.type(to, "400");
    expect(await within(card).findByText("Ranks 1 to 400")).toBeInTheDocument();
    await waitFor(() => expect(start).toBeEnabled());
    await userEvent.click(start);

    const body = {
      course: "nt-greek",
      fromRank: 1,
      toRank: 400,
      batchSize: 100,
      profile: "ollama",
    };
    await waitFor(() =>
      expect(formPosts(fetchMock, "/maintainer/vocab/form-runs")).toEqual([body]),
    );
    // no limit: the API covers the whole range (up to 2000 entries) instead of cutting it at 200
    expect(
      formPosts(fetchMock, "/maintainer/vocab/form-runs/estimate").every(
        (sent) => !("limit" in sent),
      ),
    ).toBe(true);
  });
});

describe("VocabJobsSection root run", () => {
  const rootEstimate = { ...formEstimate, promptVersion: "vocab-root-greek-v1", entries: 80 };
  const rootRun = {
    ...formRun,
    id: 9,
    promptVersion: "vocab-root-greek-v1",
    mode: "vocab-root",
    statusNote: "Wrote 70 roots, replaced 1 prefill rows",
  };
  const report = {
    runId: 9,
    rejected: 1,
    flagged: 1,
    replaced: 1,
    items: [
      {
        entryId: 501,
        lemma: "ἐκπορεύω",
        strongsKey: "G1607",
        kind: "rejected",
        reason: "Prefix key G9999 is not in the preposition table.",
        answer: "G9999 + G4198",
      },
      {
        entryId: 502,
        lemma: "συνεπιμαρτυρέω",
        strongsKey: "G4901",
        kind: "flagged",
        reason: "Prefixes and root do not plausibly form συνεπιμαρτυρέω.",
      },
      {
        entryId: 503,
        lemma: "ἀπέχω",
        strongsKey: "G0576",
        kind: "replaced",
        reason: "Prefill: ἀπό + G2192; run: ἀπό + G2192 (other root).",
      },
    ],
  };

  it("estimates, sends the prefill-check option, then lists rejected and flagged entries with editor links", async () => {
    const fetchMock = stubApi({
      "GET /maintainer/vocab/renderings/status": () => json({}),
      "GET /maintainer/vocab/status": () => json({}),
      "GET /maintainer/vocab/meanings/status": () => json({}),
      "GET /maintainer/agent-runs/profiles": () => json([profile]),
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [] }),
      "POST /maintainer/vocab/root-runs/estimate": () => json(rootEstimate),
      "POST /maintainer/vocab/root-runs": () => json(rootRun, 202),
      "GET /maintainer/agent-runs/9": () => json(rootRun),
      "GET /maintainer/vocab/root-runs/9/report": () => json(report),
    });
    renderSection();

    const start = await screen.findByRole("button", { name: "Start root run" });
    await waitFor(() => expect(start).toBeEnabled());
    await userEvent.click(screen.getByRole("switch", { name: "Check prefill" }));
    const rootCard = document.querySelector<HTMLElement>('[data-run-kind="root"]') as HTMLElement;
    const rootBatch = within(rootCard).getByLabelText("Entries per call");
    await userEvent.clear(rootBatch);
    await userEvent.type(rootBatch, "50");
    await waitFor(() =>
      expect(
        formPosts(fetchMock, "/maintainer/vocab/root-runs/estimate").some(
          (body) => body.checkPrefill === true,
        ),
      ).toBe(true),
    );
    await waitFor(() => expect(start).toBeEnabled());

    await userEvent.click(start);

    await waitFor(() =>
      expect(formPosts(fetchMock, "/maintainer/vocab/root-runs")).toEqual([
        {
          course: "nt-greek",
          fromRank: 1,
          toRank: 200,
          batchSize: 50,
          profile: "ollama",
          checkPrefill: true,
        },
      ]),
    );
    expect(await screen.findByText("Wrote 70 roots, replaced 1 prefill rows")).toBeInTheDocument();
    expect(
      await screen.findByText("Report: 1 rejected, 1 flagged, 1 replaced"),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ἐκπορεύω" })).toHaveAttribute(
      "href",
      "/edit/vocab/501",
    );
    expect(screen.getByRole("link", { name: "συνεπιμαρτυρέω" })).toHaveAttribute(
      "href",
      "/edit/vocab/502",
    );
    expect(
      screen.getByText("Prefix key G9999 is not in the preposition table."),
    ).toBeInTheDocument();
  });
});

describe("VocabJobsSection root prefill", () => {
  it("shows the root counts and starts the prefill", async () => {
    stubJobs({ current: { job: null } });
    renderSection();

    expect(await screen.findByText("13,500")).toBeInTheDocument();
    expect(screen.getByText("Without root")).toBeInTheDocument();
    expect(screen.getByText("1,900")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Prefill roots" }));

    expect(
      await screen.findByText(
        /1,025 Greek compounds and 1,511 Hebrew verbs written, 478 unresolved/,
      ),
    ).toBeInTheDocument();
  });
});
