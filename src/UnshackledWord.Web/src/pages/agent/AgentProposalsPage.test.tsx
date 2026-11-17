import { QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { AgentProposalSummary } from "@/lib/agent/types";
import { AuthProvider } from "@/lib/auth/AuthContext";
import {
  bodiesOf,
  markBooks,
  proposalDiff,
  run,
  stubFetch,
  summary,
  testQueryClient,
} from "@/test/agentFixtures";
import { json, me } from "@/test/authHelpers";
import { AgentProposalsPage } from "./AgentProposalsPage";

const proposals: AgentProposalSummary[] = [
  summary(11, 1),
  summary(12, 2, { flagCount: 2, hasLemmaChanges: true, hasNote: true }),
  summary(13, 3, { status: "merged" }),
  summary(14, 4, { hasParts: true }),
];

const Where = () => <p data-testid="where">{useLocation().pathname}</p>;

function setup(
  route: string,
  handlers: Parameters<typeof stubFetch>[0] = {},
  roles: string[] = ["maintainer"],
  list: AgentProposalSummary[] = proposals,
) {
  const fetchMock = stubFetch({
    "GET /auth/me": () => json({ ...me, roles }),
    "GET /meta-bible/structure": () => json(markBooks),
    "GET /maintainer/agent-runs?limit=50": () => json({ runs: [run] }),
    "GET /maintainer/agent-runs/7": () => json(run),
    "GET /maintainer/agent-runs/7/proposals": () => json({ runId: 7, proposals: list }),
    ...Object.fromEntries(
      list.map((p) => [
        `GET /maintainer/agent-proposals/${p.id}`,
        () => json({ ...proposalDiff, id: p.id, verse: p.verse, status: p.status }),
      ]),
    ),
    ...handlers,
  });
  render(
    <QueryClientProvider client={testQueryClient()}>
      <MemoryRouter initialEntries={[route]}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<p>Anmelden</p>} />
            <Route path="/edit/proposals/:runId?/:proposalId?" element={<AgentProposalsPage />} />
          </Routes>
          <Where />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return fetchMock;
}

const where = () => screen.getByTestId("where").textContent;
const list = () => screen.getByRole("list", { name: "Vorschläge des Laufs" });

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("AgentProposalsPage", () => {
  it("sends anonymous visitors to the sign-in and others to the forbidden note", async () => {
    setup("/edit/proposals", { "GET /auth/me": () => new Response("", { status: 401 }) });
    expect(await screen.findByText("Anmelden")).toBeInTheDocument();
  });

  it("shows a forbidden note without a write role", async () => {
    setup("/edit/proposals/7", {}, ["user"]);
    expect(await screen.findByRole("alert")).toHaveTextContent("keinen Zugriff");
  });

  it("lists the runs that have proposals and links to their review", async () => {
    setup("/edit/proposals", {
      "GET /maintainer/agent-runs?limit=50": () =>
        json({ runs: [run, { ...run, id: 8, proposedVerses: 0, openProposals: 0 }] }),
    });
    const link = await screen.findByRole("link", { name: /#7/ });
    expect(link).toHaveAttribute("href", "/edit/proposals/7");
    expect(link).toHaveTextContent("Markus 1,1–45");
    expect(link).toHaveTextContent("15 offen / 15");
    expect(screen.queryByRole("link", { name: /#8/ })).toBeNull();
  });

  describe("run review", () => {
    it("shows the run header and the open proposals, with the first one under review", async () => {
      setup("/edit/proposals/7");
      expect(await screen.findByText(/Lauf 7 · Markus 1,1–45/)).toBeInTheDocument();
      expect(screen.getByText("ollama-cloud · gemma4:31b")).toBeInTheDocument();
      const items = within(await waitForList()).getAllByRole("link");
      expect(items.map((a) => a.getAttribute("href"))).toEqual([
        "/edit/proposals/7/11",
        "/edit/proposals/7/12",
        "/edit/proposals/7/14",
      ]);
      expect(items[0]).toHaveAttribute("aria-current", "true");
      expect(await screen.findByRole("heading", { name: "Markus 1,1" })).toBeInTheDocument();
    });

    it("marks flags, lemma changes, parts and notes in the list", async () => {
      setup("/edit/proposals/7");
      const second = (await waitForList()).querySelectorAll("li")[1] as HTMLElement;
      expect(within(second).getByText("2 Markierungen des Modells")).toBeInTheDocument();
      expect(within(second).getByText("Lemmas geändert")).toBeInTheDocument();
      expect(within(second).getByText("Notiz")).toBeInTheDocument();
      const third = list().querySelectorAll("li")[2] as HTMLElement;
      expect(within(third).getByText("Wortteile")).toBeInTheDocument();
    });

    it("filters by status and by flags", async () => {
      setup("/edit/proposals/7");
      await waitForList();
      await userEvent.selectOptions(screen.getByLabelText("Status:"), "all");
      expect(within(list()).getAllByRole("link")).toHaveLength(4);
      await userEvent.selectOptions(screen.getByLabelText("Status:"), "merged");
      expect(within(list()).getAllByRole("link")).toHaveLength(1);
      await userEvent.selectOptions(screen.getByLabelText("Status:"), "open");
      await userEvent.click(screen.getByRole("checkbox", { name: "nur markierte" }));
      expect(within(list()).getAllByRole("link")).toHaveLength(1);
      expect(within(list()).getByRole("link")).toHaveAttribute("href", "/edit/proposals/7/12");
    });

    it("marks role changes and filters by them; the filter is only offered when a proposal has some", async () => {
      setup(
        "/edit/proposals/7",
        {},
        ["maintainer"],
        [summary(11, 1), summary(15, 5, { hasRoleChanges: true, hasLemmaChanges: true })],
      );
      const second = (await waitForList()).querySelectorAll("li")[1] as HTMLElement;
      expect(within(second).getByText("Rollen geändert")).toHaveAttribute(
        "title",
        "mit Rollen-, Morphem- oder Pronomenänderungen",
      );
      await userEvent.click(screen.getByRole("checkbox", { name: "nur Rollenänderungen" }));
      expect(within(list()).getAllByRole("link")).toHaveLength(1);
      expect(within(list()).getByRole("link")).toHaveAttribute("href", "/edit/proposals/7/15");
    });

    it("offers no role filter for a run without role changes", async () => {
      setup("/edit/proposals/7");
      await waitForList();
      expect(screen.queryByRole("checkbox", { name: "nur Rollenänderungen" })).toBeNull();
    });

    it("moves through the proposals with j, k and the arrow keys", async () => {
      setup("/edit/proposals/7/11");
      await screen.findByRole("heading", { name: "Markus 1,1" });
      await userEvent.keyboard("j");
      expect(where()).toBe("/edit/proposals/7/12");
      await userEvent.keyboard("{ArrowRight}");
      expect(where()).toBe("/edit/proposals/7/14");
      await userEvent.keyboard("j");
      expect(where()).toBe("/edit/proposals/7/14");
      await userEvent.keyboard("k");
      expect(where()).toBe("/edit/proposals/7/12");
      await userEvent.keyboard("{ArrowLeft}");
      expect(where()).toBe("/edit/proposals/7/11");
    });

    it("keeps the keys quiet while typing and in dialogs", async () => {
      setup("/edit/proposals/7/11");
      await screen.findByRole("heading", { name: "Markus 1,1" });
      await userEvent.keyboard("?");
      const help = await screen.findByRole("dialog", { name: "Tastenkürzel" });
      await userEvent.keyboard("j");
      expect(where()).toBe("/edit/proposals/7/11");
      await userEvent.click(within(help).getByRole("button", { name: "Schließen" }));
      await userEvent.click(await screen.findByRole("button", { name: /^Bearbeiten/ }));
      await userEvent.click(
        await screen.findByRole("textbox", { name: "Neues Lemma für getauft" }),
      );
      await userEvent.keyboard("jk");
      expect(where()).toBe("/edit/proposals/7/11");
    });

    it("has a stepper for narrow screens and an accessible shortcut button", async () => {
      setup("/edit/proposals/7/12");
      expect(await screen.findByText("2 / 3")).toBeInTheDocument();
      await userEvent.click(screen.getByRole("button", { name: "Nächster" }));
      expect(where()).toBe("/edit/proposals/7/14");
      expect(screen.getByText("3 / 3")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Nächster" })).toBeDisabled();
      await userEvent.click(screen.getByRole("button", { name: "Vorheriger" }));
      expect(where()).toBe("/edit/proposals/7/12");
      expect(screen.getByRole("button", { name: "Tastenkürzel anzeigen" })).toHaveAttribute(
        "aria-keyshortcuts",
        "?",
      );
    });

    it("moves on to the next open proposal after a merge", async () => {
      const fetchMock = setup("/edit/proposals/7/11", {
        "POST /maintainer/agent-proposals/11/merge": () =>
          json({
            id: 11,
            status: "merged",
            authorUserId: 77,
            reviewerUserId: 1,
            linksWritten: 3,
            lemmasWritten: 0,
          }),
      });
      await userEvent.click(await screen.findByRole("button", { name: /Übernehmen/ }));
      await waitFor(() => expect(where()).toBe("/edit/proposals/7/12"));
      expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/11/merge")).toHaveLength(1);
    });

    it("shows the stale banner of the selected proposal", async () => {
      setup("/edit/proposals/7/11", {
        "GET /maintainer/agent-proposals/11": () =>
          json({ ...proposalDiff, id: 11, stale: true, staleReason: "Wörter geändert" }),
      });
      expect(await screen.findByText("Wörter geändert")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Übernehmen/ })).toBeDisabled();
    });

    it("says so when the run has no proposals", async () => {
      setup("/edit/proposals/7", {}, ["maintainer"], []);
      expect(await screen.findByText("Dieser Lauf hat keine Vorschläge.")).toBeInTheDocument();
    });
  });

  describe("merge everything without flags", () => {
    it("names the count, needs the checkbox and shows the result with failures", async () => {
      const fetchMock = setup("/edit/proposals/7", {
        "POST /maintainer/agent-runs/7/proposals/merge-unflagged": () =>
          json({
            runId: 7,
            merged: 1,
            skippedFlagged: 1,
            failed: [
              {
                id: 14,
                chapter: 1,
                verse: 4,
                reason: "stale",
                messages: ["veraltet: Lemma geändert"],
              },
            ],
          }),
      });
      await userEvent.click(
        await screen.findByRole("button", { name: "Alle ohne Markierung übernehmen (2)" }),
      );
      const dialog = await screen.findByRole("dialog", { name: "Alle ohne Markierung übernehmen" });
      expect(dialog).toHaveTextContent("2 offene Vorschläge ohne Markierung werden übernommen.");
      expect(dialog).toHaveTextContent("1 markierter Vorschlag bleibt zur Prüfung offen.");
      const confirm = within(dialog).getByRole("button", { name: "2 übernehmen" });
      expect(confirm).toBeDisabled();
      await userEvent.click(
        within(dialog).getByRole("checkbox", {
          name: "Das ändert die Live-Zuordnung von 2 Versen",
        }),
      );
      expect(confirm).toBeEnabled();
      await userEvent.click(confirm);
      expect(await within(dialog).findByText("1 Vorschlag übernommen.")).toBeInTheDocument();
      expect(within(dialog).getByText("1 markierter Vorschlag blieb offen.")).toBeInTheDocument();
      expect(
        within(dialog).getByText(/1 Vorschlag konnte nicht übernommen werden/),
      ).toBeInTheDocument();
      expect(within(dialog).getByRole("link", { name: "Markus 1,4" })).toHaveAttribute(
        "href",
        "/edit/proposals/7/14",
      );
      expect(within(dialog).getByText(/veraltet: veraltet: Lemma geändert/)).toBeInTheDocument();
      expect(
        bodiesOf(fetchMock, "POST /maintainer/agent-runs/7/proposals/merge-unflagged"),
      ).toEqual([{ confirm: true }]);
    });

    it("is disabled when nothing without flags is open", async () => {
      setup("/edit/proposals/7", {}, ["maintainer"], [summary(21, 1, { flagCount: 1 })]);
      expect(
        await screen.findByRole("button", { name: "Alle ohne Markierung übernehmen (0)" }),
      ).toBeDisabled();
    });
  });
});

async function waitForList() {
  const found = await screen.findByRole("list", { name: "Vorschläge des Laufs" });
  await waitFor(() => expect(within(found).getAllByRole("listitem").length).toBeGreaterThan(0));
  return found;
}
