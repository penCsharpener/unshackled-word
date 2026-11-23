import { useQuery } from "@tanstack/react-query";
import { type KeyboardEvent, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { bookAbbreviation, bookName, chapterPath } from "@/lib/bible/books";
import { verseCrossReferenceTextsQuery } from "@/lib/bible/queries";
import type { BibleStructureBook, CrossReference } from "@/lib/bible/types";
import { useMediaQuery } from "@/lib/useMediaQuery";
import { Button } from "./Button";
import { CROSS_REFERENCE_EDITOR_WIDTH, CrossReferenceEditor } from "./CrossReferenceEditor";
import {
  hasOriginals,
  PassagePreviewCard,
  PREVIEW_CARD_WIDE_QUERY,
  PREVIEW_CARD_WIDTH,
  PREVIEW_CARD_WIDTH_WIDE,
} from "./PassagePreviewCard";

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
  /**
   * The verse the references belong to. With it a hover or focus on a link shows the text of the
   * target; the texts of all references of the verse come in one request and are cached for the page.
   */
  preview?: { bookId: number; chapter: number; verse: number; level: number };
  /**
   * Curate mode (roadmap 0024.3f, curators only): links rated `hide` are listed greyed and struck through instead of
   * being left out. Readers never get hidden links from the API; without this prop they are dropped anyway.
   */
  showHidden?: boolean;
  /**
   * `inline` with `preview` only: a pen button after every link opens `CrossReferenceEditor` for it (source verse =
   * `preview`). Maintainers only for now; suggestions by other users follow in 0024.6f.
   */
  editable?: boolean;
}

const CARD_GAP = 8;

/**
 * Left edge of the card: at the link, but inside the viewport at the right edge. The width follows the card:
 * 320 px (`w-80`), 640 px (`md:w-160`) when the passage has originals and the viewport is md or wider.
 */
export function cardLeft(linkLeft: number, wide: boolean, viewportWidth = window.innerWidth) {
  const width = wide ? PREVIEW_CARD_WIDTH_WIDE : PREVIEW_CARD_WIDTH;
  return Math.max(CARD_GAP, Math.min(linkLeft, viewportWidth - width - CARD_GAP));
}

const OPEN_MAX = 5;
const KEYS = ["ArrowRight", "ArrowDown", "ArrowLeft", "ArrowUp", "Home", "End"];

const keyOf = (r: CrossReference) => `${r.bibleBookId}-${r.chapter}-${r.verse}-${r.endVerse ?? 0}`;

/** Marker of a highlighted link (roadmap 0024.3f): a filled star in the current colour. */
function StarIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="me-0.5 inline size-3.5 align-baseline"
      fill="currentColor"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinejoin="round"
    >
      <path d="m12 3 2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9Z" />
    </svg>
  );
}

function PencilIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-3.5"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" />
    </svg>
  );
}

/** Classes of a link by its rating: highlighted bold, hidden (curate mode) greyed and struck through. */
const linkClass = (r: CrossReference) =>
  r.rating === "hide"
    ? "text-fg-muted line-through"
    : r.rating === "highlight"
      ? "font-medium text-accent underline"
      : "text-accent underline";

interface Hover {
  key: string;
  /** left edge of the hovered link; the card position is clamped once its width is known */
  left: number;
  y: number;
  above: boolean;
}

/** Mounted only while a link is hovered, so lists without a preview need no query client. */
function PreviewCard({
  hover,
  preview,
  comment,
}: {
  hover: Hover;
  preview: NonNullable<CrossReferenceListProps["preview"]>;
  /** comment of the hovered link from the list; the texts response carries it as well */
  comment?: string;
}) {
  // first hover of any link of the verse fetches all of its texts, later hovers hit the cache
  const texts = useQuery(
    verseCrossReferenceTextsQuery(preview.bookId, preview.chapter, preview.verse, preview.level),
  );
  const entry = texts.data?.references.find((r) => keyOf(r) === hover.key);
  const md = useMediaQuery(PREVIEW_CARD_WIDE_QUERY);
  const x = cardLeft(hover.left, md && hasOriginals(entry?.verses));
  return (
    <PassagePreviewCard
      position={{ x, y: hover.y, above: hover.above }}
      verses={entry?.verses}
      error={texts.isError}
      loading={!texts.isError}
      comment={comment ?? entry?.comment}
    />
  );
}

/** TSK targets as links to the verse. Labels use the abbreviated book name, the screen reader label the full one. */
export function CrossReferenceList({
  references: all,
  books,
  variant,
  truncated,
  preview,
  showHidden = false,
  editable = false,
}: CrossReferenceListProps) {
  const { t, i18n } = useTranslation();
  const [active, setActive] = useState(0);
  const listRef = useRef<HTMLUListElement>(null);
  const [hover, setHover] = useState<Hover | null>(null);
  const [editing, setEditing] = useState<{
    reference: CrossReference;
    position: Hover;
    pen: HTMLElement;
  } | null>(null);
  const references = showHidden ? all : all.filter((r) => r.rating !== "hide");
  const hovered = hover && references.find((r) => keyOf(r) === hover.key);
  const show = (r: CrossReference, element: HTMLElement) => {
    // the texts endpoint leaves hidden links out, so they have nothing to preview
    if (!preview || r.rating === "hide") return;
    const rect = element.getClientRects()[0] ?? element.getBoundingClientRect();
    const above = rect.bottom + 160 > window.innerHeight && rect.top > window.innerHeight / 2;
    setHover({
      key: keyOf(r),
      left: rect.left,
      y: above ? rect.top - CARD_GAP : rect.bottom + CARD_GAP,
      above,
    });
  };
  const hide = () => setHover(null);

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

  /** Rating for screen readers (the star and the grey are visual only). */
  const state = (r: CrossReference) =>
    r.rating === "highlight"
      ? ` (${t("bible.tskCuration.highlighted")})`
      : r.rating === "hide"
        ? ` (${t("bible.tskCuration.hidden")})`
        : "";
  /** Visible label: a star in front of a highlighted link. */
  const content = (r: CrossReference) => (
    <>
      {r.rating === "highlight" && <StarIcon />}
      {label(r, false)}
      {state(r) && <span className="sr-only">{state(r)}</span>}
    </>
  );

  const openEditor = (r: CrossReference, pen: HTMLElement) => {
    setHover(null);
    const rect = pen.getBoundingClientRect();
    const above = rect.bottom + 320 > window.innerHeight && rect.top > window.innerHeight / 2;
    setEditing({
      reference: r,
      pen,
      position: {
        key: keyOf(r),
        left: Math.max(
          CARD_GAP,
          Math.min(rect.left, window.innerWidth - CROSS_REFERENCE_EDITOR_WIDTH - CARD_GAP),
        ),
        y: above ? rect.top - CARD_GAP : rect.bottom + CARD_GAP,
        above,
      },
    });
  };
  const closeEditor = () => {
    const pen = editing?.pen;
    setEditing(null);
    if (pen?.isConnected) pen.focus();
  };

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
              aria-label={`${label(r, true)}${state(r)}`}
              tabIndex={index === active ? 0 : -1}
              onFocus={(event) => {
                setActive(index);
                show(r, event.currentTarget);
              }}
              onBlur={hide}
              onMouseEnter={(event) => show(r, event.currentTarget)}
              onMouseLeave={hide}
              onKeyDown={(event) => event.key === "Escape" && hide()}
              className={linkClass(r)}
            >
              {content(r)}
            </Link>
            {editable && preview && (
              <Button
                variant="ghost"
                size="sm"
                className="ms-0.5 inline-flex px-1 py-0 align-middle text-fg-muted"
                aria-label={t("bible.tskCuration.edit", { reference: label(r, true) })}
                title={t("bible.tskCuration.edit", { reference: label(r, true) })}
                aria-haspopup="dialog"
                aria-expanded={editing !== null && keyOf(editing.reference) === keyOf(r)}
                onClick={(event) => openEditor(r, event.currentTarget)}
              >
                <PencilIcon />
              </Button>
            )}
            {index < references.length - 1 && (
              <span aria-hidden="true" className="ms-2 text-fg-muted">
                ·
              </span>
            )}
          </li>
        ))}
        {hover && preview && (
          <PreviewCard hover={hover} preview={preview} comment={hovered?.comment} />
        )}
        {editing && preview && (
          <li className="contents">
            <CrossReferenceEditor
              source={preview}
              reference={editing.reference}
              label={label(editing.reference, true)}
              position={{
                x: editing.position.left,
                y: editing.position.y,
                above: editing.position.above,
              }}
              onClose={closeEditor}
            />
          </li>
        )}
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
            <Link
              to={to(r)}
              onFocus={(event) => show(r, event.currentTarget)}
              onBlur={hide}
              onMouseEnter={(event) => show(r, event.currentTarget)}
              onMouseLeave={hide}
              onKeyDown={(event) => event.key === "Escape" && hide()}
              className={linkClass(r)}
            >
              {content(r)}
            </Link>
          </li>
        ))}
        {hover && preview && (
          <PreviewCard hover={hover} preview={preview} comment={hovered?.comment} />
        )}
      </ul>
      {truncated && (
        <p className="mt-1 text-xs text-fg-muted">{t("bible.wordDetail.crossReferencesMore")}</p>
      )}
    </details>
  );
}
