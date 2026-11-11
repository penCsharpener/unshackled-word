import { useTranslation } from "react-i18next";
import { Navigate } from "react-router";
import { AuthCard } from "@/components/AuthCard";
import { PasskeyManager } from "@/components/PasskeyManager";
import { useAuth } from "@/lib/auth/AuthContext";

/** Passkey management; the only guarded route (the reader stays public). */
export function PasskeysPage() {
  const { t } = useTranslation();
  const { status } = useAuth();

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;

  return (
    <AuthCard title={t("auth.passkeys.title")}>
      <PasskeyManager />
    </AuthCard>
  );
}
