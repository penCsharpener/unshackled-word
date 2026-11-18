import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { FormError } from "@/components/FormError";
import { RequireRole } from "@/components/RequireRole";
import { StatusBadge } from "@/components/StatusBadge";
import { adminUsersApi, type UserDetail } from "@/lib/admin/users";
import { STATUS_TONES } from "./AdminUsersPage";
import { UserDetailsSection } from "./UserDetailsSection";
import { UserLogSection } from "./UserLogSection";
import { UserRolesSection } from "./UserRolesSection";
import { UserStatusSection } from "./UserStatusSection";

/** Admin page of one user; AI users open read-only. */
export function AdminUserDetailPage() {
  const { t } = useTranslation();
  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-4 p-4">
      <Link to="/admin/users" className="text-sm text-accent hover:underline">
        {t("admin.users.back")}
      </Link>
      <RequireRole requires="admin">
        <UserDetailView />
      </RequireRole>
    </section>
  );
}

function UserDetailView() {
  const { t, i18n } = useTranslation();
  const { id } = useParams();
  const [user, setUser] = useState<UserDetail | null>(null);
  const [error, setError] = useState("");

  // biome-ignore lint/correctness/useExhaustiveDependencies: t is stable per language
  useEffect(() => {
    const controller = new AbortController();
    adminUsersApi
      .get(Number(id), controller.signal)
      .then((detail) => {
        setUser(detail);
        setError("");
      })
      .catch(() => {
        if (!controller.signal.aborted) setError(t("admin.users.loadOneFailed"));
      });
    return () => controller.abort();
  }, [id]);

  const readOnly = user?.status === "ai";
  return (
    <>
      <FormError message={error} />
      {user && (
        <>
          <header className="flex flex-col gap-1">
            <h1 className="text-2xl font-semibold">{user.displayName}</h1>
            <p className="flex flex-wrap items-center gap-2 text-fg-muted">
              <span>{user.email}</span>
              <StatusBadge tone={STATUS_TONES[user.status]}>
                {t(`admin.users.statusLabel.${user.status}`)}
              </StatusBadge>
            </p>
            <p className="text-sm text-fg-muted">
              {t("admin.users.created")}:{" "}
              {new Date(user.createdAt).toLocaleDateString(i18n.language)}
            </p>
          </header>
          {readOnly ? (
            <p role="note" className="text-fg-muted">
              {t("admin.users.aiReadOnly")}
            </p>
          ) : (
            <p role="note" className="text-sm text-fg-muted">
              {t("admin.users.signOutNotice")}
            </p>
          )}
          <UserDetailsSection user={user} readOnly={readOnly} onSaved={setUser} />
          <UserRolesSection user={user} readOnly={readOnly} onSaved={setUser} />
          <UserStatusSection user={user} readOnly={readOnly} onSaved={setUser} />
          <UserLogSection userId={user.id} refreshKey={user} />
        </>
      )}
    </>
  );
}
