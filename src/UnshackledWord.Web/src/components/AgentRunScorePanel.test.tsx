import { QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { AgentScore } from "@/lib/agent/types";
import { stubFetch, testQueryClient } from "@/test/agentFixtures";
import { json } from "@/test/authHelpers";
import { AgentRunScorePanel } from "./AgentRunScorePanel";

const ratio = (correct: number, total: number) => ({
  correct,
  total,
  accuracy: total ? correct / total : undefined,
});

const score = (meets: boolean): AgentScore => ({
  runId: 7,
  mode: "lemma",
  profile: "ollama-cloud",
  model: "gemma4:31b",
  promptVersion: "greek-v1",
  runStatus: "completed",
  referenceVersion: "v1",
  computedAt: "2026-11-17T10:00:00Z",
  verses: {
    reference: 3,
    scored: 3,
    notInRun: 0,
    withProposal: meets ? 3 : 2,
    silent: 0,
    hardRejections: meets ? 0 : 1,
    extraRunVerses: 0,
  },
  lemmas: ratio(meets ? 100 : 90, 100),
  stts: ratio(95, 100),
  partLemmas: ratio(0, 0),
  roles: ratio(30, 30),
  morphemes: ratio(0, 0),
  roleMorphemes: ratio(30, 30),
  links: { expected: 30, actual: 29, matched: 28, precision: 0.9655, recall: 0.9333 },
  addedWords: { expected: 0, actual: 0, matched: 0 },
  flags: { total: 2, versesFlagged: 1, perVerse: 0.7, byCode: {} },
  cost: {
    inputTokens: 3000,
    cachedInputTokens: 0,
    outputTokens: 600,
    costUsd: 0.03,
    verses: 3,
    inputTokensPerVerse: 1000,
    outputTokensPerVerse: 200,
    costUsdPerVerse: 0.01,
  },
  bulkMerge: {
    meets,
    checks: [
      { code: "lemmas", value: meets ? 1 : 0.9, required: 0.98, ok: meets },
      { code: "role-morphemes", value: 1, required: 0.97, ok: true },
      { code: "hard-rejections", value: meets ? 0 : 1, required: 0, ok: meets },
    ],
    reasons: meets ? [] : ["Lemma accuracy 90 % is below 98 %."],
  },
  perVerse: [
    {
      ref: "Mk 1:17",
      bibleBookId: 41,
      chapter: 1,
      verse: 17,
      status: "proposal",
      lemmaErrors: [{ item: "3", text: "Fischer", expected: "Fischer", actual: "Fisch" }],
      roleErrors: [],
      missingLinks: [],
      extraLinks: [],
      flags: ["homograph"],
      inputTokens: 1000,
      outputTokens: 200,
      costUsd: 0.01,
    },
  ],
});

function renderPanel() {
  render(
    <QueryClientProvider client={testQueryClient()}>
      <MemoryRouter>
        <AgentRunScorePanel runId={7} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("AgentRunScorePanel", () => {
  it("computes the score on click and shows accuracies, cost and per-verse errors", async () => {
    stubFetch({ "GET /maintainer/agent-runs/7/score": () => json(score(true)) });
    renderPanel();
    expect(screen.getByText(/bevor du seine Vorschläge übernimmst/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Bewerten" }));
    expect(
      await screen.findByText("Merge-Schwelle erreicht: Massenübernahme möglich"),
    ).toBeInTheDocument();
    expect(screen.getAllByText(/100 %/).length).toBeGreaterThan(0);
    expect(screen.getByText(/Präzision 96,6 %/)).toBeInTheDocument();
    expect(screen.getByText(/Pro Vers: 1\.000 Tokens ein, 200 aus/)).toBeInTheDocument();
    await userEvent.click(screen.getByText("Mk 1:17"));
    expect(screen.getByText(/Fischer \(3\): Fischer → Fisch/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Vers im Leser öffnen" })).toHaveAttribute(
      "href",
      "/bible/41/1",
    );
    expect(screen.getByRole("link", { name: "Vorschläge des Laufs" })).toHaveAttribute(
      "href",
      "/edit/proposals/7",
    );
  });

  it("shows the stored score on open and recomputes on 'Neu bewerten'", async () => {
    let scored = 0;
    stubFetch({
      "GET /maintainer/agent-runs/7/score/stored": () => json(score(false)),
      "GET /maintainer/agent-runs/7/score": () => {
        scored++;
        return json(score(true));
      },
    });
    renderPanel();
    expect(await screen.findByText("Merge-Schwelle nicht erreicht")).toBeInTheDocument();
    expect(screen.getByText(/bewertet am/)).toBeInTheDocument();
    expect(scored).toBe(0);
    await userEvent.click(screen.getByRole("button", { name: "Neu bewerten" }));
    expect(
      await screen.findByText("Merge-Schwelle erreicht: Massenübernahme möglich"),
    ).toBeInTheDocument();
    expect(scored).toBe(1);
  });

  it("shows a failed bar with the failing checks", async () => {
    stubFetch({ "GET /maintainer/agent-runs/7/score": () => json(score(false)) });
    renderPanel();
    await userEvent.click(screen.getByRole("button", { name: "Bewerten" }));
    const bar = await screen.findByTestId("bulk-merge-bar");
    expect(within(bar).getByText("Merge-Schwelle nicht erreicht")).toBeInTheDocument();
    expect(within(bar).getByText(/✕ Harte Ablehnungen: 1 \(höchstens 0\)/)).toBeInTheDocument();
    expect(within(bar).getByText(/✓ Rollen und Morpheme/)).toBeInTheDocument();
  });

  it("explains how to get a reference set on a 409", async () => {
    stubFetch({
      "GET /maintainer/agent-runs/7/score": () =>
        json({ errors: { generalErrors: ["No reference file"] } }, 409),
    });
    renderPanel();
    await userEvent.click(screen.getByRole("button", { name: "Bewerten" }));
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent(
        "Noch kein Referenzsatz – Verse prüfen und einfrieren",
      ),
    );
  });
});
