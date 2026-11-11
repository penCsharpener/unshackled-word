import { type FormEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useSearchParams } from "react-router";
import { AuthCard } from "@/components/AuthCard";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { ApiError, describeApiError } from "@/lib/api";
import { authApi } from "@/lib/auth/api";
import { authLinkClasses } from "@/lib/auth/styles";

export function ResetPasswordPage() {
  const { t } = useTranslation();
  const token = useSearchParams()[0].get("token");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (password !== confirm) {
      setError(t("auth.resetPassword.mismatch"));
      return;
    }
    setSubmitting(true);
    try {
      await authApi.resetPassword(token ?? "", password);
      setDone(true);
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 404
          ? t("auth.resetPassword.expired")
          : describeApiError(err, t("auth.resetPassword.fallback")),
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthCard title={t("auth.resetPassword.title")}>
      {!token ? (
        <FormError message={t("auth.resetPassword.invalidLink")} />
      ) : done ? (
        <>
          <p className="text-sm">{t("auth.resetPassword.done")}</p>
          <Link to="/login" className={authLinkClasses}>
            {t("auth.signIn")}
          </Link>
        </>
      ) : (
        <form className="flex flex-col gap-4" onSubmit={onSubmit}>
          <TextField
            label={t("auth.resetPassword.newPassword")}
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={setPassword}
          />
          <TextField
            label={t("auth.resetPassword.confirmPassword")}
            type="password"
            autoComplete="new-password"
            value={confirm}
            onChange={setConfirm}
          />
          <FormError message={error} />
          <Button type="submit" variant="primary" disabled={submitting}>
            {submitting ? t("auth.sending") : t("auth.resetPassword.submit")}
          </Button>
        </form>
      )}
    </AuthCard>
  );
}
