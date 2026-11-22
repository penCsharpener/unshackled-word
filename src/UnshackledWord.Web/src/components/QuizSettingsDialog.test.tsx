import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { json } from "@/test/authHelpers";
import { QuizSettingsDialog } from "./QuizSettingsDialog";

const MC = "Lemma → Deutsch (Auswahl)";
const REVERSE = "Deutsch → Lemma (Auswahl)";

/** A scripted `api/vocab/settings`: GET answers the current state, PUT echoes the body (or refuses). */
function settingsApi(
  disabledQuizTypes: string[],
  options: { available?: string[]; putStatus?: number } = {},
) {
  const available = options.available ?? ["multiple_choice", "reverse_multiple_choice"];
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const path = url.replace(/^\/api/, "");
    if (path !== "/vocab/settings") return new Response("", { status: 404 });
    if (init?.method === "PUT") {
      if (options.putStatus) return json({ error: "refused" }, options.putStatus);
      const body = JSON.parse(String(init.body)) as { disabledQuizTypes: string[] };
      return json({ disabledQuizTypes: body.disabledQuizTypes, availableQuizTypes: available });
    }
    return json({ disabledQuizTypes, availableQuizTypes: available });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderDialog() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <QuizSettingsDialog open onClose={() => {}} />
    </QueryClientProvider>,
  );
}

function putBodies(fetchMock: ReturnType<typeof settingsApi>) {
  return fetchMock.mock.calls
    .filter(([, init]) => init?.method === "PUT")
    .map(([, init]) => JSON.parse(String(init?.body)) as unknown);
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("QuizSettingsDialog", () => {
  it("shows a switch per offered quiz type, both multiple-choice types on", async () => {
    settingsApi([]);
    renderDialog();
    expect(await screen.findByRole("switch", { name: MC })).toHaveAttribute("aria-checked", "true");
    expect(screen.getByRole("switch", { name: REVERSE })).toHaveAttribute("aria-checked", "true");
    expect(screen.queryByRole("switch", { name: "Hören" })).toBeNull();
  });

  it("shows a type the API starts to list, with its German label", async () => {
    settingsApi([], {
      available: ["multiple_choice", "reverse_multiple_choice", "listening", "verse_context"],
    });
    renderDialog();
    expect(await screen.findByRole("switch", { name: "Hören" })).toBeInTheDocument();
    expect(screen.getByRole("switch", { name: "Vers-Kontext" })).toBeInTheDocument();
  });

  it("switches a type off with a PUT of the disabled list", async () => {
    const fetchMock = settingsApi([]);
    const user = userEvent.setup();
    renderDialog();

    await user.click(await screen.findByRole("switch", { name: REVERSE }));
    expect(putBodies(fetchMock)).toEqual([{ disabledQuizTypes: ["reverse_multiple_choice"] }]);
    await waitFor(() =>
      expect(screen.getByRole("switch", { name: REVERSE })).toHaveAttribute(
        "aria-checked",
        "false",
      ),
    );
    // With reverse off, the one multiple-choice type left stays on and says so.
    expect(screen.getByRole("switch", { name: MC })).toBeDisabled();
    expect(screen.getByText("immer an")).toBeInTheDocument();
  });

  it("switches a type back on with a PUT without it", async () => {
    const fetchMock = settingsApi(["reverse_multiple_choice"]);
    const user = userEvent.setup();
    renderDialog();

    await user.click(await screen.findByRole("switch", { name: REVERSE }));
    expect(putBodies(fetchMock)).toEqual([{ disabledQuizTypes: [] }]);
  });

  it("cannot switch off the last multiple-choice type", async () => {
    const fetchMock = settingsApi(["reverse_multiple_choice"]);
    const user = userEvent.setup();
    renderDialog();

    const mc = await screen.findByRole("switch", { name: MC });
    expect(mc).toBeDisabled();
    await user.click(mc);
    expect(putBodies(fetchMock)).toEqual([]);
  });

  it("shows the server's refusal when both multiple-choice types would be off", async () => {
    settingsApi([], { putStatus: 400 });
    const user = userEvent.setup();
    renderDialog();

    await user.click(await screen.findByRole("switch", { name: REVERSE }));
    expect(
      await screen.findByText("Mindestens eine der beiden Auswahl-Arten muss an bleiben."),
    ).toBeInTheDocument();
    expect(screen.getByRole("switch", { name: REVERSE })).toHaveAttribute("aria-checked", "true");
  });
});
