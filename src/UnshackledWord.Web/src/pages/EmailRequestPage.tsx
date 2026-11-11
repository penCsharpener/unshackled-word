import { type FormEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { AuthCard } from "@/components/AuthCard";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { describeApiError } from "@/lib/api";
import { authApi } from "@/lib/auth/api";
import { authLinkClasses } from "@/lib/auth/styles";

type Kind = "reset" | "magic";

/** Email-only request form shared by "forgot password" and "sign in with a link". */
function EmailRequestPage({ kind }: { kind: Kind }) {
  const { t } = useTranslation();
  const [email, setEmail] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [sent, setSent] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      // Same response whether or not the address exists (no account enumeration).
      await (kind === "reset"
        ? authApi.requestPasswordReset(email)
        : authApi.requestMagicLink(email));
      setSent(true);
    } catch (err) {
      setError(describeApiError(err, t(`auth.${kind}.fallback`)));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthCard title={t(`auth.${kind}.title`)}>
      {sent ? (
        <p className="text-sm">{t(`auth.${kind}.sent`, { email })}</p>
      ) : (
        <form className="flex flex-col gap-4" onSubmit={onSubmit}>
          {kind === "reset" && <p className="text-sm text-fg-muted">{t("auth.reset.intro")}</p>}
          <TextField
            label={t("auth.email")}
            type="email"
            autoComplete="email"
            value={email}
            onChange={setEmail}
          />
          <FormError message={error} />
          <Button type="submit" variant="primary" disabled={submitting}>
            {submitting ? t("auth.sending") : t(`auth.${kind}.submit`)}
          </Button>
        </form>
      )}
      <Link to="/login" className={authLinkClasses}>
        {t("auth.backToSignIn")}
      </Link>
    </AuthCard>
  );
}

export const ForgotPasswordPage = () => <EmailRequestPage kind="reset" />;
export const MagicLinkRequestPage = () => <EmailRequestPage kind="magic" />;
