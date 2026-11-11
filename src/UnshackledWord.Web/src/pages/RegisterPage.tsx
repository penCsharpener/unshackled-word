import { type FormEvent, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useSearchParams } from "react-router";
import { AuthCard } from "@/components/AuthCard";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { ApiError, describeApiError } from "@/lib/api";
import { authApi } from "@/lib/auth/api";
import { authLinkClasses } from "@/lib/auth/styles";

export function RegisterPage() {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [inviteCode, setInviteCode] = useState(params.get("invite") ?? "");
  const [inviteCodeRequired, setInviteCodeRequired] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);

  // The invite field only shows when /auth/registration-policy says this deployment may require one.
  useEffect(() => {
    authApi.registrationPolicy().then(
      (policy) => setInviteCodeRequired(policy.inviteCodeRequired),
      () => {}, // a failed check never blocks registering; the API enforces the gate anyway
    );
  }, []);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      await authApi.register({
        email,
        password,
        displayName,
        inviteCode: inviteCode.trim() || undefined,
      });
      setDone(true);
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 409
          ? t("auth.register.exists")
          : describeApiError(err, t("auth.register.fallback")),
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthCard title={t("auth.createAccount")}>
      {done ? (
        <>
          <p className="text-sm">{t("auth.register.done", { email })}</p>
          <Link to="/login" className={authLinkClasses}>
            {t("auth.backToSignIn")}
          </Link>
        </>
      ) : (
        <>
          <form className="flex flex-col gap-4" onSubmit={onSubmit}>
            <TextField
              label={t("auth.register.displayName")}
              autoComplete="name"
              value={displayName}
              onChange={setDisplayName}
            />
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
              autoComplete="new-password"
              value={password}
              onChange={setPassword}
            />
            {inviteCodeRequired && (
              <TextField
                label={t("auth.register.inviteCode")}
                required={false}
                value={inviteCode}
                onChange={setInviteCode}
              />
            )}
            <FormError message={error} />
            <Button type="submit" variant="primary" disabled={submitting}>
              {submitting ? t("auth.register.submitting") : t("auth.createAccount")}
            </Button>
          </form>
          <p className="text-sm text-fg-muted">
            {t("auth.register.haveAccount")}{" "}
            <Link to="/login" className={authLinkClasses}>
              {t("auth.signIn")}
            </Link>
          </p>
        </>
      )}
    </AuthCard>
  );
}
