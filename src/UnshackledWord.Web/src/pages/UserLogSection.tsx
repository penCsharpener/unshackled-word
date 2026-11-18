import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { FormError } from "@/components/FormError";
import { type AdminLogResponse, adminLogApi, detailsSummary } from "@/lib/admin/log";

/** Entries shown in the section; the full log is one click away. */
const SECTION_SIZE = 20;

interface UserLogSectionProps {
  userId: number;
  /** Changes after each save so the section shows the entry the save just wrote. */
  refreshKey: unknown;
}

/** Protokoll section of the admin user page: the last admin-log entries for this user. */
export function UserLogSection({ userId, refreshKey }: UserLogSectionProps) {
  const { t, i18n } = useTranslation();
  const [data, setData] = useState<AdminLogResponse | null>(null);
  const [error, setError] = useState("");

  // biome-ignore lint/correctness/useExhaustiveDependencies: t is stable per language; refreshKey re-runs the fetch after a save
  useEffect(() => {
    const controller = new AbortController();
    adminLogApi
      .list({ userId, page: 1, pageSize: SECTION_SIZE }, controller.signal)
      .then((response) => {
        setData(response);
        setError("");
      })
      .catch(() => {
        if (!controller.signal.aborted) setError(t("admin.log.loadFailed"));
      });
    return () => controller.abort();
  }, [userId, refreshKey]);

  return (
    <section className="flex flex-col gap-3" aria-labelledby="user-log-heading">
      <h2 id="user-log-heading" className="text-lg font-semibold">
        {t("admin.log.section.title")}
      </h2>
      <FormError message={error} />
      {data && data.items.length === 0 && (
        <p className="text-fg-muted">{t("admin.log.section.empty")}</p>
      )}
      {data && data.items.length > 0 && (
        <>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="text-fg-muted">
                <tr className="border-b border-border">
                  <th className="py-2 pr-4 font-medium">{t("admin.log.time")}</th>
                  <th className="py-2 pr-4 font-medium">{t("admin.log.action")}</th>
                  <th className="py-2 pr-4 font-medium">{t("admin.log.actor")}</th>
                  <th className="py-2 font-medium">{t("admin.log.details")}</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((item) => (
                  <tr key={item.id} className="border-b border-border">
                    <td className="whitespace-nowrap py-2 pr-4">
                      {new Date(item.createdAt).toLocaleString(i18n.language)}
                    </td>
                    <td className="py-2 pr-4">
                      {t(`admin.log.actions.${item.action}`, { defaultValue: item.action })}
                    </td>
                    <td className="py-2 pr-4">{item.actorDisplayName}</td>
                    <td className="py-2">{detailsSummary(item.details) || "–"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Link
            to={`/admin/log?userId=${userId}`}
            className="text-sm font-medium text-accent hover:underline"
          >
            {t("admin.log.section.showAll")}
          </Link>
        </>
      )}
    </section>
  );
}
