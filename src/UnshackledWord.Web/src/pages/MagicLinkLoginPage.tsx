import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { AuthCard } from "@/components/AuthCard";
import { FormError } from "@/components/FormError";
import { useAuth } from "@/lib/auth/AuthContext";
import { authLinkClasses } from "@/lib/auth/styles";
import { useTokenAction } from "@/lib/auth/useTokenAction";

/** Landing page of the emailed magic link: consumes `?token=` and signs the user in. */
export function MagicLinkLoginPage() {
  const { t } = useTranslation();
  const { loginWithMagicLink } = useAuth();
  const navigate = useNavigate();
  const status = useTokenAction(loginWithMagicLink);

  useEffect(() => {
    if (status === "success") navigate("/", { replace: true });
  }, [status, navigate]);

  return (
    <AuthCard title={t("auth.magicLogin.title")}>
      {status === "failure" ? (
        <>
          <FormError message={t("auth.magicLogin.failure")} />
          <Link to="/magic-link" className={authLinkClasses}>
            {t("auth.magicLogin.requestNew")}
          </Link>
        </>
      ) : (
        <p className="text-sm text-fg-muted">{t("auth.magicLogin.loading")}</p>
      )}
    </AuthCard>
  );
}
