import { useTranslation } from "react-i18next";
import { InviteCodeManager } from "@/components/InviteCodeManager";
import { RequireRole } from "@/components/RequireRole";

/** Admin-only invitation code management; anonymous users go to sign-in, others see a 403 note. */
export function AdminInvitationsPage() {
  const { t } = useTranslation();

  return (
    <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold">{t("admin.invitations.title")}</h1>
      <RequireRole requires="admin">
        <InviteCodeManager />
      </RequireRole>
    </section>
  );
}
