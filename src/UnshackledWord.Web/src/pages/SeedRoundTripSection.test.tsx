import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { SeedJob, SeedStatus } from "@/lib/maintainer/seedJobs";
import { json, stubApi } from "@/test/authHelpers";
import { SeedRoundTripSection } from "./SeedRoundTripSection";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

function renderSection() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <SeedRoundTripSection />
    </QueryClientProvider>,
  );
}

const emptyList = { total: 0, items: [] };

function job(partial: Partial<SeedJob>): SeedJob {
  return {
    id: "j1",
    operation: "update",
    kind: "revision",
    status: "succeeded",
    startedAt: "2026-11-14T10:00:00Z",
    log: ["10:00:00 Started update (revision)", "10:00:02 Finished"],
    ...partial,
  };
}

const status = (partial: Partial<SeedStatus> = {}): SeedStatus => ({ imports: [], ...partial });

describe("SeedRoundTripSection", () => {
  it("starts the chosen operation for the chosen kind and shows the running job", async () => {
    const running = job({
      operation: "import",
      status: "running",
      log: ["10:00:00 Started import (mappings)"],
    });
    let current = status();
    const fetchMock = stubApi({
      "GET /maintainer/seed/status": () => json(current),
      "POST /maintainer/seed/jobs": () => {
        current = status({ job: running });
        return json(running, 202);
      },
    });
    renderSection();
    await userEvent.selectOptions(await screen.findByLabelText("Seed kind:"), "mappings");
    await userEvent.click(screen.getByRole("button", { name: "Import" }));

    expect(await screen.findByText("running")).toBeInTheDocument();
    expect(screen.getByText("10:00:00 Started import (mappings)")).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({
      operation: "import",
      kind: "mappings",
      confirmBackup: false,
    });
    expect(screen.getByRole("button", { name: "Update" })).toBeDisabled();
  });

  it("lists replaced, kept, new and removed variants of an update and explains a skipped step", async () => {
    stubApi({
      "GET /maintainer/seed/status": () =>
        json(
          status({
            job: job({
              result: {
                steps: [
                  {
                    kind: "revision",
                    action: "update",
                    counts: { variants: 5, replaced: 2, kept: 1, new: 1, removed: 1, orphaned: 3 },
                    lists: {
                      replaced: { total: 2, items: ["John 3:16 · 200", "John 1:1 · 200"] },
                      kept: { total: 1, items: ["Genesis 1:1 · 200"] },
                      orphans: { total: 250, items: ["Psalms 1:1 · 600 (Level)"] },
                    },
                    reportFiles: ["revision-update-abc.csv"],
                  },
                  {
                    kind: "mappings",
                    action: "update",
                    skipped: true,
                    reason: "up-to-date",
                    counts: {},
                    lists: {},
                    reportFiles: [],
                  },
                ],
              },
            }),
          }),
        ),
    });
    renderSection();

    const revision = await screen.findByRole("region", { name: "Revision · Update" });
    expect(within(revision).getByText("John 3:16 · 200")).toBeInTheDocument();
    expect(within(revision).getByText("Replaced (2)")).toBeInTheDocument();
    expect(within(revision).getByText("Changed locally, kept (1)")).toBeInTheDocument();
    expect(within(revision).getByText("Orphaned (250)")).toBeInTheDocument();
    expect(within(revision).getByText("… and 249 more (see the report file)")).toBeInTheDocument();
    expect(within(revision).getByText("Check")).toBeInTheDocument();
    expect(
      within(revision).getByText(/temp\/seed-report\/revision-update-abc\.csv/),
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole("region", { name: "Mappings · Update" })).getByText(
        "Skipped: The seed is the version that was already imported.",
      ),
    ).toBeInTheDocument();
  });

  it("shows the SeedImports rows", async () => {
    stubApi({
      "GET /maintainer/seed/status": () =>
        json(
          status({
            imports: [
              {
                id: 3,
                kind: "revision",
                seedVersion: "0123456789abcdef",
                importedAt: "2026-11-14T10:00:00Z",
                rows: 42,
                exact: 40,
                shifted: 1,
                modified: 0,
                orphaned: 1,
                versesUpdated: 2,
                versesKeptLocal: 1,
              },
            ],
          }),
        ),
    });
    renderSection();

    const row = (await screen.findByText("0123456789ab")).closest("tr");
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText("42")).toBeInTheDocument();
  });

  it("reports whether the round trip is byte-identical", async () => {
    const same = {
      compared: 3,
      identical: 3,
      changed: emptyList,
      onlyInNew: emptyList,
      onlyInOld: emptyList,
    };
    stubApi({
      "GET /maintainer/seed/status": () =>
        json(
          status({
            job: job({
              operation: "roundTrip",
              result: {
                steps: [],
                roundTrip: {
                  kind: "revision",
                  files: 3,
                  secondExportIdentical: true,
                  secondExport: same,
                  matchesDisk: false,
                  disk: {
                    ...same,
                    identical: 2,
                    changed: { total: 1, items: ["revision/verses/43-John.csv"] },
                  },
                },
              },
            }),
          }),
        ),
    });
    renderSection();

    expect(await screen.findByText("Two exports in a row are byte-identical.")).toBeInTheDocument();
    expect(
      screen.getByText("The export differs from the files in assets/seed."),
    ).toBeInTheDocument();
    expect(screen.getByText("revision/verses/43-John.csv", { exact: false })).toBeInTheDocument();
  });

  it("asks for the backup confirmation before it starts a rebase", async () => {
    const fetchMock = stubApi({
      "GET /maintainer/seed/status": () => json(status()),
      "POST /maintainer/seed/jobs": () =>
        json(job({ operation: "rebase", kind: "all", status: "running", log: [] }), 202),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Rebase …" }));

    const dialog = await screen.findByRole("dialog");
    const start = within(dialog).getByRole("button", { name: "Start rebase" });
    expect(start).toBeDisabled();

    await userEvent.click(
      within(dialog).getByRole("checkbox", { name: "I have made a database backup." }),
    );
    await userEvent.click(start);

    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, init]) => init?.method === "POST")).toBe(true),
    );
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({
      operation: "rebase",
      kind: "all",
      confirmBackup: true,
    });
  });

  it("shows why a rebase was refused", async () => {
    stubApi({
      "GET /maintainer/seed/status": () =>
        json(
          status({
            job: job({
              operation: "rebase",
              kind: "all",
              result: {
                steps: [],
                rebase: {
                  refused: "mapping-v2",
                  revisionRealigned: 0,
                  revisionMissing: 0,
                  steps: [],
                },
              },
            }),
          }),
        ),
    });
    renderSection();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Rebase refused, nothing was changed: Mapping v2 holds rows; use the source word sync there.",
    );
  });

  it("disables the steps and says why when the assets folder is not reachable", async () => {
    stubApi({
      "GET /maintainer/seed/status": () =>
        json(status({ assetsProblem: "The assets folder is not reachable." })),
    });
    renderSection();

    expect(
      await screen.findByText(/not possible here: The assets folder is not reachable\./),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Export to assets/seed" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Import" })).toBeDisabled();
  });

  it("explains a 409 when another job runs", async () => {
    stubApi({
      "GET /maintainer/seed/status": () => json(status()),
      "POST /maintainer/seed/jobs": () => new Response("{}", { status: 409 }),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Update" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("already running");
  });
});
