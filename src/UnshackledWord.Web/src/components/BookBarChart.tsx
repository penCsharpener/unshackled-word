import { type ReactNode, useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { bookName } from "@/lib/bible/books";
import { BIBLE_SECTIONS, bookSection } from "@/lib/bible/sections";
import type { BibleStructureBook, Testament } from "@/lib/bible/types";
import { formatNumber, formatPercent } from "@/lib/chart";
import { Button } from "./Button";

export interface BookCount {
  bibleBookId: number;
  /** Zero counts may be omitted. */
  count: number;
}

export interface BookBarChartProps {
  books: BibleStructureBook[];
  counts: BookCount[];
  /** Parts to show, in order, e.g. `["nt"]` or `["ot", "nt"]`. All bars share one scale. */
  testaments: Testament[];
  title: ReactNode;
  summary?: ReactNode;
  /** Makes every bar with occurrences a button; the selected book is passed again to deselect. */
  onSelectBook?: (bookId: number | null) => void;
  selectedBookId?: number | null;
  /** Chart columns need a wide container; narrower ones always show the list. */
  defaultView?: "chart" | "list";
}

interface BookRow {
  book: BibleStructureBook;
  count: number;
  slot: number | undefined;
  section: string | undefined;
  /** 0-100 relative to the largest count of all shown books. */
  percent: number;
}

const MIN_BAR_PERCENT = 1.5;

function abbreviation(book: BibleStructureBook, language: string): string {
  return language.startsWith("en") ? book.abbreviationEn : book.abbreviationDe;
}

function barPercent(count: number, max: number): number {
  if (count <= 0 || max <= 0) return 0;
  return Math.max((count * 100) / max, MIN_BAR_PERCENT);
}

export function BookBarChart({
  books,
  counts,
  testaments,
  title,
  summary,
  onSelectBook,
  selectedBookId = null,
  defaultView = "chart",
}: BookBarChartProps) {
  const { t, i18n } = useTranslation();
  const headingId = useId();
  const [view, setView] = useState<"chart" | "list">(defaultView);
  const language = i18n.language;

  const countByBook = new Map(counts.map((c) => [c.bibleBookId, c.count]));
  const shown = [...books]
    .filter((b) => testaments.includes(b.testament))
    .sort((a, b) => a.id - b.id);
  const max = Math.max(0, ...shown.map((b) => countByBook.get(b.id) ?? 0));

  const rowOf = (book: BibleStructureBook): BookRow => {
    const count = countByBook.get(book.id) ?? 0;
    const section = bookSection(book.id);
    return {
      book,
      count,
      slot: section?.slot,
      section: section?.key,
      percent: barPercent(count, max),
    };
  };

  const parts = testaments.map((testament) => {
    const rows = shown.filter((b) => b.testament === testament).map(rowOf);
    return { testament, rows, total: rows.reduce((sum, r) => sum + r.count, 0) };
  });

  const usedSections = new Set(
    parts.flatMap((p) => p.rows.filter((r) => r.count > 0).map((r) => r.section)),
  );
  const legend = BIBLE_SECTIONS.filter((s) => usedSections.has(s.key));
  const multiple = parts.length > 1;

  const select = (bookId: number) => onSelectBook?.(selectedBookId === bookId ? null : bookId);

  return (
    <section
      aria-labelledby={headingId}
      data-view={view}
      data-has-selection={selectedBookId !== null ? "" : undefined}
      className="bookchart @container"
    >
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id={headingId} className="font-scripture text-xl font-semibold @3xl:text-2xl">
          {title}
        </h2>
        <div className="bookchart-toggle">
          <Button
            variant="ghost"
            size="sm"
            className="text-accent underline"
            onClick={() => setView(view === "list" ? "chart" : "list")}
          >
            {view === "list" ? t("bible.stats.books.showChart") : t("bible.stats.books.showList")}
          </Button>
        </div>
      </div>
      {summary ? <p className="text-sm text-fg-muted">{summary}</p> : null}
      {legend.length > 0 ? (
        <ul
          aria-label={t("bible.stats.books.legend")}
          className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-fg-muted"
        >
          {legend.map((section) => (
            <li key={section.key} data-slot={section.slot} className="flex items-center gap-1.5">
              <span className="chart-swatch" />
              {t(`bible.stats.section.${section.key}`)}
            </li>
          ))}
        </ul>
      ) : null}

      <div className="bookchart-parts mt-4 grid gap-4" data-both={multiple ? "" : undefined}>
        {parts.map(({ testament, rows, total }) => {
          const testamentName = t(`bible.testament.${testament}`);
          return (
            <div key={testament}>
              {multiple ? (
                <h3 className="text-sm font-semibold tracking-wide text-fg-muted uppercase">
                  {t("bible.stats.books.testamentTotal", {
                    testament: testamentName,
                    count: formatNumber(total, language),
                  })}
                </h3>
              ) : null}

              <ol
                className="bookchart-cols"
                aria-label={t("bible.stats.books.columnsLabel", { testament: testamentName })}
              >
                {rows.map((row) => {
                  const name = bookName(row.book, language);
                  const label = t("bible.stats.books.bookCount", {
                    name,
                    count: formatNumber(row.count, language),
                  });
                  const body = (
                    <>
                      <div className="bookchart-plot" aria-hidden="true">
                        <svg aria-hidden="true" focusable="false">
                          {row.count > 0 ? (
                            <>
                              <rect
                                className="bookchart-bar"
                                x="5%"
                                width="90%"
                                y={`${100 - row.percent}%`}
                                height={`${row.percent}%`}
                                rx="2"
                              />
                              <text
                                className="bookchart-val"
                                x="50%"
                                y={`${100 - row.percent}%`}
                                dy="-0.4em"
                                textAnchor="middle"
                              >
                                {formatNumber(row.count, language)}
                              </text>
                            </>
                          ) : (
                            <rect
                              className="bookchart-bar-zero"
                              x="5%"
                              width="90%"
                              y="100%"
                              height="2"
                            />
                          )}
                        </svg>
                      </div>
                      <span className="bookchart-abbr" aria-hidden="true">
                        {abbreviation(row.book, language)}
                      </span>
                      <span className="sr-only">{label}</span>
                    </>
                  );
                  return (
                    <li
                      key={row.book.id}
                      data-slot={row.slot}
                      data-zero={row.count === 0 ? "" : undefined}
                      data-selected={selectedBookId === row.book.id ? "" : undefined}
                    >
                      {onSelectBook ? (
                        <Button
                          variant="inline"
                          className="bookchart-col disabled:cursor-default"
                          disabled={row.count === 0}
                          aria-pressed={selectedBookId === row.book.id}
                          onClick={() => select(row.book.id)}
                        >
                          {body}
                        </Button>
                      ) : (
                        <div className="bookchart-col px-0.5">{body}</div>
                      )}
                      {row.count > 0 ? (
                        <span className="bookchart-tip" aria-hidden="true">
                          {t("bible.stats.books.bookTip", {
                            name,
                            count: formatNumber(row.count, language),
                            percent: formatPercent(row.count, total, language),
                          })}
                        </span>
                      ) : null}
                    </li>
                  );
                })}
              </ol>

              <div className="bookchart-list">
                <ol
                  className="mt-2 grid gap-1"
                  aria-label={t("bible.stats.books.listLabel", { testament: testamentName })}
                >
                  {rows.map((row) => {
                    const name = bookName(row.book, language);
                    const count = formatNumber(row.count, language);
                    return (
                      <li
                        key={row.book.id}
                        data-slot={row.slot}
                        data-zero={row.count === 0 ? "" : undefined}
                        data-selected={selectedBookId === row.book.id ? "" : undefined}
                        className={`bookchart-row ${row.count === 0 ? "text-fg-muted" : ""}`}
                      >
                        {onSelectBook && row.count > 0 ? (
                          <Button
                            variant="inline"
                            className="text-left text-accent"
                            title={name}
                            aria-label={t("bible.stats.books.bookCount", { name, count })}
                            aria-pressed={selectedBookId === row.book.id}
                            onClick={() => select(row.book.id)}
                          >
                            {abbreviation(row.book, language)}
                          </Button>
                        ) : (
                          <span title={name}>{abbreviation(row.book, language)}</span>
                        )}
                        <svg className="bookchart-track" aria-hidden="true" focusable="false">
                          {row.count > 0 ? (
                            <rect
                              className="bookchart-bar"
                              x="0"
                              y="0"
                              width={`${row.percent}%`}
                              height="100%"
                              rx="2"
                            />
                          ) : null}
                        </svg>
                        <span
                          className="text-right tabular-nums"
                          aria-hidden={onSelectBook ? true : undefined}
                        >
                          {count}
                        </span>
                      </li>
                    );
                  })}
                </ol>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}
