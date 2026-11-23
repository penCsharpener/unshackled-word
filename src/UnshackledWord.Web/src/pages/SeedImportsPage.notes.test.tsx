import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
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

const file = (name: string) => ({ name, sizeBytes: 2048, modifiedAt: "2026-11-23T10:00:00Z" });
const OLD = "notes-20261122-100000.json";
const NEW = "notes-20261123-100000.json";

function job(over: Partial<SeedJob>): SeedJob {
  return {
    id: "n1",
    operation: "notesExport",
    kind: "notes",
    status: "succeeded",
    startedAt: "2026-11-23T10:00:00Z",
    log: [],
    ...over,
  };
}

describe("SeedImportsPage notes card", () => {
  it("refreshes the file list after an export and preselects the newest file", async () => {
    let files = [file(OLD)];
    let current: { imports: never[]; job?: SeedJob } = { imports: [] };
    stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json(current),
      "GET /maintainer/notes/files": () => json(files),
      "POST /maintainer/notes/export": () => {
        files = [file(NEW), file(OLD)];
        current = {
          imports: [],
          job: job({
            result: {
              steps: [
                {
                  kind: "notes",
                  action: "export",
                  counts: { notes: 312 },
                  lists: {},
                  reportFiles: [`notes/${NEW}`],
                },
              ],
            },
          }),
        };
        return json(current.job, 202);
      },
    });
    renderPage();
    const select = await screen.findByRole("combobox", { name: "Exportdatei:" });
    expect(select).toHaveValue(OLD);

    await userEvent.click(screen.getByRole("button", { name: "Notizen exportieren" }));
    await waitFor(() =>
      expect(screen.getByRole("combobox", { name: "Exportdatei:" })).toHaveValue(NEW),
    );
    expect(screen.getAllByRole("option")).toHaveLength(2);
  });

  it("imports the chosen file and shows 0 neu, 0 geändert, N unverändert", async () => {
    const bodies: unknown[] = [];
    let current: { imports: never[]; job?: SeedJob } = { imports: [] };
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json(current),
      "GET /maintainer/notes/files": () => json([file(NEW), file(OLD)]),
      "POST /maintainer/notes/import": () => {
        current = {
          imports: [],
          job: job({
            operation: "notesImport",
            result: {
              steps: [
                {
                  kind: "notes",
                  action: "import",
                  counts: { inserted: 0, updated: 0, unchanged: 312, skipped: 0 },
                  lists: {},
                  reportFiles: [],
                },
              ],
            },
          }),
        };
        return json(current.job, 202);
      },
    });
    renderPage();
    await userEvent.selectOptions(
      await screen.findByRole("combobox", { name: "Exportdatei:" }),
      OLD,
    );
    await userEvent.click(screen.getByRole("button", { name: "Notizen importieren" }));

    expect(await screen.findByText("312")).toBeInTheDocument();
    for (const label of ["neu", "geändert", "unverändert"]) {
      expect(screen.getAllByText(label).length).toBeGreaterThan(0);
    }
    for (const call of fetchMock.mock.calls) {
      if (call[1]?.method === "POST") bodies.push(JSON.parse(String(call[1].body)));
    }
    expect(bodies).toEqual([{ fileName: OLD }]);
  });

  it("explains a 409 while another job runs", async () => {
    stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json({ imports: [] }),
      "GET /maintainer/notes/files": () => json([file(NEW)]),
      "POST /maintainer/notes/export": () => json({ title: "busy" }, 409),
    });
    renderPage();
    await userEvent.click(await screen.findByRole("button", { name: "Notizen exportieren" }));
    expect(await screen.findByText(i18n.t("maintainer.seedJobs.busy"))).toBeInTheDocument();
  });
});
