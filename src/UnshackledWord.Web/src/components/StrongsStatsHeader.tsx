import { type ReactNode, useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { useBibleStructure } from "@/lib/bible/queries";
import {
  germanGloss,
  isShownStrongs,
  originalTextAttributes,
  renderingSliceKey,
  sliceRenderingKey,
  statsTestaments,
  useStatsRefresh,
  useWordStats,
} from "@/lib/bible/strongsStats";
import type {
  GetStrongsSummaryResponse,
  OriginalLanguage,
  StrongsRendering,
  Testament,
  WordStatsCandidate,
  WordStatsOriginal,
} from "@/lib/bible/types";
import { formatStamp } from "@/lib/format";
import { BookBarChart } from "./BookBarChart";
import { Button } from "./Button";
import { DonutChart, type DonutSlice } from "./DonutChart";
import { EmptyState } from "./EmptyState";
import { FormError } from "./FormError";
import { StrongsRenderings } from "./StrongsRenderings";

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

/** Part-of-speech tag as a label (`VERB` becomes "Verb"); unknown tags are shown as they are. */
function usePosLabel() {
  const { t } = useTranslation();
  return (pos: string) => t(`bible.pos.${pos}`, { defaultValue: pos });
}

/** "ging -> gehen, Verb": which lemma the typed word was resolved to. */
function ResolvedLemma({ typed, lemma, pos }: { typed: string; lemma: string; pos?: string }) {
  const { t } = useTranslation();
  const posLabel = usePosLabel();
  const same = typed.toLowerCase() === lemma.toLowerCase();
  return (
    <span lang="de" className="mt-1 block text-fg-muted" data-testid="resolved-lemma">
      {same ? lemma : t("bible.stats.donut.resolved", { typed, lemma })}
      {pos ? `, ${posLabel(pos)}` : null}
    </span>
  );
}

/** One button per lemma/PoS candidate when a word has several readings (Gang: Nomen, gehen: Verb). */
function PosSwitch({
  candidates,
  active,
  onSelect,
}: {
  candidates: WordStatsCandidate[];
  active?: string;
  onSelect: (pos: string | undefined) => void;
}) {
  const { t } = useTranslation();
  const posLabel = usePosLabel();
  // The API answers per part of speech with its most frequent lemma, so a second lemma with the same PoS
  // (a tagging error such as "Ging" next to "gehen") cannot be chosen; offer one button per PoS.
  const options = candidates.filter(
    (candidate, index) => candidates.findIndex((c) => c.pos === candidate.pos) === index,
  );
  return (
    <fieldset className="mt-2 flex flex-wrap items-center gap-1">
      <legend className="sr-only">{t("bible.stats.donut.posSwitch")}</legend>
      {options.map((candidate) => (
        <Button
          key={candidate.pos ?? candidate.lemma}
          size="sm"
          aria-pressed={candidate.pos === active}
          onClick={() => onSelect(candidate.pos)}
        >
          {candidate.pos ? posLabel(candidate.pos) : candidate.lemma}
          {" · "}
          {candidate.occurrences}
        </Button>
      ))}
    </fieldset>
  );
}

/** Donut: the Strong's numbers behind one German word in one testament. */
function WordStatsDonut({
  word,
  pos: initialPos,
  testament,
  summary,
}: {
  word: string;
  /** Part of speech of the rendering this donut belongs to; the switch can change it. */
  pos?: string;
  testament: Testament;
  summary: GetStrongsSummaryResponse;
}) {
  const { t } = useTranslation();
  const [chosenPos, setChosenPos] = useState<string | undefined>(initialPos);
  const stats = useWordStats(word, testament, chosenPos);
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
  const candidates = data?.candidates ?? [];
  const resolved = data?.lemma ? (
    <ResolvedLemma typed={word} lemma={data.lemma} pos={data.pos} />
  ) : null;
  const posSwitch =
    new Set(candidates.map((c) => c.pos)).size > 1 ? (
      <PosSwitch candidates={candidates} active={data?.pos} onSelect={setChosenPos} />
    ) : null;
  if (!data || data.occurrenceCount === 0 || data.originals.length === 0) {
    return (
      <EmptyState title={title}>
        {t("bible.stats.donut.empty", {
          word,
          testament: t(`bible.stats.donut.testamentDative.${testament}`),
        })}
        {resolved}
        {posSwitch}
      </EmptyState>
    );
  }
  return (
    <DonutChart
      title={title}
      subtitle={
        <>
          {t("bible.stats.donut.total", {
            count: data.occurrenceCount,
            verses: data.verseCount,
          })}
          {resolved}
        </>
      }
      note={
        data.unmappedCount > 0 || posSwitch ? (
          <>
            {data.unmappedCount > 0
              ? t("bible.stats.donut.unmapped", { count: data.unmappedCount })
              : null}
            {posSwitch}
          </>
        ) : undefined
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
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const posLabel = usePosLabel();
  const donutsId = useId();
  const composedId = useId();
  const statsRefresh = useStatsRefresh(strongs);
  const summaryQuery = statsRefresh.summary;
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

  // The contraction row ("no own lemma") is not a German word: no gloss, no donut slice, no filter.
  const renderings = summary.renderings.filter((r) => !r.noOwnLemma);
  const gloss = germanGloss(renderings).join(", ");
  const testaments = statsTestaments(summary.language, summary.byBook);
  const totalBooks = books?.filter((book) => testaments.includes(book.testament)).length;
  const chartKey = testaments.length === 2 ? "all" : (testaments[0] ?? "nt");
  const selectedRow = renderings.find((r) => r.key === rendering);
  const selectedRendering = selectedRow?.word ?? rendering ?? renderings[0]?.word;
  const selectedPos = selectedRow ? selectedRow.pos : rendering ? undefined : renderings[0]?.pos;
  const canRefresh = hasWriteRole(user) && !statsRefresh.forbidden;
  const stamp = summary.statsRefreshedAt
    ? t("bible.strongs.statsStand", { date: formatStamp(summary.statsRefreshedAt, i18n.language) })
    : t("bible.strongs.statsLive");

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
        {summary.linkRolesAppliedAt ? null : (
          <p
            role="status"
            className="mt-2 rounded-md border border-sev-warning bg-surface p-3 text-sm"
            data-testid="roles-incomplete"
          >
            {t("bible.strongs.rolesIncomplete")}
          </p>
        )}
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
                  slices={renderings.map((r) => renderingSlice(r, posLabel))}
                  centerLabel={t("bible.stats.donut.centerTranslations")}
                  activeKey={
                    rendering === undefined
                      ? null
                      : renderingSliceKey(
                          renderings.find((r) => r.key === rendering) ?? {
                            key: rendering,
                            word: rendering,
                            count: 0,
                          },
                        )
                  }
                  onSelectSlice={(key) =>
                    onSelectRendering(key === null ? null : sliceRenderingKey(key))
                  }
                  filterLabel={t("bible.stats.donut.filterTranslation")}
                />
                <StrongsRenderings renderings={summary.renderings} />
                {selectedRendering
                  ? DONUT_TESTAMENTS.map((testament) => (
                      <WordStatsDonut
                        key={`${selectedRendering}-${selectedPos ?? ""}-${testament}`}
                        word={selectedRendering}
                        pos={selectedPos}
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

      <footer className="flex flex-wrap items-center gap-2 text-sm text-fg-muted">
        <span>{statsRefresh.refreshing ? t("bible.strongs.statsRefreshing") : stamp}</span>
        {canRefresh ? (
          <Button
            variant="ghost"
            size="sm"
            disabled={statsRefresh.refreshing}
            title={t("quality.refreshHint")}
            onClick={statsRefresh.refresh}
          >
            {t("quality.refresh")}
          </Button>
        ) : null}
        {statsRefresh.failed ? <FormError message={t("quality.refreshFailed")} /> : null}
      </footer>
    </div>
  );
}

/** A rendering row: the lemma (or form) with its part of speech when the snapshot knows it. */
function renderingSlice(r: StrongsRendering, posLabel: (pos: string) => string): DonutSlice {
  const text = r.pos ? `${r.word}, ${posLabel(r.pos)}` : r.word;
  return {
    key: renderingSliceKey(r),
    label: (
      <>
        <span lang="de">{r.word}</span>
        {r.pos ? <span className="text-fg-muted">, {posLabel(r.pos)}</span> : null}
      </>
    ),
    text,
    value: r.count,
  };
}
