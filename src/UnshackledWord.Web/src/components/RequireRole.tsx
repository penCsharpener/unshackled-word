import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Navigate } from "react-router";
import { useAuth } from "@/lib/auth/AuthContext";

type RequireRoleProps = { requires: string; children: ReactNode };

/** Route guard: nothing while loading, sign-in redirect for anonymous users, a 403 note without the role, else the children. */
export function RequireRole({ requires, children }: RequireRoleProps) {
  const { t } = useTranslation();
  const { status, user } = useAuth();

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  if (!user?.roles?.some((r) => r.toLowerCase() === requires.toLowerCase())) {
    return (
      <p role="alert" className="text-fg-muted">
        {t("admin.forbidden")}
      </p>
    );
  }
  return <>{children}</>;
}
