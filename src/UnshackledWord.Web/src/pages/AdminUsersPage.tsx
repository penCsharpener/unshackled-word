import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { Button, buttonClasses } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { RequireRole } from "@/components/RequireRole";
import { Select } from "@/components/Select";
import { StatusBadge, type StatusTone } from "@/components/StatusBadge";
import { TextField } from "@/components/TextField";
import {
  ASSIGNABLE_ROLES,
  adminUsersApi,
  USER_PAGE_SIZE,
  type UserListResponse,
  type UserStatus,
} from "@/lib/admin/users";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

export const STATUS_TONES: Record<UserStatus, StatusTone> = {
  active: "ok",
  deactivated: "warning",
  ai: "info",
};

/** Admin user list: search, role/status filters and paging; a row opens the user's page. */
export function AdminUsersPage() {
  const { t } = useTranslation();
  return (
    <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t("admin.users.title")}</h1>
        <Link to="/admin/users/new" className={buttonClasses({ variant: "primary" })}>
          {t("admin.users.create")}
        </Link>
      </div>
      <RequireRole requires="admin">
        <UserList />
      </RequireRole>
    </section>
  );
}

function UserList() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(search, 300);
  const [data, setData] = useState<UserListResponse | null>(null);
  const [error, setError] = useState("");

  // biome-ignore lint/correctness/useExhaustiveDependencies: t is stable per language; refetching on it is pointless
  useEffect(() => {
    const controller = new AbortController();
    adminUsersApi
      .list({ search: debouncedSearch, role, status, page }, controller.signal)
      .then((response) => {
        setData(response);
        setError("");
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) setError(t("admin.users.loadFailed"));
        void err;
      });
    return () => controller.abort();
  }, [debouncedSearch, role, status, page]);

  const filter = (setter: (value: string) => void) => (value: string) => {
    setter(value);
    setPage(1);
  };
  const pages = data ? Math.max(1, Math.ceil(data.total / (data.pageSize || USER_PAGE_SIZE))) : 1;
  const date = (value?: string | null) =>
    value ? new Date(value).toLocaleDateString(i18n.language) : t("admin.users.never");

  return (
    <>
      <div className="flex flex-wrap items-end gap-3">
        <TextField
          type="search"
          required={false}
          label={t("admin.users.searchLabel")}
          value={search}
          onChange={filter(setSearch)}
        />
        <Select
          label={t("admin.users.roleFilter")}
          value={role}
          onChange={filter(setRole)}
          options={[
            { value: "", label: t("admin.users.all") },
            ...["user", ...ASSIGNABLE_ROLES].map((r) => ({
              value: r,
              label: t(`admin.users.roleNames.${r}`),
            })),
          ]}
        />
        <Select
          label={t("admin.users.statusFilter")}
          value={status}
          onChange={filter(setStatus)}
          options={[
            { value: "", label: t("admin.users.all") },
            ...(["active", "deactivated", "ai"] as const).map((s) => ({
              value: s,
              label: t(`admin.users.statusLabel.${s}`),
            })),
          ]}
        />
      </div>
      <FormError message={error} />
      {data && data.items.length === 0 && (
        <EmptyState title={t("admin.users.empty")}>{null}</EmptyState>
      )}
      {data && data.items.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-fg-muted">
              <tr className="border-b border-border">
                <th className="py-2 pr-4 font-medium">{t("admin.users.name")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.users.email")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.users.roles")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.users.status")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.users.created")}</th>
                <th className="py-2 font-medium">{t("admin.users.lastSignIn")}</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((user) => (
                // biome-ignore lint/a11y/useKeyWithClickEvents: the name cell holds the keyboard-accessible link
                <tr
                  key={user.id}
                  className="cursor-pointer border-b border-border hover:bg-surface"
                  onClick={() => navigate(`/admin/users/${user.id}`)}
                >
                  <td className="py-2 pr-4">
                    <Link
                      to={`/admin/users/${user.id}`}
                      className="font-medium text-accent hover:underline"
                      onClick={(event) => event.stopPropagation()}
                    >
                      {user.displayName}
                    </Link>
                  </td>
                  <td className="py-2 pr-4">{user.email}</td>
                  <td className="py-2 pr-4">
                    <div className="flex flex-wrap gap-1">
                      {user.roles.map((r) => (
                        <StatusBadge key={r} tone="neutral">
                          {t(`admin.users.roleNames.${r.toLowerCase()}`, { defaultValue: r })}
                        </StatusBadge>
                      ))}
                    </div>
                  </td>
                  <td className="py-2 pr-4">
                    <StatusBadge tone={STATUS_TONES[user.status]}>
                      {t(`admin.users.statusLabel.${user.status}`)}
                    </StatusBadge>
                  </td>
                  <td className="py-2 pr-4">{date(user.createdAt)}</td>
                  <td className="py-2">{date(user.lastSignInAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {data && data.total > 0 && (
        <nav className="flex flex-wrap items-center gap-3" aria-label={t("admin.users.title")}>
          <span className="text-sm text-fg-muted">
            {t("admin.users.total", { count: data.total })}
          </span>
          <Button
            variant="secondary"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            {t("admin.users.prev")}
          </Button>
          <span className="text-sm">{t("admin.users.page", { page, pages })}</span>
          <Button
            variant="secondary"
            size="sm"
            disabled={page >= pages}
            onClick={() => setPage((p) => p + 1)}
          >
            {t("admin.users.next")}
          </Button>
        </nav>
      )}
    </>
  );
}
