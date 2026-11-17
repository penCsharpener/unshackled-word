import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { LinkRoleJob, LinkRoleReport, LinkRoleStatus } from "@/lib/maintainer/linkRoles";
import { json, stubApi } from "@/test/authHelpers";
import { LinkRolesSection } from "./LinkRolesSection";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

function renderSection() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <MemoryRouter>
      <QueryClientProvider client={client}>
        <LinkRolesSection />
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

const report: LinkRoleReport = {
  verses: 31102,
  links: 250000,
  decided: { hebrew: { "1": 120, "1-main": 300 }, greek: { "3": 40 }, german: { "3b": 5 } },
  changed: { hebrew: { "1": 100, "1-main": 0 }, greek: { "3": 40 }, german: { "3b": 5 } },
  undecided: 7,
  undecidedByReason: { greek: { "several heads on morpheme": 7 } },
  mappingRows: 140,
  lemmaRows: 5,
  samples: {
    "3": [
      {
        hebRefId: 41001008,
        reference: "Mark 1:8",
        language: "greek",
        text: "ich taufe euch",
        changes: [{ word: "auf", before: "head G0450", after: "unit G0450" }],
      },
    ],
  },
};

function job(partial: Partial<LinkRoleJob> = {}): LinkRoleJob {
  return {
    id: "j1",
    mode: "dryRun",
    status: "succeeded",
    startedAt: "2026-11-17T10:00:00Z",
    log: ["10:00:00 Started"],
    result: { report, mappingRows: 0, lemmaRows: 0, mappingSkipped: 0, lemmasSkipped: 0 },
    ...partial,
  };
}

const applied = {
  id: 9,
  userId: 1,
  createdAt: "2026-11-16T10:00:00Z",
  mappingRows: 12,
  lemmaRows: 3,
};

describe("LinkRolesSection", () => {
  it("renders the dry-run report with counts, undecided reasons and sample links", async () => {
    const current: LinkRoleStatus = { job: job() };
    stubApi({ "GET /maintainer/link-roles/status": () => json(current) });
    renderSection();

    const table = await screen.findByRole("table", { name: "Changes per rule and language" });
    const row = within(table).getByRole("row", { name: /^1 – Hebrew morpheme by word class/ });
    expect(within(row).getByText(/100/)).toBeInTheDocument();
    expect(screen.getByText("several heads on morpheme")).toBeInTheDocument();
    expect(screen.getByText("Undecided: 7 links")).toBeInTheDocument();

    await userEvent.click(screen.getByText(/^Rule 3 – /));
    expect(screen.getByRole("link", { name: "Mark 1:8" })).toHaveAttribute(
      "href",
      "/bible/41/1#v8",
    );
    expect(screen.getByText(/head G0450 → unit G0450/)).toBeInTheDocument();
  });

  it("asks for confirmation with the row count before apply writes", async () => {
    const fetchMock = stubApi({
      "GET /maintainer/link-roles/status": () => json({ job: job() }),
      "POST /maintainer/link-roles/jobs": () => json(job({ mode: "apply" }), 202),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Apply …" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/145 rows will be written/)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);

    await userEvent.click(within(dialog).getByRole("button", { name: "Apply" }));
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ mode: "apply" });
  });

  it("disables apply without a report and undo without a change set", async () => {
    stubApi({ "GET /maintainer/link-roles/status": () => json({}) });
    renderSection();
    expect(await screen.findByText("No job has run yet.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Apply …" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Undo …" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Dry run" })).toBeEnabled();
  });

  it("enables undo with a change set and confirms before restoring", async () => {
    const fetchMock = stubApi({
      "GET /maintainer/link-roles/status": () =>
        json({ applied, linkRolesAppliedAt: "2026-11-16T10:00:00Z" }),
      "POST /maintainer/link-roles/jobs": () => json(job({ mode: "undo" }), 202),
    });
    renderSection();
    const undo = await screen.findByRole("button", { name: "Undo …" });
    await vi.waitFor(() => expect(undo).toBeEnabled());
    await userEvent.click(undo);
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/15 rows get their previous values/)).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole("button", { name: "Undo" }));
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ mode: "undo" });
  });

  it("shows a readable message when the server answers 409", async () => {
    stubApi({
      "GET /maintainer/link-roles/status": () => json({}),
      "POST /maintainer/link-roles/jobs": () =>
        json({ errors: { generalErrors: ["An agent run is active."] } }, 409),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Dry run" }));
    expect(
      await screen.findByText("The job cannot start now: An agent run is active."),
    ).toBeInTheDocument();
  });
});
