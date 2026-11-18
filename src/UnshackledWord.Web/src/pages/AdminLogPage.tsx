import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useSearchParams } from "react-router";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { RequireRole } from "@/components/RequireRole";
import { Select, type SelectOption } from "@/components/Select";
import {
  ADMIN_ACTIONS,
  ADMIN_LOG_PAGE_SIZE,
  type AdminLogItem,
  type AdminLogResponse,
  adminLogApi,
  detailsSummary,
  parseUserId,
} from "@/lib/admin/log";
import { adminUsersApi, type UserListItem } from "@/lib/admin/users";

/** Users offered in the filter; the API's maximum page size. */
const USER_FILTER_SIZE = 200;

/** Admin-Protokoll: read-only list of admin actions with filters (`?userId=`, `?action=`) and paging. */
export function AdminLogPage() {
  const { t } = useTranslation();
  return (
    <section className="mx-auto flex w-full max-w-6xl flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold">{t("admin.log.title")}</h1>
      <RequireRole requires="admin">
        <LogView />
      </RequireRole>
    </section>
  );
}

function LogView() {
  const { t, i18n } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();
  const userId = parseUserId(searchParams.get("userId"));
  const actionParam = searchParams.get("action") ?? "";
  const action = (ADMIN_ACTIONS as readonly string[]).includes(actionParam) ? actionParam : "";
  const [page, setPage] = useState(1);
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [data, setData] = useState<AdminLogResponse | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    // The filter only offers names; if this fails the user filter still works with the ID from the URL.
    adminUsersApi
      .list(
        { search: "", role: "", status: "", page: 1, pageSize: USER_FILTER_SIZE },
        controller.signal,
      )
      .then((response) => setUsers(response.items))
      .catch(() => setUsers([]));
    return () => controller.abort();
  }, []);

  // biome-ignore lint/correctness/useExhaustiveDependencies: t is stable per language; refetching on it is pointless
  useEffect(() => {
    const controller = new AbortController();
    adminLogApi
      .list({ userId, action, page }, controller.signal)
      .then((response) => {
        setData(response);
        setError("");
      })
      .catch(() => {
        if (!controller.signal.aborted) setError(t("admin.log.loadFailed"));
      });
    return () => controller.abort();
  }, [userId, action, page]);

  const setFilter = (key: "userId" | "action", value: string) => {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(key, value);
    else next.delete(key);
    setSearchParams(next);
    setPage(1);
  };

  const userOptions: SelectOption[] = [
    { value: "", label: t("admin.log.all") },
    ...users
      .filter((user) => user.status !== "ai")
      .map((user) => ({ value: String(user.id), label: user.displayName })),
  ];
  if (userId !== undefined && !userOptions.some((option) => option.value === String(userId))) {
    userOptions.push({ value: String(userId), label: `#${userId}` });
  }
  const actionOptions: SelectOption[] = [
    { value: "", label: t("admin.log.all") },
    ...ADMIN_ACTIONS.map((name) => ({ value: name, label: t(`admin.log.actions.${name}`) })),
  ];

  const pages = data
    ? Math.max(1, Math.ceil(data.total / (data.pageSize || ADMIN_LOG_PAGE_SIZE)))
    : 1;

  return (
    <>
      <div className="flex flex-wrap items-end gap-3">
        <Select
          label={t("admin.log.userFilter")}
          value={userId === undefined ? "" : String(userId)}
          onChange={(value) => setFilter("userId", value)}
          options={userOptions}
        />
        <Select
          label={t("admin.log.actionFilter")}
          value={action}
          onChange={(value) => setFilter("action", value)}
          options={actionOptions}
        />
      </div>
      <p className="text-sm text-fg-muted">{t("admin.log.readOnly")}</p>
      <FormError message={error} />
      {data && data.items.length === 0 && (
        <EmptyState title={t("admin.log.empty")}>{null}</EmptyState>
      )}
      {data && data.items.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-fg-muted">
              <tr className="border-b border-border">
                <th className="py-2 pr-4 font-medium">{t("admin.log.time")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.log.actor")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.log.action")}</th>
                <th className="py-2 pr-4 font-medium">{t("admin.log.target")}</th>
                <th className="py-2 font-medium">{t("admin.log.details")}</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((item) => (
                <tr key={item.id} className="border-b border-border">
                  <td className="whitespace-nowrap py-2 pr-4">
                    {new Date(item.createdAt).toLocaleString(i18n.language)}
                  </td>
                  <td className="py-2 pr-4">{item.actorDisplayName}</td>
                  <td className="py-2 pr-4">
                    {t(`admin.log.actions.${item.action}`, { defaultValue: item.action })}
                  </td>
                  <td className="py-2 pr-4">
                    <TargetCell item={item} />
                  </td>
                  <td className="py-2">{detailsSummary(item.details) || "–"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {data && data.total > 0 && (
        <nav className="flex flex-wrap items-center gap-3" aria-label={t("admin.log.title")}>
          <span className="text-sm text-fg-muted">
            {t("admin.log.total", { count: data.total })}
          </span>
          <Button
            variant="secondary"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            {t("admin.log.prev")}
          </Button>
          <span className="text-sm">{t("admin.log.page", { page, pages })}</span>
          <Button
            variant="secondary"
            size="sm"
            disabled={page >= pages}
            onClick={() => setPage((p) => p + 1)}
          >
            {t("admin.log.next")}
          </Button>
        </nav>
      )}
    </>
  );
}

/** The affected user (linked to its admin page), an invite code, or a dash. */
function TargetCell({ item }: { item: AdminLogItem }) {
  const { t } = useTranslation();
  if (item.targetUserId) {
    return (
      <Link
        to={`/admin/users/${item.targetUserId}`}
        className="font-medium text-accent hover:underline"
      >
        {item.targetDisplayName ?? `#${item.targetUserId}`}
      </Link>
    );
  }
  if (item.inviteCodeId) return <>{t("admin.log.inviteCode", { id: item.inviteCodeId })}</>;
  return <span className="text-fg-muted">–</span>;
}
