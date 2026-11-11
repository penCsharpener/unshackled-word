import { useTranslation } from "react-i18next";
import { Navigate } from "react-router";
import { InviteCodeManager } from "@/components/InviteCodeManager";
import { useAuth } from "@/lib/auth/AuthContext";

/** Admin-only invitation code management; anonymous users go to sign-in, others see a 403 note. */
export function AdminInvitationsPage() {
  const { t } = useTranslation();
  const { status, user } = useAuth();

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;

  return (
    <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold">{t("admin.invitations.title")}</h1>
      {user?.roles?.includes("admin") ? (
        <InviteCodeManager />
      ) : (
        <p role="alert" className="text-fg-muted">
          {t("admin.forbidden")}
        </p>
      )}
    </section>
  );
}
