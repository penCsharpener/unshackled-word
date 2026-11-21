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

beforeEach(() => i18n.changeLanguage("en"));
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
