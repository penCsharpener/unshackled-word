import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { json, me, renderWithAuth, stubApi } from "@/test/authHelpers";
import { RegisterPage } from "./RegisterPage";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => vi.unstubAllGlobals());

const anonymous = () => new Response("", { status: 401 });

describe("RegisterPage", () => {
  it("shows the invite field only when the policy requires a code", async () => {
    stubApi({
      "GET /auth/me": anonymous,
      "GET /auth/registration-policy": () => json({ mode: "restricted", inviteCodeRequired: true }),
    });
    renderWithAuth(<RegisterPage />, "/register?invite=abc");
    expect(await screen.findByLabelText("Invite code")).toHaveValue("abc");
  });

  it("hides the invite field for open registration", async () => {
    const fetchMock = stubApi({
      "GET /auth/me": anonymous,
      "GET /auth/registration-policy": () => json({ mode: "open", inviteCodeRequired: false }),
    });
    renderWithAuth(<RegisterPage />);
    await vi.waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith("/api/auth/registration-policy", expect.anything()),
    );
    expect(screen.queryByLabelText("Invite code")).toBeNull();
  });

  it("confirms by email after registering and reports a duplicate", async () => {
    let status = 200;
    stubApi({
      "GET /auth/me": anonymous,
      "GET /auth/registration-policy": () => json({ mode: "open", inviteCodeRequired: false }),
      "POST /auth/register": () => (status === 200 ? json(me) : new Response("", { status })),
    });
    renderWithAuth(<RegisterPage />);
    await userEvent.type(screen.getByLabelText("Display name"), "Ada");
    await userEvent.type(screen.getByLabelText("Email"), "ada@example.org");
    await userEvent.type(screen.getByLabelText("Password"), "secret-pass");
    status = 409;
    await userEvent.click(screen.getByRole("button", { name: "Create account" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("already exists");
    status = 200;
    await userEvent.click(screen.getByRole("button", { name: "Create account" }));
    expect(await screen.findByText(/verification link to ada@example.org/)).toBeInTheDocument();
  });
});
