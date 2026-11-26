import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Select } from "@/components/Select";
import { bookName } from "@/lib/bible/books";
import type { BibleStructureBook } from "@/lib/bible/types";
import type { LxxReviewBook } from "@/lib/maintainer/lxxReview";
import { useMediaQuery } from "@/lib/useMediaQuery";

export interface ChapterListProps {
  books: LxxReviewBook[];
  structure: BibleStructureBook[] | undefined;
  currentBookId: number | undefined;
  currentChapter: number | undefined;
  /** Keeps the view params (`f`, `form`) on the links. */
  search: string;
  /** Books are loading: three muted skeleton lines instead of the list. */
  loading?: boolean;
  /** Mobile: navigate on Select change. */
  onNavigate: (bookId: number, chapter: number) => void;
}

/**
 * Left column of `/edit/lxx` (06a "Layout"): books with open work and their Rahlfs chapters with counts; below md two
 * Selects (book, chapter); the count of AI-`low` words is shown next to the open units.
 */
export function ChapterList({
  books,
  structure,
  currentBookId,
  currentChapter,
  search,
  loading = false,
  onNavigate,
}: ChapterListProps) {
  const { t, i18n } = useTranslation();
  const nameOf = (id: number) => {
    const book = structure?.find((b) => b.id === id);
    return book ? bookName(book, i18n.language) : `#${id}`;
  };
  const current = books.find((b) => b.bibleBookId === currentBookId);
  const desktop = useMediaQuery("(min-width: 48rem)");
  const bookOptions = books.map((b) => ({
    value: String(b.bibleBookId),
    label: nameOf(b.bibleBookId),
  }));

  return (
    <nav
      aria-label={t("lxxReview.list.label")}
      className="flex flex-col gap-3 md:w-64 md:shrink-0 md:overflow-y-auto"
    >
      <p className="text-xs text-fg-muted">{t("lxxReview.list.rahlfs")}</p>
      {loading ? (
        <ul aria-hidden="true" className="flex flex-col gap-2">
          {[1, 2, 3].map((n) => (
            <li key={n} className="h-5 rounded-sm bg-surface-muted" />
          ))}
        </ul>
      ) : desktop ? (
        <ul className="flex flex-col gap-2 overflow-y-auto">
          {books.map((book) => (
            <li key={book.bibleBookId}>
              <p className="flex justify-between font-medium">
                <span>{nameOf(book.bibleBookId)}</span>
                <span className="text-fg-muted">{book.openUnits}</span>
              </p>
              <ul className="flex flex-col">
                {book.chapters.map((c) => {
                  const active = book.bibleBookId === currentBookId && c.chapter === currentChapter;
                  return (
                    <li key={c.chapter}>
                      <Link
                        to={`/edit/lxx/${book.bibleBookId}/${c.chapter}${search}`}
                        aria-current={active ? "page" : undefined}
                        className={`flex justify-between rounded-md px-2 py-1 ${active ? "bg-surface-muted text-accent" : ""}`}
                      >
                        <span>{c.chapter}</span>
                        <span className="text-fg-muted">
                          {t("lxxReview.list.open", { count: c.openUnits })}
                          {c.lowWords > 0
                            ? ` · ${t("lxxReview.list.low", { count: c.lowWords })}`
                            : ""}
                        </span>
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </li>
          ))}
        </ul>
      ) : (
        <div className="flex gap-2">
          <Select
            label={t("lxxReview.list.book")}
            size="sm"
            value={String(currentBookId ?? "")}
            onChange={(value) => {
              const book = books.find((b) => b.bibleBookId === Number(value));
              const first = book?.chapters[0]?.chapter;
              if (book && first !== undefined) onNavigate(book.bibleBookId, first);
            }}
            options={bookOptions}
          />
          <Select
            label={t("lxxReview.list.chapter")}
            size="sm"
            value={String(currentChapter ?? "")}
            onChange={(value) => currentBookId && onNavigate(currentBookId, Number(value))}
            options={(current?.chapters ?? []).map((c) => ({
              value: String(c.chapter),
              label: `${c.chapter} (${c.openUnits})`,
            }))}
          />
        </div>
      )}
    </nav>
  );
}
