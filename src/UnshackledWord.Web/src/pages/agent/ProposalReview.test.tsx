import { QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { AgentProposalDiff } from "@/lib/agent/types";
import {
  bodiesOf,
  lemmaProposalDiff,
  markBooks,
  proposalDiff,
  stubFetch,
  testQueryClient,
} from "@/test/agentFixtures";
import { json } from "@/test/authHelpers";
import { valueMaps } from "@/test/fixtures/lemmas";
import { ProposalReview } from "./ProposalReview";

const diffUrl = "GET /maintainer/agent-proposals/5";

function setup(
  diff: AgentProposalDiff = proposalDiff,
  extra: Parameters<typeof stubFetch>[0] = {},
) {
  const fetchMock = stubFetch({
    "GET /meta-bible/structure": () => json(markBooks),
    [diffUrl]: () => json(diff),
    "GET /lemmas/value-maps": () => json(valueMaps),
    ...extra,
  });
  const onDone = vi.fn();
  const onEditingChange = vi.fn();
  render(
    <QueryClientProvider client={testQueryClient()}>
      <MemoryRouter>
        <ProposalReview proposalId={5} onDone={onDone} onEditingChange={onEditingChange} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { fetchMock, onDone, onEditingChange };
}

const merged = {
  id: 5,
  status: "merged",
  authorUserId: 77,
  reviewerUserId: 1,
  linksWritten: 3,
  lemmasWritten: 1,
};

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("ProposalReview", () => {
  it("shows the verse, status, model note, flags, the compare mapping and the lemma changes", async () => {
    setup();
    expect(await screen.findByRole("heading", { name: "Markus 1,8" })).toBeInTheDocument();
    expect(screen.getByText("offen")).toBeInTheDocument();
    expect(screen.getByText("„euch“ steht für ὑμᾶς.")).toBeInTheDocument();
    expect(screen.getByText("Mehrere deutsche Wörter für ein Urtextwort")).toBeInTheDocument();
    expect(
      screen.getByText("8 ἐβάπτισα: 2 German words share this source word"),
    ).toBeInTheDocument();
    const item = (text: string) =>
      screen.getByText((_, el) => el?.tagName === "LI" && el.textContent === text);
    expect(item("Verbunden: getauft – ἐβάπτισα")).toBeInTheDocument();
    expect(item("Gelöst: getauft – ὑμᾶς")).toBeInTheDocument();
    const table = screen.getByRole("table", { name: "Lemma-Änderungen" });
    expect(within(table).getByText("getauft", { selector: "th" })).toBeInTheDocument();
    expect(within(table).getByText("taufen")).toBeInTheDocument();
    expect(within(table).getByText("VVINF")).toBeInTheDocument();
    expect(within(table).queryByText("live geändert")).toBeNull();
  });

  it("falls back to the server message for a flag code without a label", async () => {
    setup({ ...proposalDiff, flags: [{ code: "brand-new", message: "Neue Prüfung schlug an" }] });
    expect(await screen.findByText("Neue Prüfung schlug an")).toBeInTheDocument();
  });

  it("merges with the button and reports done", async () => {
    const { onDone, fetchMock } = setup(proposalDiff, {
      "POST /maintainer/agent-proposals/5/merge": () => json(merged),
    });
    await userEvent.click(await screen.findByRole("button", { name: /Übernehmen/ }));
    await waitFor(() => expect(onDone).toHaveBeenCalledOnce());
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/merge")).toHaveLength(1);
  });

  it("merges with the m key, but not while a dialog or an editor is active", async () => {
    const { onDone, fetchMock } = setup(proposalDiff, {
      "POST /maintainer/agent-proposals/5/merge": () => json(merged),
    });
    await screen.findByRole("heading", { name: "Markus 1,8" });
    await userEvent.keyboard("d");
    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    await userEvent.keyboard("m");
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/merge")).toHaveLength(0);
    await userEvent.keyboard("{Escape}");
    await userEvent.keyboard("m");
    await waitFor(() => expect(onDone).toHaveBeenCalledOnce());
  });

  it("asks before discarding and only then calls the endpoint", async () => {
    const { onDone, fetchMock } = setup(proposalDiff, {
      "POST /maintainer/agent-proposals/5/discard": () => json({ id: 5, status: "discarded" }),
    });
    await userEvent.click(await screen.findByRole("button", { name: /Verwerfen/ }));
    const dialog = await screen.findByRole("dialog", { name: "Vorschlag verwerfen?" });
    expect(dialog).toHaveTextContent("Das lässt sich nicht rückgängig machen");
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/discard")).toHaveLength(0);
    await userEvent.click(within(dialog).getByRole("button", { name: "Behalten" }));
    expect(onDone).not.toHaveBeenCalled();
    await userEvent.keyboard("d");
    await userEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "Verwerfen" }),
    );
    await waitFor(() => expect(onDone).toHaveBeenCalledOnce());
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/discard")).toHaveLength(1);
  });

  describe("edit-merge", () => {
    it("starts from the proposal, edits links and lemmas and sends them with clearParts", async () => {
      const { onDone, fetchMock, onEditingChange } = setup(proposalDiff, {
        "POST /maintainer/agent-proposals/5/edit-merge": () =>
          json({ ...merged, status: "edited-merged" }),
      });
      await userEvent.click(await screen.findByRole("button", { name: /Bearbeiten/ }));
      expect(onEditingChange).toHaveBeenLastCalledWith(true);
      // the editor offers the lemma rows for editing
      const lemma = await screen.findByRole("textbox", { name: "Neues Lemma für getauft" });
      await userEvent.clear(lemma);
      await userEvent.type(lemma, "taufen ");
      await userEvent.selectOptions(
        screen.getByRole("combobox", { name: "STTS-Tag für getauft" }),
        "VVINF",
      );
      // change one link: euch -> ἐγὼ as well
      const node = (key: string) => document.querySelector(`[data-node="${key}"]`) as HTMLElement;
      await userEvent.click(node("g103"));
      await userEvent.click(node("o201"));
      await userEvent.click(screen.getByRole("button", { name: "Bearbeitet übernehmen" }));
      await waitFor(() => expect(onDone).toHaveBeenCalledOnce());
      expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/edit-merge")).toEqual([
        {
          links: [
            { elbWordId: 101, stepWordId: 201, isAddedWord: false },
            { elbWordId: 103, stepWordId: 201, isAddedWord: false },
            { elbWordId: 103, stepWordId: 203, isAddedWord: false },
            { elbWordId: 104, stepWordId: 202, isAddedWord: false },
          ],
          lemmas: [{ elbWordId: 104, lemma: "taufen", stts: "VVINF", partOfSpeech: "VERB" }],
          clearParts: false,
        },
      ]);
    });

    it('leaves a lemma row out with "Nicht übernehmen" and sends no lemmas', async () => {
      const { fetchMock } = setup(proposalDiff, {
        "POST /maintainer/agent-proposals/5/edit-merge": () => json(merged),
      });
      await userEvent.click(await screen.findByRole("button", { name: /Bearbeiten/ }));
      await userEvent.click(await screen.findByRole("button", { name: "Nicht übernehmen" }));
      await userEvent.click(screen.getByRole("button", { name: "Bearbeitet übernehmen" }));
      await waitFor(() =>
        expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/edit-merge")).toHaveLength(
          1,
        ),
      );
      expect(
        bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/edit-merge")[0].lemmas,
      ).toEqual([]);
    });

    it("refuses an empty new lemma before calling the server", async () => {
      const { fetchMock } = setup();
      await userEvent.click(await screen.findByRole("button", { name: /Bearbeiten/ }));
      await userEvent.clear(
        await screen.findByRole("textbox", { name: "Neues Lemma für getauft" }),
      );
      await userEvent.click(screen.getByRole("button", { name: "Bearbeitet übernehmen" }));
      expect(screen.getByRole("alert")).toHaveTextContent("Ein neues Lemma ist leer");
      expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/edit-merge")).toHaveLength(0);
    });

    it("keeps the review keys quiet inside the editor and leaves it with Escape", async () => {
      const { fetchMock } = setup(proposalDiff, {
        "POST /maintainer/agent-proposals/5/merge": () => json(merged),
      });
      await userEvent.click(await screen.findByRole("button", { name: /Bearbeiten/ }));
      await screen.findByRole("textbox", { name: "Neues Lemma für getauft" });
      await userEvent.keyboard("m");
      expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/merge")).toHaveLength(0);
      (document.querySelector("[data-node]") as HTMLElement).focus();
      await userEvent.keyboard("{Escape}");
      expect(await screen.findByRole("button", { name: /Bearbeiten/ })).toBeInTheDocument();
    });
  });

  describe("lemma mode (roles and morphemes)", () => {
    it("lists the role changes: morpheme with original and gloss, pronoun to added word, source", async () => {
      setup(lemmaProposalDiff);
      const table = await screen.findByRole("table", { name: "Rollen- und Morphemänderungen" });
      const und = within(table).getByText("Und", { selector: "th" }).closest("tr") as HTMLElement;
      // before: the main morpheme H0559 (no number stored), proposed: the H9001 prefix
      expect(und).toHaveTextContent("H0559");
      expect(und).toHaveTextContent("Hauptmorphem");
      expect(und).toHaveTextContent("H9001");
      expect(within(und).getByText("וַ")).toBeInTheDocument();
      expect(within(und).getByText("and")).toBeInTheDocument();
      expect(und).toHaveTextContent("Regeln");
      const er = within(table).getByText("er", { selector: "th" }).closest("tr") as HTMLElement;
      expect(er).toHaveTextContent("hinzugefügtes Wort zu „sprach“");
      expect(er).toHaveTextContent("KI");
      expect(screen.getByText("Pronomen wird hinzugefügtes Wort")).toBeInTheDocument();
      expect(screen.queryByRole("table", { name: "Lemma-Änderungen" })).toBeNull();
    });

    it("labels the lemma-mode flag codes", async () => {
      setup({
        ...lemmaProposalDiff,
        flags: ["lemma-human-edited", "lemma-unknown", "homograph", "multi-head"].map((code) => ({
          code,
          message: `${code} message`,
        })),
      });
      expect(await screen.findByText("Lemma wurde von einem Menschen gesetzt")).toBeInTheDocument();
      expect(screen.getByText("Lemma nicht im Lexikon")).toBeInTheDocument();
      expect(screen.getByText("Homograph: Lemma prüfen")).toBeInTheDocument();
      expect(screen.getByText("Mehrere Kerne auf einem Morphem")).toBeInTheDocument();
    });

    it("edit-merge starts from the proposed roles and saves role-aware", async () => {
      const { fetchMock } = setup(lemmaProposalDiff, {
        "POST /maintainer/agent-proposals/5/edit-merge": () =>
          json({ ...merged, status: "edited-merged" }),
      });
      await userEvent.click(await screen.findByRole("button", { name: /Bearbeiten/ }));
      await userEvent.click(await screen.findByRole("button", { name: "Bearbeitet übernehmen" }));
      await waitFor(() =>
        expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/edit-merge")).toHaveLength(
          1,
        ),
      );
      expect(bodiesOf(fetchMock, "POST /maintainer/agent-proposals/5/edit-merge")[0]).toEqual({
        links: [
          {
            elbWordId: 301,
            stepWordId: 401,
            isAddedWord: false,
            role: "head",
            strongsNumber: "H9001",
          },
          { elbWordId: 303, stepWordId: 401, isAddedWord: false, role: "head" },
          { elbWordId: 302, isAddedWord: true, parentElbWordId: 303 },
        ],
        lemmas: [],
        clearParts: false,
      });
    });
  });

  describe("stale and live-changed proposals", () => {
    it("shows the stale banner and disables merge and edit, but not discard", async () => {
      setup({
        ...proposalDiff,
        stale: true,
        staleReason: "die Wörter des Verses haben sich geändert",
      });
      expect(await screen.findByRole("alert")).toHaveTextContent("veraltet");
      expect(screen.getByRole("alert")).toHaveTextContent(
        "die Wörter des Verses haben sich geändert",
      );
      expect(screen.getByRole("button", { name: /Übernehmen/ })).toBeDisabled();
      expect(screen.getByRole("button", { name: /Bearbeiten/ })).toBeDisabled();
      expect(screen.getByRole("button", { name: /Verwerfen/ })).toBeEnabled();
    });

    it("marks a lemma that changed live and only allows the edit-merge", async () => {
      setup({
        ...proposalDiff,
        lemmaChanges: proposalDiff.lemmaChanges.map((c) => ({
          ...c,
          currentLemma: "taufen",
          changed: true,
        })),
      });
      expect(await screen.findByText("live geändert")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Übernehmen/ })).toBeDisabled();
      expect(screen.getByRole("button", { name: /Bearbeiten/ })).toBeEnabled();
      expect(screen.getByText(/nur über Bearbeiten/)).toBeInTheDocument();
    });

    it("locks everything but a note for a proposal that is not open any more", async () => {
      setup({ ...proposalDiff, status: "merged" });
      expect(
        await screen.findByText(/ist übernommen und kann nicht mehr geändert werden/),
      ).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Übernehmen/ })).toBeDisabled();
      expect(screen.getByRole("button", { name: /Verwerfen/ })).toBeDisabled();
    });
  });

  describe("errors", () => {
    it("409: reloads the proposal and says why", async () => {
      let loads = 0;
      const { onDone, fetchMock } = setup(proposalDiff, {
        [diffUrl]: () => {
          loads++;
          return json(loads > 1 ? { ...proposalDiff, stale: true } : proposalDiff);
        },
        "POST /maintainer/agent-proposals/5/merge": () =>
          json({ errors: { general: ["veraltet: das Lemma wurde geändert"] } }, 409),
      });
      await userEvent.click(await screen.findByRole("button", { name: /Übernehmen/ }));
      expect(
        await screen.findByText(/nicht mehr aktuell oder nicht mehr offen/),
      ).toBeInTheDocument();
      expect(screen.getByText("veraltet: das Lemma wurde geändert")).toBeInTheDocument();
      await waitFor(() =>
        expect(screen.getByRole("button", { name: /Übernehmen/ })).toBeDisabled(),
      );
      expect(onDone).not.toHaveBeenCalled();
      expect(
        fetchMock.mock.calls.filter(([u]) => String(u).endsWith("/agent-proposals/5")).length,
      ).toBeGreaterThan(1);
    });

    it("400: lists every hard-rule message", async () => {
      setup(proposalDiff, {
        "POST /maintainer/agent-proposals/5/merge": () =>
          json({ errors: { links: ["Regel eins verletzt.", "Regel zwei verletzt."] } }, 400),
      });
      await userEvent.click(await screen.findByRole("button", { name: /Übernehmen/ }));
      expect(await screen.findByText("Regel eins verletzt.")).toBeInTheDocument();
      expect(screen.getByText("Regel zwei verletzt.")).toBeInTheDocument();
    });

    it("403: says that editing is switched off", async () => {
      setup(proposalDiff, {
        "POST /maintainer/agent-proposals/5/merge": () => new Response("{}", { status: 403 }),
      });
      await userEvent.click(await screen.findByRole("button", { name: /Übernehmen/ }));
      expect(await screen.findByText("Bearbeiten ist abgeschaltet.")).toBeInTheDocument();
    });

    it("shows a load error", async () => {
      setup(proposalDiff, { [diffUrl]: () => new Response("", { status: 500 }) });
      expect(
        await screen.findByText("Der Vorschlag konnte nicht geladen werden."),
      ).toBeInTheDocument();
    });
  });
});
