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

beforeEach(() => i18n.changeLanguage("en"));
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

describe("SeedImportsPage", () => {
  it("explains every import with its tables and starts the chosen one", async () => {
    const started: unknown[] = [];
    const running: SeedJob = {
      id: "j1",
      operation: "import",
      kind: "vocab",
      status: "running",
      startedAt: "2026-11-22T10:00:00Z",
      log: ["10:00:00 Started import (vocab)"],
    };
    let current: { imports: never[]; job?: SeedJob } = { imports: [] };
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json(current),
      "POST /maintainer/seed/jobs": () => {
        current = { imports: [], job: running };
        return json(running, 202);
      },
    });
    renderPage();

    const vocab = (await screen.findByRole("heading", { name: "Vocab" })).closest("li");
    expect(vocab).not.toBeNull();
    const card = within(vocab as HTMLElement);
    expect(card.getByText("VocabMeanings")).toBeInTheDocument();
    for (const link of screen.getAllByRole("link", { name: "Maintenance" })) {
      expect(link).toHaveAttribute("href", "/edit/maintainer");
    }
    expect(card.getByRole("note")).toBeInTheDocument();

    await userEvent.click(card.getByRole("button", { name: "Import Vocabulary" }));

    for (const call of fetchMock.mock.calls) {
      if (call[1]?.method === "POST") started.push(JSON.parse(String(call[1].body)));
    }
    expect(started).toEqual([{ operation: "import", kind: "vocab" }]);
    expect(await screen.findByText("10:00:00 Started import (vocab)")).toBeInTheDocument();
  });

  it("offers an update only where one exists", async () => {
    stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["maintainer"] }),
      "GET /maintainer/seed/status": () => json({ imports: [] }),
    });
    renderPage();
    await screen.findByRole("heading", { name: "Revision" });

    expect(screen.getByRole("button", { name: "Update Revision" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Update Mappings" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Update Vocabulary" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Update Morphology" })).toBeNull();
  });

  it("shows a forbidden note without a write role", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: [] }) });
    renderPage();
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Import/ })).toBeNull();
  });
});
