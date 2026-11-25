import { useQuery } from "@tanstack/react-query";
import { Fragment, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { bookAbbreviation, bookName, chapterPath } from "@/lib/bible/books";
import { SIMILAR_CHAPTER_LIMIT, similarVersesOfVerseQuery } from "@/lib/bible/sidePanel";
import type {
  BibleStructureBook,
  SimilarSharedStrongs,
  SimilarVerseHit,
  VerseSimilarVerses,
} from "@/lib/bible/types";
import { normalizeStrongs } from "@/lib/bible/wordDetail";
import { Button } from "./Button";
import { cardLeft } from "./CrossReferenceList";
import { PassagePreviewCard } from "./PassagePreviewCard";

export interface SimilarVerseListProps {
  /** The source verse with its hits from the chapter request (best first, may be empty). */
  source: VerseSimilarVerses;
  /** book names for the labels; the numeric book id is shown until they are loaded */
  books?: BibleStructureBook[];
  /** Hits per verse in the chapter request: with this many, "Mehr anzeigen" loads up to 30 and replaces the list. */
  limit?: number;
  /**
   * A hit is hovered or focused (`undefined` = none anymore): the reader marks the hit's `sourcePositions` in the source verse
   * (roadmap 0027.4f).
   */
  onHoverHit?: (hit: SimilarVerseHit | undefined) => void;
}

const CARD_GAP = 8;

interface Hover {
  hebRefId: number;
  left: number;
  y: number;
  above: boolean;
}

/** Strong's of all phrases of a hit: these words are shown in their phrase chip, not again as single words. */
const phraseStrongs = (hit: SimilarVerseHit) => new Set(hit.phrases.flatMap((p) => p.strongs));

/** One run of the hit text: marked runs are consecutive words at `targetPositions`. */
export interface TextRun {
  /** 1-based position of the run's first word */
  start: number;
  text: string;
  marked: boolean;
}

/**
 * Splits an Elb1871 verse text into runs; word n (1-based `PositionInVerse`) is the n-th word of the text, which the API
 * builds by joining the words with one space. Consecutive marked words form one run; positions outside the text are ignored.
 */
export function markRuns(text: string, positions: readonly number[] = []): TextRun[] {
  if (positions.length === 0) return [{ start: 1, text, marked: false }];
  const marked = new Set(positions);
  const runs: TextRun[] = [];
  text.split(" ").forEach((word, index) => {
    const isMarked = marked.has(index + 1);
    const last = runs[runs.length - 1];
    if (last && last.marked === isMarked) last.text += ` ${word}`;
    else runs.push({ start: index + 1, text: word, marked: isMarked });
  });
  return runs;
}

/** Relative score in whole percent of the best hit of the verse (the raw score is never shown). */
export function relativeScore(score: number, best: number) {
  return best > 0 ? Math.max(0, Math.min(100, Math.round((score / best) * 100))) : 0;
}

/**
 * Similar verses of one verse (roadmap 0027.3f): reference link with a hover card of the Elb1871 text, the clamped
 * text, chips for the shared original words (phrases first) and a quiet bar relative to the best hit. Labelled
 * "ähnlich", never as a cross reference: the chips say why a verse matched. The words of the hit text that render the
 * shared Strong's are marked (`targetPositions`); hovering or focusing a hit reports it through `onHoverHit`.
 */
export function SimilarVerseList({
  source,
  books,
  limit = SIMILAR_CHAPTER_LIMIT,
  onHoverHit,
}: SimilarVerseListProps) {
  const { t, i18n } = useTranslation();
  const [expanded, setExpanded] = useState(false);
  const [hover, setHover] = useState<Hover | null>(null);
  const more = useQuery({ ...similarVersesOfVerseQuery(source.hebRefId), enabled: expanded });
  const hits = expanded && more.data ? more.data.hits : source.hits;
  const canLoadMore = !(expanded && more.data) && source.hits.length >= limit;
  const best = hits.reduce((max, h) => Math.max(max, h.score), 0);
  const hebrew = source.bibleBookId < 40;
  const lang = hebrew ? "he" : "grc";
  const dir = hebrew ? "rtl" : "ltr";

  const label = (h: SimilarVerseHit, full: boolean) => {
    const book = books?.find((b) => b.id === h.bibleBookId);
    const name = book
      ? full
        ? bookName(book, i18n.language)
        : bookAbbreviation(book, i18n.language)
      : h.bibleBookId;
    return `${name} ${h.chapter},${h.verse}`;
  };

  const show = (h: SimilarVerseHit, element: HTMLElement) => {
    const rect = element.getClientRects()[0] ?? element.getBoundingClientRect();
    const above = rect.bottom + 160 > window.innerHeight && rect.top > window.innerHeight / 2;
    setHover({
      hebRefId: h.hebRefId,
      left: rect.left,
      y: above ? rect.top - CARD_GAP : rect.bottom + CARD_GAP,
      above,
    });
  };
  const hide = () => setHover(null);
  const reportHit = onHoverHit;
  const hovered = hover && hits.find((h) => h.hebRefId === hover.hebRefId);

  const wordDetails = (w: SimilarSharedStrongs) =>
    [w.transliteration, w.gloss && `„${w.gloss}“`].filter(Boolean).join(", ");

  if (hits.length === 0) {
    return <p className="text-fg-muted">{t("bible.sidePanel.similar.empty")}</p>;
  }

  return (
    <div className="flex flex-col gap-2">
      <ol className="flex flex-col gap-2" data-testid="similar-hits">
        {hits.map((h) => {
          const inPhrase = phraseStrongs(h);
          const words = h.shared.filter((w) => !inPhrase.has(w.strongs));
          const percent = relativeScore(h.score, best);
          return (
            <li
              key={h.hebRefId}
              data-testid="similar-hit"
              className="flex flex-col gap-1"
              onMouseEnter={() => reportHit?.(h)}
              onMouseLeave={() => reportHit?.(undefined)}
              onFocus={() => reportHit?.(h)}
              onBlur={() => reportHit?.(undefined)}
            >
              <div className="flex items-center gap-2">
                <Link
                  to={`${chapterPath({ bookId: h.bibleBookId, chapter: h.chapter })}#v${h.verse}`}
                  aria-label={label(h, true)}
                  onFocus={(event) => show(h, event.currentTarget)}
                  onBlur={hide}
                  onMouseEnter={(event) => show(h, event.currentTarget)}
                  onMouseLeave={hide}
                  onKeyDown={(event) => event.key === "Escape" && hide()}
                  className="shrink-0 font-medium text-accent no-underline hover:underline"
                >
                  {label(h, false)}
                </Link>
                <meter
                  className="uw-meter"
                  min={0}
                  max={100}
                  value={percent}
                  aria-label={t("bible.sidePanel.similar.score", { percent })}
                  title={t("bible.sidePanel.similar.score", { percent })}
                />
              </div>
              <p className="line-clamp-2 font-serif text-fg-muted">
                {markRuns(h.text, h.targetPositions).map((run, index) => (
                  <Fragment key={run.start}>
                    {index > 0 && " "}
                    {run.marked ? (
                      <mark className="rounded-sm bg-mark-linked px-0.5 text-fg">{run.text}</mark>
                    ) : (
                      run.text
                    )}
                  </Fragment>
                ))}
              </p>
              {(h.phrases.length > 0 || words.length > 0) && (
                <ul
                  aria-label={t("bible.sidePanel.similar.shared")}
                  className="flex flex-wrap gap-1"
                >
                  {h.phrases.map((p) => (
                    <li key={`p-${p.strongs.join("-")}`} data-chip="phrase">
                      <span
                        lang={lang}
                        dir={dir}
                        title={t("bible.sidePanel.similar.phrase", { text: p.text })}
                        className="inline-block rounded-full border border-accent bg-surface-muted px-2 font-original text-base"
                      >
                        {p.text}
                      </span>
                    </li>
                  ))}
                  {words.map((w) => {
                    const strongs = normalizeStrongs(w.strongs) ?? w.strongs;
                    const details = wordDetails(w);
                    return (
                      <li key={`w-${w.strongs}`} data-chip="word">
                        <Link
                          to={`/bible/strongs/${strongs}`}
                          title={details || undefined}
                          aria-label={t("bible.sidePanel.similar.word", {
                            lemma: w.lemma || strongs,
                            details: details || strongs,
                            strongs,
                          })}
                          className="inline-block rounded-full border border-border px-2 text-fg no-underline hover:bg-surface-muted"
                        >
                          {w.lemma ? (
                            <span lang={lang} dir={dir} className="font-original text-base">
                              {w.lemma}
                            </span>
                          ) : (
                            <span className="font-sans text-xs">{strongs}</span>
                          )}
                        </Link>
                      </li>
                    );
                  })}
                </ul>
              )}
            </li>
          );
        })}
      </ol>
      {canLoadMore && (
        <div className="flex items-center gap-2 text-xs">
          <Button
            variant="ghost"
            size="sm"
            className="px-1 py-0 text-xs text-fg-muted"
            disabled={expanded && more.isFetching}
            onClick={() => (expanded ? void more.refetch() : setExpanded(true))}
          >
            {t("bible.sidePanel.similar.more")}
          </Button>
          <span role="status" className="text-fg-muted">
            {expanded && more.isFetching && t("bible.sidePanel.similar.moreLoading")}
          </span>
          {expanded && more.isError && !more.isFetching && (
            <span role="alert">{t("bible.sidePanel.similar.moreError")}</span>
          )}
        </div>
      )}
      {hover && hovered && (
        <PassagePreviewCard
          position={{ x: cardLeft(hover.left, false), y: hover.y, above: hover.above }}
          title={label(hovered, true)}
          verses={[{ verse: hovered.verse, text: hovered.text }]}
        />
      )}
    </div>
  );
}
