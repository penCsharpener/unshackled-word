import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button, buttonClasses } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { bookName } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import { formatNumber } from "@/lib/chart";
import { formatStamp } from "@/lib/format";
import { qualityKeys, useQualityOverview, useRefreshQuality } from "@/lib/quality/queries";
import type { TestamentFacet } from "@/lib/quality/types";
import { resetFacetsPatch } from "@/lib/quality/url";
import { useQualityUrl } from "@/lib/quality/useQualityUrl";
import { CoverageSection } from "./quality/CoverageSection";
import { Explorer } from "./quality/Explorer";
import { Findings } from "./quality/Findings";
import { OverviewSection } from "./quality/OverviewSection";
import { UnmappedSection } from "./quality/UnmappedSection";

const TESTAMENTS: (TestamentFacet | "")[] = ["", "ot", "nt"];

export function DataQualityPage() {
  const { t, i18n } = useTranslation();
  const { status, user } = useAuth();
  const authed = status === "authenticated";
  const canRefresh = hasWriteRole(user);
  // The data quality endpoints answer 403 to everyone but Admin/Maintainer: do not even fire them.
  const allowed = authed && canRefresh;
  const { state, update, openFilter, goToCrumb } = useQualityUrl();
  const client = useQueryClient();
  const structure = useBibleStructure();
  const books = structure.data ?? [];
  const facets = { testament: state.testament, bookId: state.bookId };

  const overview = useQualityOverview(facets, allowed);
  const refresh = useRefreshQuality();
  const refreshedAt = overview.data?.refreshedAt ?? null;

  // A new snapshot invalidates every chart, list and search of the page.
  const lastSnapshot = useRef<string | null | undefined>(undefined);
  useEffect(() => {
    if (!overview.data) return;
    if (lastSnapshot.current !== undefined && lastSnapshot.current !== refreshedAt) {
      void client.invalidateQueries({ queryKey: [...qualityKeys.all, "data"] });
    }
    lastSnapshot.current = refreshedAt;
  }, [overview.data, refreshedAt, client]);

  const title = <h1 className="font-scripture text-2xl font-semibold">{t("quality.title")}</h1>;

  if (status === "loading") return <p className="text-fg-muted">{t("auth.loading")}</p>;
  if (!authed) {
    return (
      <div className="flex flex-col gap-4">
        {title}
        <EmptyState title={t("quality.loginRequired.title")}>
          <p className="mb-3">{t("quality.loginRequired.text")}</p>
          <Link to="/login" className={buttonClasses({ variant: "primary" })}>
            {t("auth.signIn")}
          </Link>
        </EmptyState>
      </div>
    );
  }

  if (!canRefresh) {
    return (
      <div className="flex flex-col gap-4">
        {title}
        <p role="alert" className="text-fg-muted">
          {t("quality.noPermission")}
        </p>
      </div>
    );
  }

  const data = overview.data;
  const refreshing = !!data?.refreshing || refresh.isPending;
  const stale =
    !!data?.lastEditAt && !!refreshedAt && new Date(data.lastEditAt) > new Date(refreshedAt);
  const date = refreshedAt && formatStamp(refreshedAt, i18n.language);
  const statsRefreshedAt = data?.statsRefreshedAt ?? null;

  const refreshButton = (primary: boolean, label: string) =>
    canRefresh ? (
      <Button
        variant={primary ? "primary" : "ghost"}
        size={primary ? "md" : "sm"}
        disabled={refreshing}
        title={t("quality.refreshHint")}
        onClick={() => refresh.mutate()}
      >
        {refreshing ? t("quality.refreshing") : label}
      </Button>
    ) : refreshing ? (
      <span className="text-sm text-fg-muted">{t("quality.refreshing")}</span>
    ) : null;

  const facetBooks = books.filter((book) => !state.testament || book.testament === state.testament);
  const hasFacets = state.testament !== undefined || state.bookId !== undefined;

  return (
    <div className="@container flex flex-col gap-8 pb-12">
      <header className="grid gap-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          {title}
          {data && refreshedAt ? refreshButton(false, t("quality.refresh")) : null}
        </div>
        {data && refreshedAt ? (
          <p className="text-sm text-fg-muted">
            {t("quality.stand", { date })} ·{" "}
            {statsRefreshedAt
              ? t("quality.statsStand", { date: formatStamp(statsRefreshedAt, i18n.language) })
              : t("quality.statsNone")}{" "}
            ·{" "}
            {t("quality.totals", {
              germanWords: formatNumber(data.totals.germanWords, i18n.language),
              sourceWords: formatNumber(data.totals.sourceWords, i18n.language),
            })}
          </p>
        ) : null}
        {data?.seedImports?.length ? (
          <ul className="grid gap-1 text-sm text-fg-muted">
            {data.seedImports.map((seed) => (
              <li
                key={`${seed.kind}-${seed.seedVersion}-${seed.importedAt}`}
                title={seed.reportPath}
              >
                {seed.versesUpdated == null
                  ? t("quality.seed.import", {
                      kind: t(`quality.seed.kinds.${seed.kind}`),
                      exported: seed.exportedAt ? formatStamp(seed.exportedAt, i18n.language) : "?",
                      imported: formatStamp(seed.importedAt, i18n.language),
                      count: seed.orphaned,
                    })
                  : t(
                      seed.kind === "revision"
                        ? "quality.seed.updateRevision"
                        : "quality.seed.update",
                      {
                        updated: seed.versesUpdated,
                        kept: seed.versesKeptLocal ?? 0,
                        report: seed.reportPath,
                      },
                    )}
              </li>
            ))}
          </ul>
        ) : null}
        {refresh.isError ? <FormError message={t("quality.refreshFailed")} /> : null}
        {stale ? (
          <p role="status" className="rounded-md bg-surface-muted px-3 py-2 text-sm text-fg-muted">
            {t("quality.states.stale")}
          </p>
        ) : null}
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex gap-1">
            {TESTAMENTS.map((value) => (
              <Button
                key={value || "all"}
                size="sm"
                aria-pressed={(state.testament ?? "") === value}
                onClick={() => update({ testament: value || undefined, bookId: undefined })}
              >
                {t(`quality.facets.${value || "all"}`)}
              </Button>
            ))}
          </div>
          <Select
            label={t("quality.facets.book")}
            value={state.bookId ? String(state.bookId) : ""}
            onChange={(value) => update({ bookId: value ? Number(value) : undefined })}
            options={[
              { value: "", label: t("quality.facets.anyBook") },
              ...facetBooks.map((book) => ({
                value: String(book.id),
                label: bookName(book, i18n.language),
              })),
            ]}
          />
          {hasFacets ? (
            <Button variant="ghost" size="sm" onClick={() => update(resetFacetsPatch())}>
              {t("quality.facets.reset")}
            </Button>
          ) : null}
        </div>
      </header>

      {overview.isError ? (
        <div className="grid gap-2">
          <FormError message={t("quality.states.error")} />
          <Button size="sm" className="justify-self-start" onClick={() => void overview.refetch()}>
            {t("quality.states.retry")}
          </Button>
        </div>
      ) : !data ? (
        <div aria-busy="true">
          <EmptyState variant="comingSoon" skeletonRows={3}>
            {t("quality.states.loading")}
          </EmptyState>
        </div>
      ) : !refreshedAt ? (
        <EmptyState title={t("quality.states.notComputed")}>
          <p className="mb-3">
            {canRefresh
              ? t("quality.states.notComputedText")
              : t("quality.states.notComputedReadOnly")}
          </p>
          {refreshButton(true, t("quality.refreshNow"))}
        </EmptyState>
      ) : (
        <>
          <OverviewSection
            overview={data}
            selectedCheck={state.check}
            onSelectCheck={(code) => update({ check: code, facets: {} })}
          />
          <UnmappedSection
            facets={facets}
            books={books}
            functionWords={state.functionWords}
            check={state.check}
            classFacet={state.facets.class}
            selectedBookId={state.bookId}
            update={update}
            openFilter={openFilter}
          />
          <CoverageSection
            metric={state.metric}
            testament={state.testament}
            books={books}
            bookId={state.bookId}
            chapter={state.facets.chapter}
            update={update}
          />
          <Explorer
            state={state}
            facets={facets}
            books={books}
            update={update}
            openFilter={openFilter}
            goToCrumb={goToCrumb}
          />
          <Findings
            state={state}
            facets={facets}
            books={books}
            update={update}
            openFilter={openFilter}
          />
        </>
      )}
    </div>
  );
}
