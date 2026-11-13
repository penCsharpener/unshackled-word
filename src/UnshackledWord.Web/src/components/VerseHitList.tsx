import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { bookName } from "@/lib/bible/books";
import type { BibleStructureBook, VerseSearchHit } from "@/lib/bible/types";
import { Button } from "./Button";

export interface VerseHitListProps {
  hits: VerseSearchHit[];
  books: BibleStructureBook[];
  language: string;
  /** Index of the hit the keyboard points at (`aria-activedescendant` of the search input), -1 for none. */
  activeIndex: number;
  /** A plain click or Enter on a hit; the caller navigates (modified clicks keep the browser's behaviour). */
  onSelect: (hit: VerseSearchHit) => void;
  onLoadMore: () => void;
  hasMore: boolean;
  /** The next page is being fetched. */
  loading?: boolean;
  /** Id of the `listbox`; the options are `${id}-option-${index}`. */
  id: string;
  label: string;
}

export const hitPath = (hit: VerseSearchHit) =>
  `/bible/${hit.bibleBookId}/${hit.chapter}#v${hit.verse}`;

export const hitOptionId = (listId: string, index: number) => `${listId}-option-${index}`;

/** Ranked verse hits as a listbox of links: reference in the reader language, verse text with the matched words marked. */
export function VerseHitList({
  hits,
  books,
  language,
  activeIndex,
  onSelect,
  onLoadMore,
  hasMore,
  loading = false,
  id,
  label,
}: VerseHitListProps) {
  const { t } = useTranslation();

  useEffect(() => {
    if (activeIndex < 0) return;
    document.getElementById(hitOptionId(id, activeIndex))?.scrollIntoView?.({ block: "nearest" });
  }, [activeIndex, id]);

  return (
    <div className="grid gap-3">
      <div id={id} role="listbox" aria-label={label} className="grid gap-2">
        {hits.map((hit, index) => {
          const book = books.find((b) => b.id === hit.bibleBookId);
          const reference = `${book ? bookName(book, language) : hit.bibleBookId} ${hit.chapter},${hit.verse}`;
          const active = index === activeIndex;
          return (
            <div
              key={`${hit.bibleBookId}-${hit.chapter}-${hit.verse}`}
              id={hitOptionId(id, index)}
              role="option"
              tabIndex={-1}
              aria-selected={active}
              className={`rounded-md border ${active ? "border-accent bg-surface-muted" : "border-border"}`}
            >
              <Link
                to={hitPath(hit)}
                tabIndex={-1}
                onClick={(event) => {
                  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey)
                    return;
                  event.preventDefault();
                  onSelect(hit);
                }}
                className="block rounded-md px-3 py-2 hover:bg-surface-muted"
              >
                <span className="block text-sm font-medium text-accent">{reference}</span>
                <span lang="de" className="font-scripture">
                  {hit.tokens.map((token, i) => (
                    // Tokens are the verse's words in order and never reorder, so the index is the identity.
                    // biome-ignore lint/suspicious/noArrayIndexKey: see above
                    <span key={i}>
                      {i > 0 ? " " : null}
                      {token.matched ? (
                        <mark className="rounded-sm bg-mark-linked px-0.5 text-fg">
                          {token.text}
                        </mark>
                      ) : (
                        token.text
                      )}
                    </span>
                  ))}
                </span>
              </Link>
            </div>
          );
        })}
      </div>
      {hasMore ? (
        <Button
          variant="secondary"
          disabled={loading}
          onClick={onLoadMore}
          className="justify-self-start"
        >
          {loading ? t("search.loading") : t("search.more")}
        </Button>
      ) : null}
    </div>
  );
}
