import { type FormEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { AuthCard } from "@/components/AuthCard";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { passkeysApi } from "@/lib/auth/api";
import { authLinkClasses } from "@/lib/auth/styles";
import {
  getPasskeyAssertion,
  isPasskeySupported,
  WebAuthnCancelledError,
} from "@/lib/auth/webauthn";

export function SignInPage() {
  const { t } = useTranslation();
  const { login, refresh } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [passkeyBusy, setPasskeyBusy] = useState(false);
  const [passkeyError, setPasskeyError] = useState("");

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      await login(email, password);
      navigate("/");
    } catch (err) {
      // Deliberately generic: the API never says which part was wrong.
      setError(
        err instanceof ApiError && err.status === 401
          ? t("auth.invalidCredentials")
          : t("auth.loginFailed"),
      );
    } finally {
      setSubmitting(false);
    }
  }

  async function signInWithPasskey() {
    setPasskeyError("");
    setPasskeyBusy(true);
    try {
      const { flowId, options } = await passkeysApi.getLoginOptions();
      const credential = await getPasskeyAssertion(options);
      await passkeysApi.login(flowId, credential);
      await refresh();
      navigate("/");
    } catch (err) {
      setPasskeyError(
        err instanceof WebAuthnCancelledError
          ? t("auth.passkeys.cancelled")
          : t("auth.passkeyFailed"),
      );
    } finally {
      setPasskeyBusy(false);
    }
  }

  return (
    <AuthCard title={t("auth.signIn")}>
      <form className="flex flex-col gap-4" onSubmit={onSubmit}>
        <TextField
          label={t("auth.email")}
          type="email"
          autoComplete="email"
          value={email}
          onChange={setEmail}
        />
        <TextField
          label={t("auth.password")}
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={setPassword}
        />
        <FormError message={error} />
        <Button type="submit" variant="primary" disabled={submitting}>
          {submitting ? t("auth.signingIn") : t("auth.signIn")}
        </Button>
      </form>
      {isPasskeySupported() && (
        <div className="flex flex-col gap-2">
          <Button disabled={passkeyBusy} onClick={signInWithPasskey}>
            {passkeyBusy ? t("auth.passkeyWaiting") : t("auth.passkey")}
          </Button>
          <FormError message={passkeyError} />
        </div>
      )}
      <div className="flex flex-wrap justify-between gap-2 text-sm">
        <Link to="/register" className={authLinkClasses}>
          {t("auth.createAccount")}
        </Link>
        <Link to="/forgot-password" className={authLinkClasses}>
          {t("auth.forgotPassword")}
        </Link>
        <Link to="/magic-link" className={authLinkClasses}>
          {t("auth.magicLink")}
        </Link>
      </div>
    </AuthCard>
  );
}
