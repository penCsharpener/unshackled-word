import { type ReactNode, useId } from "react";
import { useTranslation } from "react-i18next";
import { useBibleStructure } from "@/lib/bible/queries";
import {
  germanGloss,
  isShownStrongs,
  originalTextAttributes,
  statsTestaments,
  useStrongsSummary,
  useWordStats,
} from "@/lib/bible/strongsStats";
import type {
  GetStrongsSummaryResponse,
  OriginalLanguage,
  Testament,
  WordStatsOriginal,
} from "@/lib/bible/types";
import { BookBarChart } from "./BookBarChart";
import { Button } from "./Button";
import { DonutChart, type DonutSlice } from "./DonutChart";
import { EmptyState } from "./EmptyState";

const DONUT_TESTAMENTS: readonly Testament[] = ["ot", "nt"];

function OriginalText({ language, children }: { language: OriginalLanguage; children: ReactNode }) {
  const { lang, dir } = originalTextAttributes(language);
  return (
    <span lang={lang} dir={dir} className="font-original">
      {children}
    </span>
  );
}

function originalSlice(
  original: WordStatsOriginal,
  shown: boolean,
  ofTotalUse: string,
): DonutSlice {
  const name = [original.strongs, original.lemma].filter(Boolean).join(" ");
  return {
    key: original.strongs,
    text: original.gloss ? `${name} – ${original.gloss}` : name,
    value: original.count,
    highlight: shown,
    label: (
      <>
        <span className="font-medium">{original.strongs}</span>
        {original.lemma ? (
          <>
            {" "}
            <OriginalText language={original.language}>{original.lemma}</OriginalText>
          </>
        ) : null}
        {original.gloss ? <span className="text-fg-muted"> – {original.gloss}</span> : null}
        <span className="text-fg-muted"> · {ofTotalUse}</span>
      </>
    ),
  };
}

/** Donut: the Strong's numbers behind one German word in one testament. */
function WordStatsDonut({
  word,
  testament,
  summary,
}: {
  word: string;
  testament: Testament;
  summary: GetStrongsSummaryResponse;
}) {
  const { t } = useTranslation();
  const stats = useWordStats(word, testament);
  const title = t("bible.stats.donut.inTestament", {
    word,
    testament: t(`bible.stats.donut.testamentDative.${testament}`),
  });

  if (stats.isLoading) {
    return <EmptyState title={title}>{t("bible.stats.donut.loading")}</EmptyState>;
  }
  if (stats.isError) {
    return (
      <EmptyState title={title}>
        <div role="alert" className="grid justify-items-start gap-2">
          <p>{t("bible.stats.donut.error", { word })}</p>
          <Button variant="secondary" size="sm" onClick={() => stats.refetch()}>
            {t("bible.retry")}
          </Button>
        </div>
      </EmptyState>
    );
  }
  const data = stats.data;
  if (!data || data.occurrenceCount === 0 || data.originals.length === 0) {
    return (
      <EmptyState title={title}>
        {t("bible.stats.donut.empty", {
          word,
          testament: t(`bible.stats.donut.testamentDative.${testament}`),
        })}
      </EmptyState>
    );
  }
  return (
    <DonutChart
      title={title}
      subtitle={t("bible.stats.donut.total", {
        count: data.occurrenceCount,
        verses: data.verseCount,
      })}
      note={
        data.unmappedCount > 0
          ? t("bible.stats.donut.unmapped", { count: data.unmappedCount })
          : undefined
      }
      slices={data.originals.map((original) =>
        originalSlice(
          original,
          isShownStrongs(original.strongs, summary),
          t("bible.stats.donut.ofTotalUse", { total: original.totalCount }),
        ),
      )}
      centerLabel={t("bible.stats.donut.centerOriginals")}
    />
  );
}

export interface StrongsStatsHeaderProps {
  /** Normalized Strong's number, e.g. `G1577`. */
  strongs: string;
  /** Book filter of the concordance (Elberfelder id). */
  bookId?: number;
  /** German rendering filter of the concordance (`StrongsRendering.key`). */
  rendering?: string;
  onSelectBook: (bookId: number | null) => void;
  onSelectRendering: (key: string | null) => void;
}

/**
 * Top of the Strong's page: heading with lemma and glosses, stat line, "formed from" placeholder,
 * occurrences per book and the translation donuts. Clicking a bar or a rendering slice reports the
 * filter for the concordance below.
 */
export function StrongsStatsHeader({
  strongs,
  bookId,
  rendering,
  onSelectBook,
  onSelectRendering,
}: StrongsStatsHeaderProps) {
  const { t } = useTranslation();
  const donutsId = useId();
  const composedId = useId();
  const summaryQuery = useStrongsSummary(strongs);
  const structure = useBibleStructure();
  const summary = summaryQuery.data;
  const books = structure.data;

  const heading = (
    <h1 className="font-scripture text-2xl font-semibold @3xl:text-3xl">
      {t("bible.strongs.title", { strongs: summary?.strongs ?? strongs })}
      {summary?.lemma ? (
        <>
          {" – "}
          <span className="text-accent">
            <OriginalText language={summary.language}>{summary.lemma}</OriginalText>
          </span>
        </>
      ) : null}
      {summary?.transliteration ? <i> – {summary.transliteration}</i> : null}
    </h1>
  );

  if (summaryQuery.isLoading) {
    return (
      <header className="@container">
        {heading}
        <p className="mt-2 text-fg-muted">{t("bible.strongs.statsLoading")}</p>
      </header>
    );
  }
  if (summaryQuery.isError || !summary) {
    return (
      <header className="@container">
        {heading}
        <div role="alert" className="mt-4 flex flex-wrap items-center gap-4">
          <p>{t("bible.strongs.statsError")}</p>
          <Button variant="secondary" onClick={() => summaryQuery.refetch()}>
            {t("bible.retry")}
          </Button>
        </div>
      </header>
    );
  }

  const gloss = germanGloss(summary.renderings).join(", ");
  const testaments = statsTestaments(summary.language, summary.byBook);
  const totalBooks = books?.filter((book) => testaments.includes(book.testament)).length;
  const chartKey = testaments.length === 2 ? "all" : (testaments[0] ?? "nt");
  const selectedRendering =
    summary.renderings.find((r) => r.key === rendering)?.word ??
    rendering ??
    summary.renderings[0]?.word;

  return (
    <div className="@container grid gap-8">
      <header>
        {heading}
        <p className="mt-2 text-fg-muted">
          {t(`bible.wordDetail.language.${summary.language}`)}
          {gloss ? <> – {gloss}</> : null}
          {summary.gloss ? (
            <>
              {" "}
              (<span lang="en">{summary.gloss}</span>)
            </>
          ) : null}
        </p>
        <p className="mt-1 text-fg-muted">
          {t("bible.strongs.count", { count: summary.occurrenceCount })}
          {" · "}
          {t("bible.strongs.statVerses", { count: summary.verseCount })}
          {totalBooks !== undefined ? (
            <>
              {" · "}
              {t("bible.strongs.statBooks", { count: summary.byBook.length, total: totalBooks })}
            </>
          ) : null}
        </p>
      </header>

      <section aria-labelledby={composedId}>
        <h2 id={composedId} className="mb-2 font-scripture text-xl font-semibold">
          {t("bible.strongs.composedOf.title")}
        </h2>
        <EmptyState variant="comingSoon">{t("bible.strongs.composedOf.soon")}</EmptyState>
      </section>

      {summary.occurrenceCount === 0 ? (
        <EmptyState>{t("bible.strongs.statsEmpty")}</EmptyState>
      ) : (
        <>
          {books ? (
            <BookBarChart
              books={books}
              counts={summary.byBook}
              testaments={testaments}
              title={t(`bible.stats.books.title.${chartKey}`)}
              summary={t("bible.stats.books.summary", {
                count: summary.occurrenceCount,
                books: summary.byBook.length,
                total: totalBooks ?? 0,
              })}
              selectedBookId={bookId ?? null}
              onSelectBook={onSelectBook}
            />
          ) : structure.isError ? (
            <div role="alert" className="flex flex-wrap items-center gap-4">
              <p>{t("bible.structureError")}</p>
              <Button variant="secondary" onClick={() => structure.refetch()}>
                {t("bible.retry")}
              </Button>
            </div>
          ) : (
            <p className="text-fg-muted">{t("bible.strongs.statsLoading")}</p>
          )}

          <section aria-labelledby={donutsId}>
            <h2 id={donutsId} className="mb-4 font-scripture text-xl font-semibold">
              {t("bible.stats.donuts.title")}
            </h2>
            {summary.renderings.length === 0 ? (
              <EmptyState>
                {t("bible.stats.donut.unmapped", { count: summary.unmappedCount })}
              </EmptyState>
            ) : (
              <div className="grid gap-4 @3xl:grid-cols-2 @6xl:grid-cols-3">
                <DonutChart
                  title={
                    <>
                      {summary.strongs}
                      {summary.lemma ? (
                        <>
                          {" – "}
                          <OriginalText language={summary.language}>{summary.lemma}</OriginalText>
                        </>
                      ) : null}
                    </>
                  }
                  subtitle={t("bible.stats.donut.total", {
                    count: summary.occurrenceCount,
                    verses: summary.verseCount,
                  })}
                  note={
                    summary.unmappedCount > 0
                      ? t("bible.stats.donut.unmapped", { count: summary.unmappedCount })
                      : undefined
                  }
                  slices={summary.renderings.map((r) => ({
                    key: r.key,
                    label: <span lang="de">{r.word}</span>,
                    text: r.word,
                    value: r.count,
                  }))}
                  centerLabel={t("bible.stats.donut.centerTranslations")}
                  activeKey={rendering ?? null}
                  onSelectSlice={onSelectRendering}
                  filterLabel={t("bible.stats.donut.filterTranslation")}
                />
                {selectedRendering
                  ? DONUT_TESTAMENTS.map((testament) => (
                      <WordStatsDonut
                        key={`${selectedRendering}-${testament}`}
                        word={selectedRendering}
                        testament={testament}
                        summary={summary}
                      />
                    ))
                  : null}
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}
