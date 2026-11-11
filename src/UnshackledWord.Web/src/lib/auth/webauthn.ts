/**
 * The only module touching `navigator.credentials`. Uses the WebAuthn Level 3 JSON helpers
 * (`PublicKeyCredential.parse*OptionsFromJSON`, `credential.toJSON()`), so the server's JSON options and
 * verify payloads pass straight through without a hand-rolled base64url codec.
 */

export type PasskeyCredentialJson = RegistrationResponseJSON | AuthenticationResponseJSON;

export class WebAuthnUnsupportedError extends Error {
  constructor() {
    super("This browser does not support passkeys.");
    this.name = "WebAuthnUnsupportedError";
  }
}

/** The user closed the prompt, had no matching passkey or it timed out. */
export class WebAuthnCancelledError extends Error {
  constructor() {
    super("Passkey step was cancelled or no matching passkey was found.");
    this.name = "WebAuthnCancelledError";
  }
}

export function isPasskeySupported(): boolean {
  return (
    typeof window !== "undefined" &&
    !!window.PublicKeyCredential?.parseCreationOptionsFromJSON &&
    !!window.PublicKeyCredential?.parseRequestOptionsFromJSON
  );
}

async function runCeremony<T>(ceremony: () => Promise<T>): Promise<T> {
  if (!isPasskeySupported()) throw new WebAuthnUnsupportedError();
  try {
    return await ceremony();
  } catch (error) {
    if (error instanceof DOMException && ["NotAllowedError", "AbortError"].includes(error.name)) {
      throw new WebAuthnCancelledError();
    }
    throw error;
  }
}

/** Browser half of "register a passkey"; the result is POSTed back for verification. */
export async function createPasskey(
  options: PublicKeyCredentialCreationOptionsJSON,
): Promise<PasskeyCredentialJson> {
  const credential = await runCeremony(() =>
    navigator.credentials.create({
      publicKey: PublicKeyCredential.parseCreationOptionsFromJSON(options),
    }),
  );
  if (!(credential instanceof PublicKeyCredential)) throw new WebAuthnCancelledError();
  return credential.toJSON();
}

/** Browser half of "sign in with a passkey"; the result is POSTed back for verification. */
export async function getPasskeyAssertion(
  options: PublicKeyCredentialRequestOptionsJSON,
): Promise<PasskeyCredentialJson> {
  const credential = await runCeremony(() =>
    navigator.credentials.get({
      publicKey: PublicKeyCredential.parseRequestOptionsFromJSON(options),
    }),
  );
  if (!(credential instanceof PublicKeyCredential)) throw new WebAuthnCancelledError();
  return credential.toJSON();
}
