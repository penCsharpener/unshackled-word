import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { SeedJob, SeedStatus } from "@/lib/maintainer/seedJobs";
import { json, stubApi } from "@/test/authHelpers";
import { SeedRoundTripSection } from "./SeedRoundTripSection";

beforeEach(() => i18n.changeLanguage("de"));
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

const exported: SeedJob = {
  id: "t1",
  operation: "lxxSeedExport",
  kind: "lxx",
  status: "succeeded",
  startedAt: "2026-11-23T10:00:00Z",
  log: ["10:00:00 Wrote 3 LXX tags and 2 links to seed/lxx"],
  result: {
    steps: [
      { kind: "lxx", action: "export", counts: { tags: 3, links: 2 }, lists: {}, reportFiles: [] },
    ],
  },
};

describe("SeedRoundTripSection LXX tag export (0028.8)", () => {
  it("starts the export job and shows its status and the tag and link counts", async () => {
    let current: SeedStatus = { imports: [] };
    const fetchMock = stubApi({
      "GET /maintainer/seed/status": () => json(current),
      "POST /maintainer/lxx/seed/export": () => {
        current = { imports: [], job: exported };
        return json(exported, 202);
      },
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "LXX-Tags exportieren" }));

    expect(await screen.findByRole("status")).toHaveTextContent("fertig");
    expect(
      screen.getByRole("heading", { name: /LXX-Tags exportieren · LXX-Tags/ }),
    ).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getByText("Verknüpfungen")).toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(1);
  });

  it("explains a 409 while another job runs", async () => {
    stubApi({
      "GET /maintainer/seed/status": () => json({ imports: [] }),
      "POST /maintainer/lxx/seed/export": () => json({ title: "busy" }, 409),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "LXX-Tags exportieren" }));
    expect(await screen.findByText(i18n.t("maintainer.seedJobs.busy"))).toBeInTheDocument();
  });
});
