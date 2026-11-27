import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { json, me, renderWithAuth, stubApi } from "@/test/authHelpers";
import { AdminMenu } from "./AdminMenu";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("AdminMenu", () => {
  it("offers Invitations and the admin log to admins", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: ["user", "admin"] }) });
    renderWithAuth(<AdminMenu />);
    await userEvent.click(await screen.findByRole("button", { name: "Administration" }));
    expect(screen.getByRole("menuitem", { name: "Initial setup" })).toHaveAttribute(
      "href",
      "/edit/setup",
    );
    expect(screen.getByRole("menuitem", { name: "Users" })).toHaveAttribute("href", "/admin/users");
    expect(screen.getByRole("menuitem", { name: "Invitations" })).toHaveAttribute(
      "href",
      "/admin/invitations",
    );
    expect(screen.getByRole("menuitem", { name: "Admin log" })).toHaveAttribute(
      "href",
      "/admin/log",
    );
  });

  it("shows no gear to maintainers", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => json({ ...me, roles: ["user", "maintainer"] }),
    });
    renderWithAuth(<AdminMenu />);
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(screen.queryByRole("button", { name: "Administration" })).toBeNull();
  });

  it("shows no gear when logged out", () => {
    stubApi({ "GET /auth/me": () => new Response("", { status: 401 }) });
    renderWithAuth(<AdminMenu />);
    expect(screen.queryByRole("button", { name: "Administration" })).toBeNull();
  });
});
