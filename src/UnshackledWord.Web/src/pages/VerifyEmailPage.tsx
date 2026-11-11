import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { AuthCard } from "@/components/AuthCard";
import { FormError } from "@/components/FormError";
import { authApi } from "@/lib/auth/api";
import { authLinkClasses } from "@/lib/auth/styles";
import { useTokenAction } from "@/lib/auth/useTokenAction";

/** Landing page of the verification email: confirms `?token=`. */
export function VerifyEmailPage() {
  const { t } = useTranslation();
  const status = useTokenAction(authApi.verifyEmail);

  return (
    <AuthCard title={t("auth.verify.title")}>
      {status === "running" && (
        <p className="text-sm text-fg-muted">{t("auth.verify.verifying")}</p>
      )}
      {status === "success" && (
        <>
          <p className="text-sm">{t("auth.verify.success")}</p>
          <Link to="/login" className={authLinkClasses}>
            {t("auth.signIn")}
          </Link>
        </>
      )}
      {status === "failure" && <FormError message={t("auth.verify.failure")} />}
    </AuthCard>
  );
}
