import { useState } from "react";
import { useTranslation } from "react-i18next";
import { FindingsTable } from "@/components/FindingsTable";
import { FormError } from "@/components/FormError";
import { StatTile } from "@/components/StatTile";
import { useBibleStructure } from "@/lib/bible/queries";
import { formatNumber } from "@/lib/chart";
import {
  REVISION_WORK_LISTS,
  type RevisionWorkListName,
  revisionVerseHref,
  toFinding,
  useRevisionOverview,
} from "@/lib/revision/overview";

const DEFAULT_PAGE_SIZE = 50;

/** "Revision" section of the maintenance page: revised verses per level and the three lists of work to do. */
export function RevisionOverviewSection() {
  const { t, i18n } = useTranslation();
  const books = useBibleStructure().data ?? [];
  const [list, setList] = useState<RevisionWorkListName>("unreviewed");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const overview = useRevisionOverview({ list, page, pageSize }, true);
  const data = overview.data;
  const levelName = (level: number) =>
    data?.levels.find((l) => l.level === level)?.name ?? String(level);
  const number = (value: number) => formatNumber(value, i18n.language);

  const findings =
    data?.list?.items.map((item) =>
      toFinding(
        list,
        item,
        t(`maintainer.revision.detail.${list}`, {
          level: levelName(item.level),
          version: item.version,
          german: item.unlinkedGermanWords,
          original: item.unlinkedOriginalWords,
        }),
      ),
    ) ?? [];

  return (
    <div className="@container flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t("maintainer.revision.title")}</h2>
      <p className="text-fg-muted">{t("maintainer.revision.intro")}</p>
      {overview.isError && !data ? (
        <FormError message={t("maintainer.revision.failed")} />
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 @2xl:grid-cols-3">
            {data?.levels.length === 0 ? (
              <p className="col-span-full text-fg-muted">{t("maintainer.revision.noLevels")}</p>
            ) : null}
            {(data?.levels ?? [undefined]).map((level) => (
              <StatTile
                key={level?.level ?? "loading"}
                loading={!level}
                title={level?.name ?? ""}
                titleText={level?.name}
                value={
                  level ? `${number(level.revisedVerses)} / ${number(level.totalVerses)}` : null
                }
                sub={level ? t("maintainer.revision.revisedVerses") : undefined}
              />
            ))}
          </div>
          <h3 className="text-sm font-medium text-fg-muted">
            {t("maintainer.revision.worklists")}
          </h3>
          <div className="grid grid-cols-1 gap-3 @2xl:grid-cols-3">
            {REVISION_WORK_LISTS.map((name) => {
              const count = data?.counts[name];
              return (
                <StatTile
                  key={name}
                  loading={count === undefined}
                  title={t(`maintainer.revision.lists.${name}`)}
                  value={count === undefined ? null : number(count)}
                  severity={count === 0 ? "ok" : name === "unreviewed" ? "info" : "warning"}
                  severityLabel={t(`maintainer.revision.severity.${count === 0 ? "ok" : name}`)}
                  selected={list === name}
                  onSelect={() => {
                    setList(name);
                    setPage(1);
                  }}
                />
              );
            })}
          </div>
          <FindingsTable
            findings={findings}
            total={data?.list?.total ?? 0}
            page={page}
            pageSize={pageSize}
            onPage={setPage}
            onPageSize={(size) => {
              setPageSize(size);
              setPage(1);
            }}
            columns={["ref", "detail", "severity"]}
            books={books}
            loading={overview.isFetching}
            error={overview.isError}
            onRetry={() => overview.refetch()}
            emptyText={t(`maintainer.revision.empty.${list}`)}
            jumps={(finding) => {
              const item = data?.list?.items[findings.indexOf(finding)];
              return item
                ? [
                    {
                      key: "open",
                      label: t("maintainer.revision.open"),
                      href: revisionVerseHref(item),
                    },
                  ]
                : [];
            }}
          />
        </>
      )}
    </div>
  );
}
