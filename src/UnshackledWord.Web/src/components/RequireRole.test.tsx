import { screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { json, me, renderWithAuth, stubApi } from "@/test/authHelpers";
import { RequireRole } from "./RequireRole";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("RequireRole", () => {
  it("renders the children with the role", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: ["user", "admin"] }) });
    renderWithAuth(<RequireRole requires="admin">secret</RequireRole>);
    expect(await screen.findByText("secret")).toBeInTheDocument();
  });

  it("shows the forbidden note without the role", async () => {
    stubApi({ "GET /auth/me": () => json({ ...me, roles: ["user", "maintainer"] }) });
    renderWithAuth(<RequireRole requires="admin">secret</RequireRole>);
    expect(await screen.findByRole("alert")).toHaveTextContent("You do not have access");
    expect(screen.queryByText("secret")).toBeNull();
  });
});
