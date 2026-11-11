import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { json, me, renderWithAuth, stubApi } from "@/test/authHelpers";
import { SignInPage } from "./SignInPage";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

async function fillAndSubmit() {
  await userEvent.type(screen.getByLabelText("Email"), "ada@example.org");
  await userEvent.type(screen.getByLabelText("Password"), "secret");
  await userEvent.click(screen.getByRole("button", { name: "Sign in" }));
}

describe("SignInPage", () => {
  it("posts the credentials with the session cookie", async () => {
    let signedIn = false;
    const fetchMock = stubApi({
      "GET /auth/me": () => (signedIn ? json(me) : new Response("", { status: 401 })),
      "POST /auth/login": () => {
        signedIn = true;
        return json(me);
      },
    });
    renderWithAuth(<SignInPage />);
    await fillAndSubmit();
    await vi.waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => url === "/api/auth/login");
      expect(call?.[1]).toMatchObject({
        credentials: "include",
        body: JSON.stringify({ email: "ada@example.org", password: "secret" }),
      });
    });
  });

  it("shows one generic message for a 401", async () => {
    stubApi({
      "GET /auth/me": () => new Response("", { status: 401 }),
      "POST /auth/login": () => new Response("", { status: 401 }),
    });
    renderWithAuth(<SignInPage />);
    await fillAndSubmit();
    expect(await screen.findByRole("alert")).toHaveTextContent("Email or password is incorrect.");
  });

  it("hides the passkey button without WebAuthn and links to the other flows", () => {
    stubApi({ "GET /auth/me": () => new Response("", { status: 401 }) });
    renderWithAuth(<SignInPage />);
    expect(screen.queryByRole("button", { name: "Sign in with a passkey" })).toBeNull();
    expect(screen.getByRole("link", { name: "Create account" })).toHaveAttribute(
      "href",
      "/register",
    );
    expect(screen.getByRole("link", { name: "Forgot password?" })).toHaveAttribute(
      "href",
      "/forgot-password",
    );
  });
});
