import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { CoverageHeatmap, type HeatRow } from "@/components/CoverageHeatmap";
import { bookName } from "@/lib/bible/books";
import type { BibleStructureBook } from "@/lib/bible/types";
import { formatPercent } from "@/lib/chart";
import { SHARE_SCALE } from "@/lib/quality/checks";
import { useCoverage } from "@/lib/quality/queries";
import { SECTION_IDS, scrollToId } from "@/lib/quality/scroll";
import type { CoverageMetric, TestamentFacet } from "@/lib/quality/types";
import type { QualityPatch } from "@/lib/quality/url";
import { QueryState } from "./QueryState";

const METRICS: CoverageMetric[] = ["german", "source"];

export function CoverageSection({
  metric,
  testament,
  books,
  bookId,
  chapter,
  update,
}: {
  metric: CoverageMetric;
  testament?: TestamentFacet;
  books: BibleStructureBook[];
  bookId?: number;
  chapter?: string;
  update: (patch: QualityPatch) => void;
}) {
  const { t, i18n } = useTranslation();
  const query = useCoverage(metric, testament, true);
  const language = i18n.language;

  const selectedBook = books.find((book) => book.id === bookId);
  const selected = selectedBook && chapter ? { rowKey: String(bookId), cellKey: chapter } : null;

  return (
    <section
      id={SECTION_IDS.coverage}
      aria-labelledby="quality-coverage-h"
      className="grid scroll-mt-20 gap-3"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="quality-coverage-h" className="text-lg font-semibold">
          {t("quality.sections.coverage")}
        </h2>
        <fieldset className="flex gap-1">
          <legend className="sr-only">{t("quality.heatmap.metric")}</legend>
          {METRICS.map((value) => (
            <Button
              key={value}
              size="sm"
              aria-pressed={metric === value}
              onClick={() => update({ metric: value })}
            >
              {t(`quality.heatmap.${value}`)}
            </Button>
          ))}
        </fieldset>
      </div>
      <QueryState query={query}>
        {(data) => {
          const rows: HeatRow[] = [];
          for (const entry of data.books) {
            const book = books.find((b) => b.id === entry.bibleBookId);
            if (!book) continue;
            const name = bookName(book, language);
            rows.push({
              key: String(book.id),
              label: name,
              text: name,
              cells: entry.chapters.map((c) => ({
                key: String(c.chapter),
                label: String(c.chapter),
                value: c.unaligned,
                total: c.total,
                title: t("quality.heatmap.cell", {
                  book: name,
                  chapter: c.chapter,
                  percent: formatPercent(c.unaligned, c.total, language),
                  count: c.unaligned,
                  total: c.total,
                }),
              })),
            });
          }
          rows.sort((a, b) => Number(a.key) - Number(b.key));
          return (
            <>
              <CoverageHeatmap
                title={t("quality.heatmap.title")}
                rows={rows}
                mode="share"
                scale={SHARE_SCALE}
                legend={t("quality.heatmap.legend")}
                selected={selected}
                tableCaption={t("quality.heatmap.title")}
                onSelectCell={(rowKey, cellKey) => {
                  const book = books.find((b) => String(b.id) === rowKey);
                  if (!book) return;
                  const code = metric === "german" ? "U1" : book.testament === "nt" ? "U2" : "U3";
                  update({ bookId: book.id, check: code, facets: { chapter: cellKey } });
                  scrollToId(SECTION_IDS.findings);
                }}
              />
              {selectedBook && chapter ? (
                <p className="flex flex-wrap items-center gap-3 text-sm">
                  <span>
                    {t("quality.heatmap.selected", {
                      book: bookName(selectedBook, language),
                      chapter,
                    })}
                  </span>
                  <a
                    href={`/bible/${selectedBook.id}/${chapter}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-accent underline"
                  >
                    {t("quality.heatmap.open")}
                    <span className="sr-only"> {t("quality.findings.newTab")}</span>
                  </a>
                </p>
              ) : null}
            </>
          );
        }}
      </QueryState>
    </section>
  );
}
