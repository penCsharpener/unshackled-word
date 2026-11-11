import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { type PasskeySummary, passkeysApi } from "@/lib/auth/api";
import { createPasskey, isPasskeySupported, WebAuthnCancelledError } from "@/lib/auth/webauthn";
import { Button } from "./Button";
import { FormError } from "./FormError";
import { TextField } from "./TextField";

/** Lists, adds and deletes the signed-in user's passkeys (WebAuthn). */
export function PasskeyManager() {
  const { t, i18n } = useTranslation();
  const [passkeys, setPasskeys] = useState<PasskeySummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [name, setName] = useState("");
  const [busy, setBusy] = useState(false);
  const supported = isPasskeySupported();

  const load = useCallback(async () => {
    try {
      setPasskeys((await passkeysApi.list()).passkeys);
    } catch {
      setError(t("auth.passkeys.loadFailed"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const date = (value: string) => new Date(value).toLocaleDateString(i18n.language);

  async function add() {
    setError("");
    setBusy(true);
    try {
      const credential = await createPasskey(await passkeysApi.getRegisterOptions());
      await passkeysApi.verifyRegistration(
        credential,
        name.trim() || t("auth.passkeys.defaultName"),
      );
      setName("");
      await load();
    } catch (err) {
      setError(
        err instanceof WebAuthnCancelledError
          ? t("auth.passkeys.cancelled")
          : t("auth.passkeys.addFailed"),
      );
    } finally {
      setBusy(false);
    }
  }

  async function remove(id: number) {
    setError("");
    try {
      await passkeysApi.remove(id);
      setPasskeys((list) => list.filter((p) => p.id !== id));
    } catch {
      setError(t("auth.passkeys.removeFailed"));
    }
  }

  if (!supported) return <p className="text-sm text-fg-muted">{t("auth.passkeys.unsupported")}</p>;

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm text-fg-muted">{t("auth.passkeys.intro")}</p>
      <FormError message={error} />
      {loading ? (
        <p className="text-sm text-fg-muted">{t("auth.loading")}</p>
      ) : passkeys.length === 0 ? (
        <p className="text-sm text-fg-muted">{t("auth.passkeys.empty")}</p>
      ) : (
        <ul className="flex flex-col divide-y divide-border rounded-md border border-border">
          {passkeys.map((passkey) => (
            <li key={passkey.id} className="flex items-center justify-between gap-4 p-3">
              <div className="min-w-0">
                <p className="truncate font-medium">{passkey.name}</p>
                <p className="text-sm text-fg-muted">
                  {t("auth.passkeys.added", { date: date(passkey.createdAt) })}
                  {passkey.lastUsedAt &&
                    ` · ${t("auth.passkeys.lastUsed", { date: date(passkey.lastUsedAt) })}`}
                </p>
              </div>
              <Button size="sm" onClick={() => remove(passkey.id)}>
                {t("auth.passkeys.remove")}
              </Button>
            </li>
          ))}
        </ul>
      )}
      <form
        className="flex flex-col gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          void add();
        }}
      >
        <TextField
          label={t("auth.passkeys.name")}
          value={name}
          required={false}
          onChange={setName}
          placeholder={t("auth.passkeys.defaultName")}
        />
        <Button type="submit" variant="primary" disabled={busy}>
          {busy ? t("auth.passkeys.adding") : t("auth.passkeys.add")}
        </Button>
      </form>
    </div>
  );
}
