import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { json, renderWithAuth, stubApi } from "@/test/authHelpers";
import { PasskeyManager } from "./PasskeyManager";

beforeEach(() => i18n.changeLanguage("en"));
afterEach(() => {
  vi.unstubAllGlobals();
  Reflect.deleteProperty(window, "PublicKeyCredential");
});

const supported = () =>
  Object.assign(window, {
    PublicKeyCredential: {
      parseCreationOptionsFromJSON: (o: unknown) => o,
      parseRequestOptionsFromJSON: (o: unknown) => o,
    },
  });

describe("PasskeyManager", () => {
  it("explains missing WebAuthn support", () => {
    stubApi({});
    renderWithAuth(<PasskeyManager />);
    expect(screen.getByText("This browser does not support passkeys.")).toBeInTheDocument();
  });

  it("lists and deletes passkeys", async () => {
    supported();
    const fetchMock = stubApi({
      "GET /auth/me": () => new Response("", { status: 401 }),
      "GET /auth/passkeys": () =>
        json({
          passkeys: [
            { id: 7, name: "Laptop", createdAt: "2026-01-02T00:00:00Z", lastUsedAt: null },
          ],
        }),
      "DELETE /auth/passkeys/7": () => new Response(null, { status: 204 }),
    });
    renderWithAuth(<PasskeyManager />);
    expect(await screen.findByText("Laptop")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Delete" }));
    expect(await screen.findByText("No passkeys set up yet.")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/auth/passkeys/7",
      expect.objectContaining({ method: "DELETE" }),
    );
  });
});
