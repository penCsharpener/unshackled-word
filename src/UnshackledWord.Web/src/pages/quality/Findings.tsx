import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { CoverageHeatmap } from "@/components/CoverageHeatmap";
import { type FindingColumn, FindingsTable } from "@/components/FindingsTable";
import { RankedBarList } from "@/components/RankedBarList";
import { bookName } from "@/lib/bible/books";
import type { BibleStructureBook } from "@/lib/bible/types";
import { formatNumber } from "@/lib/chart";
import { AGGREGATED_CHECKS } from "@/lib/quality/checks";
import { useCheckFindings } from "@/lib/quality/queries";
import { SECTION_IDS } from "@/lib/quality/scroll";
import type { CheckGroup, CheckMatrix, QualityFacets } from "@/lib/quality/types";
import type { QualityPatch, QualityUrlState, TrailEntry } from "@/lib/quality/url";
import { CheckHelp, SectionHelp } from "./SectionHelp";
import { useJumps } from "./useJumps";

const COUNT_SCALE = [1, 10, 100, 1000, 10000] as const;
const STTS_COLUMNS: FindingColumn[] = ["ref", "german", "stts", "pos", "detail", "severity"];
const EDITION_COLUMNS: FindingColumn[] = ["ref", "german", "original", "detail", "severity"];
const VOCAB_ROOT_COLUMNS: FindingColumn[] = ["original", "detail", "severity"];
const TSK_COLUMNS: FindingColumn[] = ["ref", "german", "detail", "severity"];

function matrixRows(matrix: CheckMatrix) {
  return matrix.rows.map((row) => ({
    key: row,
    label: row,
    cells: matrix.columns.map((column) => {
      const cell = matrix.cells.find((c) => c.row === row && c.column === column);
      const count = cell?.count ?? 0;
      return {
        key: column,
        label: count > 0 ? String(count) : "",
        value: count,
        title: `${row} × ${column}: ${count}`,
      };
    }),
  }));
}

/** C1 groups are keyed `bookId:chapter`; the server label is just the numbers. */
function groupLabel(
  code: string,
  group: CheckGroup,
  books: BibleStructureBook[],
  language: string,
) {
  if (code !== "C1") return group.label;
  const [bookId, chapter] = group.key.split(":");
  const book = books.find((b) => b.id === Number(bookId));
  return book ? `${bookName(book, language)} ${chapter}` : group.label;
}

export function Findings({
  state,
  facets,
  books,
  update,
  openFilter,
}: {
  state: QualityUrlState;
  facets: QualityFacets;
  books: BibleStructureBook[];
  update: (patch: QualityPatch) => void;
  openFilter: (entry: TrailEntry) => void;
}) {
  const { t, i18n } = useTranslation();
  const code = state.check;
  const query = useCheckFindings(
    code,
    { ...facets, page: state.page, pageSize: state.size, filters: state.facets },
    true,
  );
  const jumps = useJumps(t, openFilter);
  const data = query.data;
  const facetEntries = Object.entries(state.facets);

  // Aggregated checks list groups first; a group's findings come back with `group` (+ `lang`).
  const aggregated = !!code && AGGREGATED_CHECKS.includes(code);
  const groupSelected = !!state.facets.group;
  const groupsOnly = aggregated && !groupSelected && !!data;
  const expandable = code !== "C1";

  const removeFacet = (name: string) => {
    // `lang` of an expanded group only makes sense together with it
    const drop = name === "group" ? ["group", "lang"] : [name];
    update({
      facets: Object.fromEntries(Object.entries(state.facets).filter(([k]) => !drop.includes(k))),
    });
  };

  const selectGroup = (key: string) => {
    if (code === "M4") {
      // M4 groups are the testaments (its items are always listed); `group` is not a filter there
      update({ testament: state.testament === key ? undefined : (key as "ot" | "nt") });
      return;
    }
    const group = data?.groups?.find((g) => g.key === key);
    update({
      facets: { ...state.facets, group: key, ...(group?.lang ? { lang: group.lang } : {}) },
    });
  };

  const groupFrom = data && data.total > 0 ? (state.page - 1) * state.size + 1 : 0;
  const groupTo = data ? Math.min(state.page * state.size, data.total) : 0;

  return (
    <section
      id={SECTION_IDS.findings}
      aria-labelledby="quality-findings-h"
      className="grid scroll-mt-20 gap-4"
    >
      <h2 id="quality-findings-h" className="text-lg font-semibold">
        {t("quality.sections.findings")}
        {code ? `: ${code} ${t(`quality.checks.${code}.title`, { defaultValue: "" })}` : ""}
      </h2>
      <SectionHelp>
        <p>{t("quality.help.findings.text")}</p>
        {code ? <CheckHelp code={code} /> : null}
      </SectionHelp>
      {!code ? (
        <p className="text-sm text-fg-muted">{t("quality.findings.selectCheck")}</p>
      ) : (
        <>
          {facetEntries.length > 0 ? (
            <ul className="flex flex-wrap gap-2">
              {facetEntries.map(([name, value]) => (
                <li key={name}>
                  <Button
                    size="sm"
                    aria-label={t("quality.findings.removeFacet", { name: `${name}=${value}` })}
                    onClick={() => removeFacet(name)}
                  >
                    {name}: {value} <span aria-hidden="true">✕</span>
                  </Button>
                </li>
              ))}
            </ul>
          ) : null}
          {data?.matrix ? (
            <CoverageHeatmap
              title={t("quality.findings.matrix")}
              rows={matrixRows(data.matrix)}
              columns={data.matrix.columns.map((column) => ({ key: column, label: column }))}
              mode="count"
              scale={COUNT_SCALE}
              showLabels
              tableCaption={t("quality.findings.matrix")}
              outlined={(row, column) =>
                data.matrix?.cells.some(
                  (c) => c.row === row && c.column === column && !c.expected,
                ) ?? false
              }
              selected={
                state.facets.pos && state.facets.class
                  ? { rowKey: state.facets.pos, cellKey: state.facets.class }
                  : null
              }
              onSelectCell={(row, column) =>
                update({
                  facets: {
                    ...state.facets,
                    pos: row,
                    // M3: `G:NOUN`-style class; L5: the field. Both go through `class`.
                    class: column,
                  },
                })
              }
            />
          ) : null}
          {data?.groups && data.groups.length > 0 ? (
            <RankedBarList
              title={t("quality.findings.groups")}
              items={data.groups.map((group) => ({
                key: group.key,
                label: groupLabel(code, group, books, i18n.language),
                value: group.count,
                share: group.share,
                flags: (group.flags ?? []).map((flag) => ({
                  key: flag,
                  label:
                    flag === "error"
                      ? t("quality.severity.error")
                      : t(`quality.flags.${flag}`, { defaultValue: flag }),
                  severity: flag === "error" ? ("error" as const) : ("info" as const),
                })),
              }))}
              onSelect={expandable ? selectGroup : undefined}
              selectedKey={code === "M4" ? (state.testament ?? null) : (state.facets.group ?? null)}
            />
          ) : null}
          {groupsOnly ? (
            <div className="grid gap-3">
              {data?.total === 0 ? (
                <p className="text-sm text-fg-muted">{t("quality.states.emptyCheck")}</p>
              ) : expandable ? (
                <p className="text-sm text-fg-muted">{t("quality.findings.pickGroup")}</p>
              ) : null}
              {data && data.total > state.size ? (
                <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
                  <span aria-live="polite">
                    {t("quality.findings.groupsShowing", {
                      from: formatNumber(groupFrom, i18n.language),
                      to: formatNumber(groupTo, i18n.language),
                      total: formatNumber(data.total, i18n.language),
                    })}
                  </span>
                  <span className="flex gap-2">
                    <Button
                      size="sm"
                      disabled={state.page <= 1}
                      onClick={() => update({ page: state.page - 1 })}
                    >
                      {t("quality.findings.prev")}
                    </Button>
                    <Button
                      size="sm"
                      disabled={groupTo >= data.total}
                      onClick={() => update({ page: state.page + 1 })}
                    >
                      {t("quality.findings.next")}
                    </Button>
                  </span>
                </div>
              ) : null}
            </div>
          ) : (
            <FindingsTable
              findings={data?.items ?? []}
              total={data?.total ?? 0}
              page={state.page}
              pageSize={state.size}
              onPage={(page) => update({ page })}
              onPageSize={(size) => update({ size: size as 50 | 100 | 250 })}
              books={books}
              columns={
                code === "L6" || code === "L7"
                  ? STTS_COLUMNS
                  : code === "T1" || code === "T2"
                    ? TSK_COLUMNS
                    : code === "G1" || code === "G2"
                      ? EDITION_COLUMNS
                      : code === "V1" || code === "V2" || code === "V3"
                        ? VOCAB_ROOT_COLUMNS
                        : undefined
              }
              jumps={jumps}
              loading={query.isFetching}
              error={query.isError}
              onRetry={() => void query.refetch()}
            />
          )}
        </>
      )}
    </section>
  );
}
