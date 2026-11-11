import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { renderWithAuth, stubApi } from "@/test/authHelpers";
import { ResetPasswordPage } from "./ResetPasswordPage";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("ResetPasswordPage", () => {
  it("rejects mismatching passwords before calling the API", async () => {
    const fetchMock = stubApi({ "GET /auth/me": () => new Response("", { status: 401 }) });
    renderWithAuth(<ResetPasswordPage />, "/reset-password?token=x");
    await userEvent.type(screen.getByLabelText("New password"), "one");
    await userEvent.type(screen.getByLabelText("Repeat password"), "two");
    await userEvent.click(screen.getByRole("button", { name: "Save password" }));
    expect(screen.getByRole("alert")).toHaveTextContent("do not match");
    expect(fetchMock.mock.calls.some(([url]) => url === "/api/auth/reset-password")).toBe(false);
  });

  it("explains a missing token", () => {
    stubApi({ "GET /auth/me": () => new Response("", { status: 401 }) });
    renderWithAuth(<ResetPasswordPage />, "/reset-password");
    expect(screen.getByRole("alert")).toHaveTextContent("invalid");
  });

  it("shows success after a reset", async () => {
    stubApi({
      "GET /auth/me": () => new Response("", { status: 401 }),
      "POST /auth/reset-password": () => new Response(null, { status: 204 }),
    });
    renderWithAuth(<ResetPasswordPage />, "/reset-password?token=x");
    await userEvent.type(screen.getByLabelText("New password"), "same");
    await userEvent.type(screen.getByLabelText("Repeat password"), "same");
    await userEvent.click(screen.getByRole("button", { name: "Save password" }));
    expect(await screen.findByText("Your password has been changed.")).toBeInTheDocument();
  });
});
