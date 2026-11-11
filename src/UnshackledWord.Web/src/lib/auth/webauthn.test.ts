import { afterEach, describe, expect, it, vi } from "vitest";
import {
  createPasskey,
  getPasskeyAssertion,
  isPasskeySupported,
  WebAuthnCancelledError,
  WebAuthnUnsupportedError,
} from "./webauthn";

class FakeCredential {
  toJSON() {
    return { id: "abc" };
  }
}

function stubWebAuthn(get: () => Promise<unknown>) {
  vi.stubGlobal(
    "PublicKeyCredential",
    Object.assign(FakeCredential, {
      parseCreationOptionsFromJSON: (o: unknown) => o,
      parseRequestOptionsFromJSON: (o: unknown) => o,
    }),
  );
  vi.stubGlobal(
    "window",
    Object.assign(window, { PublicKeyCredential: globalThis.PublicKeyCredential }),
  );
  Object.defineProperty(navigator, "credentials", {
    configurable: true,
    value: { get, create: get },
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
  Reflect.deleteProperty(window, "PublicKeyCredential");
});

describe("webauthn", () => {
  it("reports unsupported browsers and refuses ceremonies", async () => {
    expect(isPasskeySupported()).toBe(false);
    await expect(getPasskeyAssertion({} as never)).rejects.toBeInstanceOf(WebAuthnUnsupportedError);
  });

  it("returns the credential as JSON", async () => {
    stubWebAuthn(async () => new FakeCredential());
    expect(isPasskeySupported()).toBe(true);
    await expect(getPasskeyAssertion({} as never)).resolves.toEqual({ id: "abc" });
    await expect(createPasskey({} as never)).resolves.toEqual({ id: "abc" });
  });

  it("maps a dismissed prompt to a cancelled error", async () => {
    stubWebAuthn(async () => {
      throw new DOMException("no", "NotAllowedError");
    });
    await expect(getPasskeyAssertion({} as never)).rejects.toBeInstanceOf(WebAuthnCancelledError);
  });
});
