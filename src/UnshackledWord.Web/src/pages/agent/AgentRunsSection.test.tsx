import { QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { AgentRun } from "@/lib/agent/types";
import { bodiesOf, run, stubFetch, testQueryClient } from "@/test/agentFixtures";
import { json } from "@/test/authHelpers";
import { AgentRunsSection } from "./AgentRunsSection";

const books = {
  books: [
    {
      id: 41,
      testament: "NT",
      nameDe: "Markus",
      nameEn: "Mark",
      abbreviationDe: "Mk",
      abbreviationEn: "Mk",
      versesPerChapter: [45],
    },
  ],
};

const stopped: AgentRun = {
  ...run,
  id: 8,
  status: "budget-exceeded",
  statusNote: "Budget aufgebraucht",
  active: false,
  packagesCompleted: 2,
  openProposals: 0,
  proposedVerses: 30,
};

function renderSection() {
  render(
    <QueryClientProvider client={testQueryClient()}>
      <MemoryRouter>
        <AgentRunsSection />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const rowOf = (id: number) => screen.getByText(`#${id}`).closest("tr") as HTMLElement;

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("AgentRunsSection", () => {
  it("lists the runs with range, status, tokens, cost and the review link", async () => {
    stubFetch({
      "GET /meta-bible/structure": () => json(books),
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [run, stopped] }),
    });
    renderSection();
    const row = within(await screen.findByText("#7").then(() => rowOf(7)));
    expect(row.getByText("Markus 1,1–45")).toBeInTheDocument();
    expect(row.getByText("Läuft")).toBeInTheDocument();
    expect(row.getByText("50.000 ein / 10.000 aus")).toBeInTheDocument();
    expect(row.getByText(/0,12/)).toBeInTheDocument();
    expect(row.getByText("15 offen / 15")).toBeInTheDocument();
    expect(row.getByRole("progressbar", { name: "Pakete" })).toHaveAttribute("value", "1");
    expect(row.getByRole("link", { name: /Vorschläge prüfen \(15 offen\)/ })).toHaveAttribute(
      "href",
      "/edit/proposals/7",
    );
    const stoppedRow = within(rowOf(8));
    expect(stoppedRow.getByText("Budget überschritten")).toBeInTheDocument();
    expect(stoppedRow.getByText("Budget aufgebraucht")).toBeInTheDocument();
  });

  it("says so when there is no run and when loading fails", async () => {
    stubFetch({ "GET /maintainer/agent-runs?limit=50": () => json({ runs: [] }) });
    renderSection();
    expect(await screen.findByText("Noch kein Lauf.")).toBeInTheDocument();
    vi.unstubAllGlobals();
    stubFetch({ "GET /maintainer/agent-runs?limit=50": () => new Response("", { status: 500 }) });
    renderSection();
    expect(await screen.findByText("Die Läufe konnten nicht geladen werden.")).toBeInTheDocument();
  });

  it("polls while a run works and stops when it is done", async () => {
    let state: AgentRun = run;
    const fetchMock = stubFetch({
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [state] }),
    });
    renderSection();
    expect(
      await within(await screen.findByText("#7").then(() => rowOf(7))).findByText("Läuft"),
    ).toBeInTheDocument();
    state = { ...run, status: "completed", active: false, packagesCompleted: 3 };
    expect(
      await within(rowOf(7)).findByText("Fertig", undefined, { timeout: 4500 }),
    ).toBeInTheDocument();
    const calls = bodiesOf(fetchMock, "GET /maintainer/agent-runs?limit=50").length;
    await new Promise((resolve) => setTimeout(resolve, 2300));
    expect(bodiesOf(fetchMock, "GET /maintainer/agent-runs?limit=50")).toHaveLength(calls);
  }, 12000);

  it("cancels a running run only after an inline confirmation", async () => {
    const fetchMock = stubFetch({
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [run] }),
      "POST /maintainer/agent-runs/7/cancel": () =>
        json({ ...run, status: "cancelled", active: false }),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Abbrechen" }));
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs/7/cancel")).toHaveLength(0);
    await userEvent.click(screen.getByRole("button", { name: "Weiterlaufen lassen" }));
    expect(screen.getByRole("button", { name: "Abbrechen" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Abbrechen" }));
    await userEvent.click(screen.getByRole("button", { name: "Wirklich abbrechen" }));
    await waitFor(() =>
      expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs/7/cancel")).toHaveLength(1),
    );
  });

  it("resumes a budget-stopped run with raised caps from the mini form", async () => {
    const fetchMock = stubFetch({
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [stopped] }),
      "POST /maintainer/agent-runs/8/resume": () =>
        json({ ...stopped, status: "running", active: true }, 202),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Fortsetzen" }));
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs/8/resume")).toHaveLength(0);
    expect(screen.getByText(/Bisher verbraucht: 60.000 Tokens/)).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText("Neue Obergrenze Tokens"), "4000000");
    await userEvent.click(screen.getByRole("button", { name: "Fortsetzen", description: "" }));
    await waitFor(() =>
      expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs/8/resume")).toEqual([
        { maxTokens: 4000000 },
      ]),
    );
  });

  it("shows the server's message when resuming is refused", async () => {
    stubFetch({
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [stopped] }),
      "POST /maintainer/agent-runs/8/resume": () =>
        json(
          {
            statusCode: 400,
            errors: { maxTokens: ["Die Obergrenze muss über dem Verbrauch liegen."] },
          },
          400,
        ),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Fortsetzen" }));
    await userEvent.type(screen.getByLabelText("Neue Obergrenze Tokens"), "10");
    const buttons = screen.getAllByRole("button", { name: "Fortsetzen" });
    await userEvent.click(buttons[buttons.length - 1] as HTMLElement);
    expect(
      await screen.findByText("Die Obergrenze muss über dem Verbrauch liegen."),
    ).toBeInTheDocument();
  });

  it("resumes a failed run at once, without the cap form", async () => {
    const failed: AgentRun = { ...run, id: 9, status: "failed", active: false };
    const fetchMock = stubFetch({
      "GET /maintainer/agent-runs?limit=50": () => json({ runs: [failed] }),
      "POST /maintainer/agent-runs/9/resume": () =>
        json({ ...failed, status: "queued", active: true }, 202),
    });
    renderSection();
    await userEvent.click(await screen.findByRole("button", { name: "Fortsetzen" }));
    await waitFor(() =>
      expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs/9/resume")).toEqual([{}]),
    );
  });
});
