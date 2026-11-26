import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { LxxStatus } from "@/lib/maintainer/lxx";
import type { SeedJob } from "@/lib/maintainer/seedJobs";
import { json, stubApi } from "@/test/authHelpers";
import { LxxSection } from "./LxxSection";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

function job(partial: Partial<SeedJob> = {}): SeedJob {
  return {
    id: "j",
    operation: "lxxPrefill",
    kind: "all",
    status: "succeeded",
    startedAt: "2026-11-26T10:00:00Z",
    log: ["10:00:00 Gen 55.0 %"],
    ...partial,
  };
}

function status(partial: Partial<LxxStatus> = {}): LxxStatus {
  return {
    verses: 100,
    words: 1000,
    tags: [
      { status: "proposed", source: "nt-form", count: 400 },
      { status: "confirmed", source: "human", count: 100 },
      { status: "confirmed", source: "ai", count: 50 },
    ],
    running: false,
    ...partial,
  };
}

function renderSection() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidate = vi.spyOn(client, "invalidateQueries");
  render(
    <QueryClientProvider client={client}>
      <LxxSection />
    </QueryClientProvider>,
  );
  return { invalidate };
}

describe("LxxSection", () => {
  it("shows the counts by status and source and the coverage percentage", async () => {
    stubApi({ "GET /maintainer/lxx/status": () => json(status({ job: job() })) });
    renderSection();
    expect(await screen.findByText("55.0%")).toBeInTheDocument();
    expect(screen.getByText("proposed").nextSibling).toHaveTextContent("400");
    expect(screen.getByText("confirmed").nextSibling).toHaveTextContent("150");
    expect(screen.getByText("NT form").nextSibling).toHaveTextContent("400");
    expect(screen.getByText("Human").nextSibling).toHaveTextContent("100");
    expect(screen.getByText("10:00:00 Gen 55.0 %")).toBeInTheDocument();
  });

  it("posts the prefill and shows the running state with the job log", async () => {
    let current = status();
    const fetchMock = stubApi({
      "GET /maintainer/lxx/status": () => json(current),
      "POST /maintainer/lxx/prefill": () => {
        current = status({
          running: true,
          job: job({ status: "running", log: ["10:05:00 Started lxxPrefill (all)"] }),
        });
        return json({ job: current.job }, 202);
      },
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Prefill" }));
    expect(await screen.findByText("10:05:00 Started lxxPrefill (all)")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Running …" })).toBeDisabled();
    expect(screen.getByRole("status")).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === "POST")).toBe(true);
  });

  it("explains a 409", async () => {
    stubApi({
      "GET /maintainer/lxx/status": () => json(status()),
      "POST /maintainer/lxx/prefill": () => json({ errors: {} }, 409),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Prefill" }));
    expect(
      await screen.findByText("Another seed or LXX job is already running."),
    ).toBeInTheDocument();
  });

  it("invalidates the reader's LXX queries when a running job finishes", async () => {
    let current = status({ running: true, job: job({ status: "running" }) });
    stubApi({ "GET /maintainer/lxx/status": () => json(current) });
    const { invalidate } = renderSection();
    await screen.findByText("Running …", { selector: "button" });

    current = status({ job: job() });
    await waitFor(
      () => expect(invalidate).toHaveBeenCalledWith({ queryKey: ["bible", "chapterLxx"] }),
      { timeout: 4000 },
    );
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["bible", "lxxWord"] });
  });
});
