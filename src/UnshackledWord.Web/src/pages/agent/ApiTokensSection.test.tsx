import { QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { ApiToken } from "@/lib/agent/types";
import { bodiesOf, stubFetch, testQueryClient } from "@/test/agentFixtures";
import { json } from "@/test/authHelpers";
import { ApiTokensSection } from "./ApiTokensSection";

const SECRET = "uwt_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-AbCdE";

const users = [
  { id: 5, name: "ai:gemma4", roles: ["ai"] },
  { id: 2, name: "Ada", roles: ["maintainer"] },
];

const token = (over: Partial<ApiToken>): ApiToken => ({
  id: 1,
  userId: 5,
  name: "Claude Code",
  prefix: "uwt_AbCd",
  scope: "propose",
  createdByUserId: 2,
  createdAt: "2026-11-01T10:00:00Z",
  expiresAt: "2027-01-01T10:00:00Z",
  active: true,
  ...over,
});

const tokens = [
  token({ id: 1 }),
  token({ id: 2, name: "Alt", revokedAt: "2026-11-05T10:00:00Z", active: false }),
  token({ id: 3, name: "Abgelaufen", expiresAt: "2026-10-01T10:00:00Z", active: false }),
];

let client = testQueryClient();
function renderSection() {
  client = testQueryClient();
  render(
    <QueryClientProvider client={client}>
      <ApiTokensSection />
    </QueryClientProvider>,
  );
}

const base = {
  "GET /maintainer/api-tokens": () => json({ tokens }),
  "GET /maintainer/api-tokens/users": () => json({ users }),
};

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(() => vi.unstubAllGlobals());

describe("ApiTokensSection", () => {
  it("lists tokens with a status per token and the user's name", async () => {
    stubFetch(base);
    renderSection();
    const row = (name: string) => within(screen.getByText(name).closest("tr") as HTMLElement);
    expect(await screen.findByText("Claude Code")).toBeInTheDocument();
    expect(row("Claude Code").getByText("aktiv")).toBeInTheDocument();
    expect(row("Claude Code").getByText("ai:gemma4")).toBeInTheDocument();
    expect(row("Claude Code").getByText("uwt_AbCd…")).toBeInTheDocument();
    expect(row("Claude Code").getByText("nie")).toBeInTheDocument();
    expect(row("Alt").getByText("widerrufen")).toBeInTheDocument();
    expect(row("Alt").queryByRole("button", { name: "Widerrufen" })).toBeNull();
    expect(row("Abgelaufen").getByText("abgelaufen")).toBeInTheDocument();
  });

  it("offers ai users first, 30 days by default, and creates for the chosen user", async () => {
    const fetchMock = stubFetch({
      ...base,
      "POST /maintainer/api-tokens": () =>
        json({ token: token({ id: 9, name: "Neu" }), secret: SECRET }),
    });
    renderSection();
    const user = await screen.findByLabelText(/Benutzer \/ Modell/);
    await waitFor(() => expect(within(user).getAllByRole("option")).toHaveLength(3));
    const options = within(user).getAllByRole("option");
    expect(options[0]).toHaveTextContent("ai:gemma4");
    expect(options[1]).toHaveTextContent("Ada");
    expect(options[2]).toHaveTextContent("Neues Modell");
    expect(screen.getByLabelText(/Gültig für/)).toHaveValue("30");
    await userEvent.selectOptions(screen.getByLabelText(/Gültig für/), "90");
    await userEvent.type(screen.getByLabelText("Name"), "Neu");
    await userEvent.click(screen.getByRole("button", { name: "Token erstellen" }));
    await screen.findByRole("dialog");
    expect(bodiesOf(fetchMock, "POST /maintainer/api-tokens")).toEqual([
      { name: "Neu", expiresInDays: 90, userId: 5 },
    ]);
  });

  it("creates the ai user for a new model by sending the model name", async () => {
    const fetchMock = stubFetch({
      "GET /maintainer/api-tokens": () => json({ tokens: [] }),
      "GET /maintainer/api-tokens/users": () => json({ users: [] }),
      "POST /maintainer/api-tokens": () =>
        json({ token: token({ id: 9, name: "Gemma" }), secret: SECRET }),
    });
    renderSection();
    expect(await screen.findByText("Noch kein Token.")).toBeInTheDocument();
    const create = screen.getByRole("button", { name: "Token erstellen" });
    await userEvent.type(screen.getByLabelText("Name"), "Gemma");
    expect(create).toBeDisabled(); // a new model needs its name
    await userEvent.type(screen.getByLabelText("Modell"), " gemma4:31b ");
    await userEvent.click(create);
    await screen.findByRole("dialog");
    expect(bodiesOf(fetchMock, "POST /maintainer/api-tokens")).toEqual([
      { name: "Gemma", expiresInDays: 30, model: "gemma4:31b" },
    ]);
  });

  it("shows the secret once, cannot be left before it is copied, and never caches it", async () => {
    stubFetch({
      ...base,
      "POST /maintainer/api-tokens": () =>
        json({ token: token({ id: 9, name: "Neu" }), secret: SECRET }),
    });
    const user = userEvent.setup();
    renderSection();
    await user.type(await screen.findByLabelText("Name"), "Neu");
    await user.click(screen.getByRole("button", { name: "Token erstellen" }));

    const dialog = await screen.findByRole("dialog");
    const field = within(dialog).getByLabelText("Token");
    expect(field).toHaveValue(SECRET);
    expect(field).toHaveAttribute("readonly");
    expect(within(dialog).getByText(/nur jetzt angezeigt/)).toBeInTheDocument();
    // MCP snippet with the bearer token and the address
    const snippet = within(dialog).getByText(/"mcpServers"/);
    expect(snippet).toHaveTextContent(`"Authorization": "Bearer ${SECRET}"`);
    expect(snippet).toHaveTextContent('"url": "http');

    // not dismissible: Esc and Fertig do nothing yet
    const done = within(dialog).getByRole("button", { name: "Fertig" });
    expect(done).toBeDisabled();
    await user.keyboard("{Escape}");
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    await user.click(within(dialog).getByRole("button", { name: "Kopieren" }));
    expect(await within(dialog).findByRole("button", { name: "Kopiert" })).toBeInTheDocument();
    expect(await navigator.clipboard.readText()).toBe(SECRET);
    expect(done).toBeEnabled();

    // the secret is in no query or mutation cache
    const cached = JSON.stringify([
      client
        .getQueryCache()
        .getAll()
        .map((q) => q.state.data),
      client
        .getMutationCache()
        .getAll()
        .map((m) => m.state.data),
    ]);
    expect(cached).not.toContain(SECRET);

    await user.click(done);
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.queryByDisplayValue(SECRET)).toBeNull();
    expect(document.body).not.toHaveTextContent(SECRET);
  });

  it("can also be confirmed without the clipboard", async () => {
    stubFetch({
      ...base,
      "POST /maintainer/api-tokens": () => json({ token: token({ id: 9 }), secret: SECRET }),
    });
    const user = userEvent.setup();
    renderSection();
    await user.type(await screen.findByLabelText("Name"), "Neu");
    await user.click(screen.getByRole("button", { name: "Token erstellen" }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByLabelText("Ich habe das Token gespeichert"));
    await user.click(within(dialog).getByRole("button", { name: "Fertig" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("shows the server's message when creating is refused", async () => {
    stubFetch({
      ...base,
      "POST /maintainer/api-tokens": () =>
        json(
          {
            statusCode: 400,
            errors: { userId: ["Tokens gibt es nur für ai, maintainer oder admin."] },
          },
          400,
        ),
    });
    renderSection();
    await userEvent.type(await screen.findByLabelText("Name"), "Neu");
    await userEvent.click(screen.getByRole("button", { name: "Token erstellen" }));
    expect(
      await screen.findByText("Tokens gibt es nur für ai, maintainer oder admin."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("revokes in two steps", async () => {
    const fetchMock = stubFetch({
      ...base,
      "POST /maintainer/api-tokens/1/revoke": () => new Response(null, { status: 204 }),
    });
    renderSection();
    const row = async () =>
      within((await screen.findByText("Claude Code")).closest("tr") as HTMLElement);
    await userEvent.click((await row()).getByRole("button", { name: "Widerrufen" }));
    expect(bodiesOf(fetchMock, "POST /maintainer/api-tokens/1/revoke")).toHaveLength(0);
    await userEvent.click(screen.getByRole("button", { name: "Behalten" }));
    expect(screen.queryByRole("button", { name: /Wirklich widerrufen/ })).toBeNull();
    await userEvent.click((await row()).getByRole("button", { name: "Widerrufen" }));
    await userEvent.click(screen.getByRole("button", { name: "Wirklich widerrufen: Claude Code" }));
    await waitFor(() =>
      expect(bodiesOf(fetchMock, "POST /maintainer/api-tokens/1/revoke")).toHaveLength(1),
    );
  });
});
