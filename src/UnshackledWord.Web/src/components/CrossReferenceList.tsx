import { type KeyboardEvent, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { bookAbbreviation, bookName, chapterPath } from "@/lib/bible/books";
import type { BibleStructureBook, CrossReference } from "@/lib/bible/types";

export interface CrossReferenceListProps {
  references: CrossReference[];
  /** book names for the labels; the numeric book id is shown until they are loaded */
  books?: BibleStructureBook[];
  /**
   * `details`: collapsible list with a count heading (word box; long lists start collapsed).
   * `inline`: plain wrapping links for the side panel and the eye panel; one Tab stop, arrow keys move.
   */
  variant: "details" | "inline";
  /** `details` only: the verse has more references than listed */
  truncated?: boolean;
}

const OPEN_MAX = 5;
const KEYS = ["ArrowRight", "ArrowDown", "ArrowLeft", "ArrowUp", "Home", "End"];

const keyOf = (r: CrossReference) => `${r.bibleBookId}-${r.chapter}-${r.verse}-${r.endVerse ?? 0}`;

/** TSK targets as links to the verse. Labels use the abbreviated book name, the screen reader label the full one. */
export function CrossReferenceList({
  references,
  books,
  variant,
  truncated,
}: CrossReferenceListProps) {
  const { t, i18n } = useTranslation();
  const [active, setActive] = useState(0);
  const listRef = useRef<HTMLUListElement>(null);

  const label = (r: CrossReference, full: boolean) => {
    const book = books?.find((b) => b.id === r.bibleBookId);
    const name = book
      ? full
        ? bookName(book, i18n.language)
        : bookAbbreviation(book, i18n.language)
      : r.bibleBookId;
    const verses = r.endVerse ? `${r.verse}–${r.endVerse}` : r.verse;
    return `${name} ${r.chapter},${verses}`;
  };

  const to = (r: CrossReference) =>
    `${chapterPath({ bookId: r.bibleBookId, chapter: r.chapter })}#v${r.verse}`;

  if (variant === "inline") {
    const move = (event: KeyboardEvent) => {
      if (!KEYS.includes(event.key)) return;
      const links = listRef.current?.querySelectorAll<HTMLAnchorElement>("a") ?? [];
      const index = Array.from(links).indexOf(event.target as HTMLAnchorElement);
      if (index < 0) return;
      event.preventDefault();
      const last = links.length - 1;
      const next =
        event.key === "Home"
          ? 0
          : event.key === "End"
            ? last
            : event.key === "ArrowRight" || event.key === "ArrowDown"
              ? Math.min(last, index + 1)
              : Math.max(0, index - 1);
      links[next]?.focus();
    };
    return (
      <ul
        ref={listRef}
        onKeyDown={move}
        className="flex flex-wrap gap-x-2 gap-y-1 font-sans text-sm"
      >
        {references.map((r, index) => (
          <li key={keyOf(r)}>
            <Link
              to={to(r)}
              aria-label={label(r, true)}
              tabIndex={index === active ? 0 : -1}
              onFocus={() => setActive(index)}
              className="text-accent underline"
            >
              {label(r, false)}
            </Link>
            {index < references.length - 1 && (
              <span aria-hidden="true" className="ms-2 text-fg-muted">
                ·
              </span>
            )}
          </li>
        ))}
      </ul>
    );
  }

  return (
    <details
      open={references.length <= OPEN_MAX}
      className="mt-4 text-sm"
      data-testid="cross-references"
    >
      <summary className="cursor-pointer text-fg-muted">
        {t("bible.wordDetail.crossReferences", { count: references.length })}
      </summary>
      <ul className="mt-1 flex flex-wrap gap-x-3 gap-y-1">
        {references.map((r) => (
          <li key={keyOf(r)}>
            <Link to={to(r)} className="text-accent underline">
              {label(r, false)}
            </Link>
          </li>
        ))}
      </ul>
      {truncated && (
        <p className="mt-1 text-xs text-fg-muted">{t("bible.wordDetail.crossReferencesMore")}</p>
      )}
    </details>
  );
}
