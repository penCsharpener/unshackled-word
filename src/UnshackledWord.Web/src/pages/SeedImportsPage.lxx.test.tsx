import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { AuthProvider } from "@/lib/auth/AuthContext";
import type { SeedJob } from "@/lib/maintainer/seedJobs";
import { json, me, stubApi } from "@/test/authHelpers";
import { SeedImportsPage } from "./SeedImportsPage";

beforeEach(() => i18n.changeLanguage("de"));
afterEach(() => vi.unstubAllGlobals());

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <AuthProvider>
          <SeedImportsPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function importJob(counts: Record<string, number>, skipped: string[] = []): SeedJob {
  return {
    id: "t2",
    operation: "lxxSeedImport",
    kind: "lxx",
    status: "succeeded",
    startedAt: "2026-11-23T10:00:00Z",
    log: [],
    result: {
      steps: [
        {
          kind: "lxx",
          action: "import",
          counts,
          lists: { skippedRows: { total: skipped.length, items: skipped } },
          reportFiles: [],
        },
      ],
    },
  };
}

function postBodies(fetchMock: ReturnType<typeof stubApi>) {
  return fetchMock.mock.calls
    .filter(([, init]) => init?.method === "POST")
    .map(([url, init]) => [String(url), JSON.parse(String(init?.body))]);
}

const lxxCard = () => screen.getByRole("region", { name: "LXX-Tags" });

describe("SeedImportsPage LXX tag card (0028.8)", () => {
  it("imports without mirror at once and shows the report with the skipped list", async () => {
    let current: { imports: never[]; job?: SeedJob } = { imports: [] };
    const job = importJob(
      {
        tags: 3,
        links: 2,
        inserted: 1,
        updated: 0,
        unchanged: 4,
        deleted: 0,
        skipped: 1,
        anchorMismatch: 1,
      },
      ["800100100#2: the word's hash changed (aaaaaaaa → bbbbbbbb)"],
    );
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json(current),
      "GET /maintainer/notes/files": () => json([]),
      "POST /maintainer/lxx/seed/import": () => {
        current = { imports: [], job };
        return json(job, 202);
      },
    });
    renderPage();
    await screen.findByRole("heading", { name: "LXX-Tags" });
    await userEvent.click(within(lxxCard()).getByRole("button", { name: "LXX-Tags importieren" }));

    expect(await screen.findByText("4")).toBeInTheDocument();
    expect(screen.getAllByText("gelöscht").length).toBeGreaterThan(0);
    expect(screen.getByText("Übersprungen (1)")).toBeInTheDocument();
    expect(postBodies(fetchMock)).toEqual([["/api/maintainer/lxx/seed/import", { mirror: false }]]);
    expect(
      fetchMock.mock.calls.some(([url]) => String(url).includes("lxx/seed/import/preview")),
    ).toBe(false);
  });

  it("with Exakt wie Datei asks first with the preview count and deletes after confirming", async () => {
    let current: { imports: never[]; job?: SeedJob } = { imports: [] };
    const job = importJob({ inserted: 0, updated: 0, unchanged: 3, deleted: 1, skipped: 0 });
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json(current),
      "GET /maintainer/notes/files": () => json([]),
      "GET /maintainer/lxx/seed/import/preview": () =>
        json({ wouldDeleteTags: 1, wouldDeleteLinks: 2, fileTagRows: 3, fileLinkRows: 2 }),
      "POST /maintainer/lxx/seed/import": () => {
        current = { imports: [], job };
        return json(job, 202);
      },
    });
    renderPage();
    await screen.findByRole("heading", { name: "LXX-Tags" });
    await userEvent.click(within(lxxCard()).getByRole("checkbox", { name: "Exakt wie Datei" }));
    await userEvent.click(within(lxxCard()).getByRole("button", { name: "LXX-Tags importieren" }));

    const dialog = await screen.findByRole("dialog");
    expect(
      within(dialog).getByText(
        "1 bestätigte Tags und 2 bestätigte Verknüpfungen werden gelöscht. Vorschläge bleiben.",
      ),
    ).toBeInTheDocument();
    expect(postBodies(fetchMock)).toEqual([]);

    await userEvent.click(within(dialog).getByRole("button", { name: "Importieren" }));
    expect(await screen.findByText("gelöscht")).toBeInTheDocument();
    expect(postBodies(fetchMock)).toEqual([["/api/maintainer/lxx/seed/import", { mirror: true }]]);
  });

  it("cancelling the confirm starts nothing; the dialog names tags and links", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json({ imports: [] }),
      "GET /maintainer/notes/files": () => json([]),
      "GET /maintainer/lxx/seed/import/preview": () =>
        json({ wouldDeleteTags: 5, wouldDeleteLinks: 0, fileTagRows: 3, fileLinkRows: 0 }),
    });
    renderPage();
    await screen.findByRole("heading", { name: "LXX-Tags" });
    await userEvent.click(within(lxxCard()).getByRole("checkbox", { name: "Exakt wie Datei" }));
    await userEvent.click(within(lxxCard()).getByRole("button", { name: "LXX-Tags importieren" }));

    const dialog = await screen.findByRole("dialog");
    expect(
      within(dialog).getByText(
        "5 bestätigte Tags und 0 bestätigte Verknüpfungen werden gelöscht. Vorschläge bleiben.",
      ),
    ).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole("button", { name: "Abbrechen" }));
    expect(postBodies(fetchMock)).toEqual([]);
  });
});
