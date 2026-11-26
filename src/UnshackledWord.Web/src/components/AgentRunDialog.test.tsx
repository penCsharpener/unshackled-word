import { QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import {
  bodiesOf,
  estimate,
  type Handler,
  profiles,
  run,
  stubFetch,
  testQueryClient,
} from "@/test/agentFixtures";
import { json } from "@/test/authHelpers";
import { AgentRunDialog, type AgentRunDialogProps } from "./AgentRunDialog";

function setup(props: Partial<AgentRunDialogProps> = {}) {
  const onClose = vi.fn();
  const onStarted = vi.fn();
  render(
    <QueryClientProvider client={testQueryClient()}>
      <MemoryRouter>
        <AgentRunDialog
          open
          onClose={onClose}
          onStarted={onStarted}
          bookName="Markus"
          initial={{ book: 41, fromChapter: 1, fromVerse: 1, toChapter: 1 }}
          {...props}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { onClose, onStarted };
}

const base: Record<string, Handler> = {
  "GET /maintainer/agent-runs/profiles": () => json(profiles),
  "POST /maintainer/agent-runs/estimate": () => json(estimate),
};

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("AgentRunDialog", () => {
  it("renders nothing while closed", () => {
    stubFetch(base);
    setup({ open: false });
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("disables unusable profiles with the reason and estimates the chapter", async () => {
    const fetchMock = stubFetch(base);
    setup();
    const select = await screen.findByLabelText(/Anbieter-Profil/);
    const unusable = await within(select).findByRole("option", {
      name: /claude.*kein API-Schlüssel/,
    });
    expect(unusable).toBeDisabled();
    expect(within(select).getByRole("option", { name: /ollama-cloud/ })).toBeEnabled();

    const section = await screen.findByRole("region", { name: "Schätzung" }, { timeout: 2000 });
    expect(within(section).getByText("195.000")).toBeInTheDocument();
    expect(within(section).getByText("120.000")).toBeInTheDocument();
    expect(within(section).getByText(/0,43/)).toBeInTheDocument();
    expect(within(section).getByText("45 (+ 2 übersprungen)")).toBeInTheDocument();
    expect(section).toHaveTextContent("30 % Reserve");
    expect(screen.queryByText(/Über der Obergrenze/)).toBeNull();
    // the whole chapter: toChapter set, toVerse left to the server
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs/estimate")[0]).toEqual({
      book: 41,
      fromChapter: 1,
      fromVerse: 1,
      toChapter: 1,
      profile: "ollama-cloud",
      mode: "redo",
    });
  });

  it("debounces range changes into one new estimate", async () => {
    const fetchMock = stubFetch(base);
    setup();
    await screen.findByRole("region", { name: "Schätzung" }, { timeout: 2000 });
    const toVerse = screen.getByLabelText(/Bis Vers/);
    await userEvent.type(toVerse, "45");
    await waitFor(
      () => {
        const bodies = bodiesOf(fetchMock, "POST /maintainer/agent-runs/estimate");
        expect(bodies.at(-1)).toMatchObject({ toVerse: 45 });
      },
      { timeout: 2000 },
    );
    const bodies = bodiesOf(fetchMock, "POST /maintainer/agent-runs/estimate");
    expect(bodies).toHaveLength(2); // initial range + one for "45", not one per keystroke
  });

  it("warns when the estimate is over the cap, and still lets the maintainer start", async () => {
    stubFetch({
      ...base,
      "POST /maintainer/agent-runs/estimate": () =>
        json({ ...estimate, exceedsCap: true, warnings: ["Prompt-Cache scheint aus"] }),
    });
    setup();
    expect(
      await screen.findByText(/Über der Obergrenze/, undefined, { timeout: 2000 }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Budget überschritten/)).toBeInTheDocument();
    expect(screen.getByText("Prompt-Cache scheint aus")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Lauf starten" })).toBeEnabled();
  });

  it("an own cap in the advanced section is compared with the estimate", async () => {
    stubFetch(base);
    setup();
    await screen.findByRole("region", { name: "Schätzung" }, { timeout: 2000 });
    expect(screen.queryByText(/Über der Obergrenze/)).toBeNull();
    await userEvent.click(screen.getByText("Erweitert"));
    await userEvent.type(screen.getByLabelText(/Obergrenze Tokens/), "100000");
    expect(await screen.findByText(/Über der Obergrenze/)).toBeInTheDocument();
  });

  it("shows the server's messages when the estimate fails and keeps Start off", async () => {
    stubFetch({
      ...base,
      "POST /maintainer/agent-runs/estimate": () =>
        json(
          { statusCode: 404, errors: { generalErrors: ["Im Bereich gibt es keine Wörter."] } },
          404,
        ),
    });
    setup();
    expect(
      await screen.findByText("Im Bereich gibt es keine Wörter.", undefined, { timeout: 2000 }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Lauf starten" })).toBeDisabled();
  });

  it("cannot start without a usable profile", async () => {
    stubFetch({ ...base, "GET /maintainer/agent-runs/profiles": () => json([profiles[1]]) });
    setup();
    expect(await screen.findByText(/Kein Profil ist nutzbar/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Lauf starten" })).toBeDisabled();
  });

  it("starts the run with mode, model and caps and then shows the progress", async () => {
    let status = "running";
    const fetchMock = stubFetch({
      ...base,
      "POST /maintainer/agent-runs": () => json(run, 202),
      "GET /maintainer/agent-runs/7": () => json({ ...run, status }),
    });
    const { onStarted, onClose } = setup();
    await screen.findByRole("region", { name: "Schätzung" }, { timeout: 2000 });
    await userEvent.selectOptions(screen.getByLabelText(/Modus/), "fill-open");
    await userEvent.click(screen.getByText("Erweitert"));
    await userEvent.type(screen.getByLabelText(/Modell/), "gemma4:12b");
    await userEvent.type(screen.getByLabelText(/Obergrenze Tokens/), "500000");
    await userEvent.type(screen.getByLabelText(/Obergrenze Kosten/), "1.5");
    const start = screen.getByRole("button", { name: "Lauf starten" });
    await waitFor(() => expect(start).toBeEnabled(), { timeout: 2000 });
    await userEvent.click(start);

    expect(await screen.findByRole("progressbar", { name: "Pakete" })).toHaveAttribute(
      "value",
      "1",
    );
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs")[0]).toMatchObject({
      book: 41,
      profile: "ollama-cloud",
      model: "gemma4:12b",
      mode: "fill-open",
      maxTokens: 500000,
      maxCostUsd: 1.5,
    });
    expect(onStarted).toHaveBeenCalledWith(expect.objectContaining({ id: 7 }));
    expect(await screen.findByText("Läuft")).toBeInTheDocument();
    expect(screen.getByText("50.000")).toBeInTheDocument();
    expect(screen.getByText(/0,12/)).toBeInTheDocument();
    const review = screen.getByRole("link", { name: "Vorschläge prüfen" });
    expect(review).toHaveAttribute("href", "/edit/proposals/7");
    status = "completed";
    await userEvent.click(screen.getByRole("button", { name: "Im Hintergrund weiterlaufen" }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("starts a lemma run and shows the verses it skipped without work", async () => {
    const fetchMock = stubFetch({
      ...base,
      "POST /maintainer/agent-runs": () => json({ ...run, mode: "lemma" }, 202),
      "GET /maintainer/agent-runs/7": () =>
        json({
          ...run,
          mode: "lemma",
          status: "completed",
          statusNote: "1 package(s) completed. 12 verse(s) without work skipped.",
        }),
    });
    setup();
    await screen.findByRole("region", { name: "Schätzung" }, { timeout: 2000 });
    expect(screen.getByRole("option", { name: "Lemmata & Rollen" })).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByLabelText(/Modus/), "lemma");
    expect(screen.getByText(/Die Verknüpfungen bleiben fest/)).toBeInTheDocument();
    const start = screen.getByRole("button", { name: "Lauf starten" });
    await waitFor(() => expect(start).toBeEnabled(), { timeout: 2000 });
    await userEvent.click(start);
    expect(await screen.findByText("Übersprungen (ohne Arbeit)")).toBeInTheDocument();
    expect(screen.getByText("12")).toBeInTheDocument();
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs")[0]).toMatchObject({ mode: "lemma" });
  });

  it("shows the server's reason when the start is refused", async () => {
    stubFetch({
      ...base,
      "POST /maintainer/agent-runs": () =>
        json({ statusCode: 400, errors: { generalErrors: ["Profil nicht nutzbar."] } }, 400),
    });
    setup();
    await screen.findByRole("region", { name: "Schätzung" }, { timeout: 2000 });
    const start = screen.getByRole("button", { name: "Lauf starten" });
    await waitFor(() => expect(start).toBeEnabled(), { timeout: 2000 });
    await userEvent.click(start);
    expect(await screen.findByText("Profil nicht nutzbar.")).toBeInTheDocument();
    expect(screen.queryByRole("progressbar")).toBeNull();
  });
});

describe("AgentRunDialog lxx-tag options (roadmap 0028.7f)", () => {
  it("offers 'also re-tag confirmed verses' only for lxx-tag and sends includeConfirmed and textForm", async () => {
    const fetchMock = stubFetch({
      ...base,
      "POST /maintainer/agent-runs": () => json(run, 202),
      "GET /maintainer/agent-runs/7": () => json({ ...run, status: "completed" }),
    });
    setup({
      modes: ["lxx-tag"],
      initial: { book: 27, fromChapter: 1, fromVerse: 1, toChapter: 1, toVerse: 3 },
      textForm: "th",
    });
    const checkbox = await screen.findByLabelText("auch bestätigte Verse neu taggen");
    expect(checkbox).not.toBeChecked();
    await waitFor(
      () =>
        expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs/estimate")[0]).toMatchObject({
          mode: "lxx-tag",
          textForm: "th",
        }),
      { timeout: 2000 },
    );
    expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs/estimate")[0]).not.toHaveProperty(
      "includeConfirmed",
    );
    await userEvent.click(checkbox);
    const start = screen.getByRole("button", { name: "Lauf starten" });
    await waitFor(
      () =>
        expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs/estimate").at(-1)).toMatchObject({
          includeConfirmed: true,
        }),
      { timeout: 2000 },
    );
    await waitFor(() => expect(start).toBeEnabled(), { timeout: 2000 });
    await userEvent.click(start);
    await waitFor(() =>
      expect(bodiesOf(fetchMock, "POST /maintainer/agent-runs")[0]).toMatchObject({
        includeConfirmed: true,
        textForm: "th",
        mode: "lxx-tag",
      }),
    );
  });

  it("has no such checkbox for the default modes", async () => {
    stubFetch(base);
    setup();
    await screen.findByLabelText(/Anbieter-Profil/);
    expect(screen.queryByLabelText("auch bestätigte Verse neu taggen")).toBeNull();
  });
});
