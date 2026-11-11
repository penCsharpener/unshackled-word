import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { json, me, renderWithAuth, stubApi } from "@/test/authHelpers";
import { UserMenu } from "./UserMenu";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("UserMenu", () => {
  it("shows a sign-in link when logged out", async () => {
    stubApi({ "GET /auth/me": () => new Response("", { status: 401 }) });
    renderWithAuth(<UserMenu />);
    const link = await screen.findByRole("link", { name: "Sign in" });
    expect(link).toHaveAttribute("href", "/login");
  });

  it("shows nothing while the session is loading", () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() => new Promise(() => {})),
    );
    renderWithAuth(<UserMenu />);
    expect(screen.queryByRole("link")).toBeNull();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("offers passkeys and sign out when logged in", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json(me),
      "POST /auth/logout": () => new Response(null, { status: 204 }),
    });
    renderWithAuth(<UserMenu />);
    await userEvent.click(await screen.findByRole("button", { name: /Ada/ }));
    expect(screen.getByRole("menuitem", { name: "Passkeys" })).toHaveAttribute(
      "href",
      "/account/passkeys",
    );
    await userEvent.click(screen.getByRole("menuitem", { name: "Sign out" }));
    expect(await screen.findByRole("link", { name: "Sign in" })).toBeInTheDocument();
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        "/api/auth/logout",
        expect.objectContaining({ method: "POST", credentials: "include" }),
      ),
    );
  });
});

describe("UserMenu admin link", () => {
  async function open(roles: string[]) {
    stubApi({ "GET /auth/me": () => json({ ...me, roles }) });
    renderWithAuth(<UserMenu />);
    await userEvent.click(await screen.findByRole("button", { name: /Ada/ }));
  }

  it("shows Invitations for admins", async () => {
    await open(["user", "admin"]);
    expect(screen.getByRole("menuitem", { name: "Invitations" })).toHaveAttribute(
      "href",
      "/admin/invitations",
    );
  });

  it("hides Invitations for other users", async () => {
    await open(["user"]);
    expect(screen.queryByRole("menuitem", { name: "Invitations" })).toBeNull();
  });
});
