import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { BASE_LEVEL } from "@/lib/bible/api";
import { bookName } from "@/lib/bible/books";
import { passageTextQuery } from "@/lib/bible/queries";
import { passageSpansParam } from "@/lib/bible/referenceParse";
import type { BibleStructureBook, ParsedReference, ParseReferenceError } from "@/lib/bible/types";
import { Button } from "./Button";

export interface ReferenceCardProps {
  /** References with a chapter; several references (or several chapters in one) give several links. */
  passages: ParsedReference[];
  /** Whole books: a small "Zu Hiob 1" link row. */
  bookRefs?: ParsedReference[];
  /** Hint for a wrong reference (`Offb 23`). */
  error?: ParseReferenceError | null;
  /** Books a typed prefix could mean (`Phili` gives Philipper and Philemon). */
  bookCandidates?: number[];
  books: BibleStructureBook[];
  language: string;
  /** Reader level of the verse text; defaults to the base level. */
  level?: number;
  /** Called after a link was followed (closes the overlay). */
  onNavigate: () => void;
  /** Replaces the typed book with the chosen candidate. */
  onPickBook?: (book: BibleStructureBook) => void;
}

const linkClasses = "text-accent underline";
const VERSES_SHOWN = 5;

/** Up to five verses of one reference. Mounted only for references with verse spans, so cards without need no query client. */
function PassageText({ spans, level }: { spans: string; level: number }) {
  const { t } = useTranslation();
  const query = useQuery(passageTextQuery(spans, level));
  if (query.isError) {
    return <p className="font-normal text-fg-muted">{t("bible.sidePanel.previewError")}</p>;
  }
  if (!query.data) {
    return <p className="font-normal text-fg-muted">{t("bible.sidePanel.previewLoading")}</p>;
  }
  const verses = query.data.passages.flatMap((p) =>
    p.verses.map((v) => ({ key: `${p.chapter}:${v.verse}`, ...v, chapter: p.chapter })),
  );
  const crossChapter = new Set(verses.map((v) => v.chapter)).size > 1;
  return (
    <div className="grid gap-1 font-serif font-normal">
      {verses.slice(0, VERSES_SHOWN).map((v) => (
        <p key={v.key}>
          <sup className="me-1 font-sans text-xs text-fg-muted">
            {crossChapter ? v.key : v.verse}
          </sup>
          {v.text}
        </p>
      ))}
      {verses.length > VERSES_SHOWN || query.data.truncated ? (
        <p className="font-sans text-fg-muted">
          {t("search.reference.moreVerses", { count: Math.max(1, verses.length - VERSES_SHOWN) })}
        </p>
      ) : null}
    </div>
  );
}

export function ReferenceCard({
  passages,
  bookRefs = [],
  error = null,
  bookCandidates = [],
  books,
  language,
  level = BASE_LEVEL,
  onNavigate,
  onPickBook,
}: ReferenceCardProps) {
  const { t } = useTranslation();
  const candidates = bookCandidates
    .map((id) => books.find((b) => b.id === id))
    .filter((b): b is BibleStructureBook => b !== undefined);
  if (passages.length === 0 && bookRefs.length === 0 && !error && candidates.length === 0) {
    return null;
  }
  return (
    <div className="grid gap-2 rounded-md border border-border bg-surface-muted p-3 text-sm">
      {passages.length > 0 ? (
        <ul aria-label={t("search.reference.label")} className="grid gap-1">
          {passages.map((reference) => {
            const groups =
              reference.groups.length > 0
                ? reference.groups
                : [{ display: reference.display, path: reference.path }];
            const spans = passageSpansParam(reference);
            return (
              <li key={`${reference.display}|${reference.path ?? ""}`} className="font-medium">
                {groups.map((group, index) => (
                  <span key={`${group.display}|${group.path ?? ""}`}>
                    {index > 0 ? "; " : null}
                    {group.path ? (
                      <Link to={group.path} className={linkClasses} onClick={onNavigate}>
                        {group.display}
                      </Link>
                    ) : (
                      group.display
                    )}
                  </span>
                ))}
                {spans ? <PassageText spans={spans} level={level} /> : null}
              </li>
            );
          })}
        </ul>
      ) : null}
      {error ? (
        <p role="alert" className="text-fg">
          {t(`search.reference.errors.${error.code}`, {
            ...error.args,
            defaultValue: t("search.reference.errors.generic"),
          })}
        </p>
      ) : null}
      {candidates.length > 0 ? (
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-fg-muted">{t("search.reference.didYouMean")}</span>
          {candidates.map((book) => (
            <Button key={book.id} variant="secondary" size="sm" onClick={() => onPickBook?.(book)}>
              {bookName(book, language)}
            </Button>
          ))}
        </div>
      ) : null}
      {bookRefs.length > 0 ? (
        <ul aria-label={t("search.reference.booksLabel")} className="flex flex-wrap gap-3">
          {bookRefs.map((reference) => (
            <li key={reference.bookId}>
              <Link
                to={`/bible/${reference.bookId}/1`}
                className={linkClasses}
                onClick={onNavigate}
              >
                {t("search.reference.toBook", { book: reference.display })}
              </Link>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
