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
    operation: "tskImport",
    kind: "tsk",
    status: "succeeded",
    startedAt: "2026-11-23T10:00:00Z",
    log: [],
    result: {
      steps: [
        {
          kind: "tsk",
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

const tskCard = () => screen.getByRole("region", { name: "TSK-Kuratierung" });

describe("SeedImportsPage TSK curation card (0024.7f)", () => {
  it("imports without mirror at once and shows the report with the skipped list", async () => {
    let current: { imports: never[]; job?: SeedJob } = { imports: [] };
    const job = importJob({ inserted: 1, updated: 0, unchanged: 4, deleted: 0, skipped: 1 }, [
      "1001001 → 66022021: no TSK row with this key",
    ]);
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json(current),
      "GET /maintainer/notes/files": () => json([]),
      "POST /maintainer/tsk/import": () => {
        current = { imports: [], job };
        return json(job, 202);
      },
    });
    renderPage();
    await screen.findByRole("heading", { name: "TSK-Kuratierung" });
    await userEvent.click(
      within(tskCard()).getByRole("button", { name: "TSK-Kuratierung importieren" }),
    );

    expect(await screen.findByText("4")).toBeInTheDocument();
    expect(screen.getAllByText("gelöscht").length).toBeGreaterThan(0);
    expect(screen.getByText("Übersprungen (1)")).toBeInTheDocument();
    expect(postBodies(fetchMock)).toEqual([["/api/maintainer/tsk/import", { mirror: false }]]);
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes("preview"))).toBe(false);
  });

  it("with Exakt wie Datei asks first with the preview count and deletes after confirming", async () => {
    let current: { imports: never[]; job?: SeedJob } = { imports: [] };
    const job = importJob({ inserted: 0, updated: 0, unchanged: 3, deleted: 1, skipped: 0 });
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json(current),
      "GET /maintainer/notes/files": () => json([]),
      "GET /maintainer/tsk/import/preview": () => json({ wouldDelete: 1, fileRows: 3 }),
      "POST /maintainer/tsk/import": () => {
        current = { imports: [], job };
        return json(job, 202);
      },
    });
    renderPage();
    await screen.findByRole("heading", { name: "TSK-Kuratierung" });
    await userEvent.click(within(tskCard()).getByRole("checkbox", { name: "Exakt wie Datei" }));
    await userEvent.click(
      within(tskCard()).getByRole("button", { name: "TSK-Kuratierung importieren" }),
    );

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("1 Eintrag wird gelöscht.")).toBeInTheDocument();
    expect(postBodies(fetchMock)).toEqual([]);

    await userEvent.click(within(dialog).getByRole("button", { name: "Importieren" }));
    expect(await screen.findByText("gelöscht")).toBeInTheDocument();
    expect(postBodies(fetchMock)).toEqual([["/api/maintainer/tsk/import", { mirror: true }]]);
  });

  it("cancelling the confirm starts nothing; the plural reads Einträge", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json({ imports: [] }),
      "GET /maintainer/notes/files": () => json([]),
      "GET /maintainer/tsk/import/preview": () => json({ wouldDelete: 5, fileRows: 3 }),
    });
    renderPage();
    await screen.findByRole("heading", { name: "TSK-Kuratierung" });
    await userEvent.click(within(tskCard()).getByRole("checkbox", { name: "Exakt wie Datei" }));
    await userEvent.click(
      within(tskCard()).getByRole("button", { name: "TSK-Kuratierung importieren" }),
    );

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("5 Einträge werden gelöscht.")).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole("button", { name: "Abbrechen" }));
    expect(postBodies(fetchMock)).toEqual([]);
  });
});
