import { screen } from "@testing-library/react";
import { StrictMode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { renderWithAuth, stubApi } from "@/test/authHelpers";
import { VerifyEmailPage } from "./VerifyEmailPage";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

describe("VerifyEmailPage", () => {
  it("verifies the token exactly once, even under StrictMode", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": () => new Response("", { status: 401 }),
      "POST /auth/verify-email": () => new Response(null, { status: 204 }),
    });
    renderWithAuth(
      <StrictMode>
        <VerifyEmailPage />
      </StrictMode>,
      "/verify-email?token=t0k",
    );
    expect(await screen.findByText(/is verified/)).toBeInTheDocument();
    const calls = fetchMock.mock.calls.filter(([url]) => url === "/api/auth/verify-email");
    expect(calls).toHaveLength(1);
    expect(calls[0]?.[1]?.body).toBe(JSON.stringify({ token: "t0k" }));
  });

  it("fails without a token", () => {
    stubApi({ "GET /auth/me": () => new Response("", { status: 401 }) });
    renderWithAuth(<VerifyEmailPage />, "/verify-email");
    expect(screen.getByRole("alert")).toHaveTextContent("invalid or has expired");
  });
});
