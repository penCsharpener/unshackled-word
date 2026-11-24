import { useQuery } from "@tanstack/react-query";
import {
  type DragEvent,
  type KeyboardEvent,
  type MouseEvent,
  type ReactNode,
  useEffect,
  useRef,
  useState,
} from "react";
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
import { StatusBadge } from "./StatusBadge";

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
   * Edit mode, `inline` with `preview` only: clicking a link opens `CrossReferenceEditor` for it instead of navigating
   * (source verse = `preview`), and "Querverweis hinzufügen" after the list opens it in `add` mode (roadmap 0024.4f).
   * Every logged-in user; see `suggest`.
   */
  editable?: boolean;
  /**
   * With `editable`, logged-in non-maintainers (roadmap 0024.6f): the editor sends suggestions ("Vorschlag senden").
   * Independent of it, links with the caller's `pendingProposal` get the badge "Vorschlag offen".
   */
  suggest?: boolean;
  /**
   * Link mode (roadmap 0024.5f, `inline` only): a click on a pill selects it instead of opening the verse (click /
   * Ctrl/Cmd-click toggles, Shift-click selects a run; hover still shows the verse text), every link is draggable (native HTML5 drag and drop) onto a word of the source verse; the hover
   * card stays, but is suppressed while a drag is in progress.
   */
  link?: CrossReferenceLinkMode;
  /** Keys (`crossReferenceKey`) of links that flash because their word is hovered in the verse text. */
  flashKeys?: ReadonlySet<string>;
  /** A link is hovered or focused (`undefined` = none): the reader marks its word in the verse text. */
  onHoverReference?: (reference: CrossReference | undefined) => void;
}

/** Link mode state, owned by the reader (selection and drag span the panel and the verse text). */
export interface CrossReferenceLinkMode {
  /** Keys (`crossReferenceKey`) of the selected links of this verse. */
  selected: ReadonlySet<string>;
  /** New selection of this verse, in list order. */
  onSelectedChange: (keys: string[]) => void;
  /** A drag starts: the dragged link, or the whole selection when the dragged link is part of it. */
  onDragStart: (references: CrossReference[]) => void;
  onDragEnd: () => void;
  /** Some drag is in progress (no hover card meanwhile). */
  dragging: boolean;
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
/** Stable key of a link within its verse (selection state of the link mode). */
export const crossReferenceKey = keyOf;

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

/** Classes of a link by its rating: highlighted bold, hidden (curate mode) greyed and struck through. */
const PILL =
  "inline-block rounded-full border border-border px-2 py-0.5 no-underline hover:bg-surface-muted";
/**
 * Background of a pill: linked to a word of the translation (teal, like the dotted underline of the word) or
 * highlighted (amber), a little stronger while it flashes for its hovered word.
 */
const pillBackground = (r: CrossReference, flash: boolean) =>
  flash
    ? "bg-mark-linked ring-2 ring-mark-linked"
    : r.word?.state === "ok"
      ? "bg-mark-link/40"
      : r.rating === "highlight"
        ? "bg-mark-linked/30"
        : "";
const linkClass = (r: CrossReference, flash = false, selected = false) =>
  `${PILL} ${
    selected
      ? "bg-accent font-medium text-accent-fg ring-2 ring-accent"
      : `${pillBackground(r, flash)} ${
          r.rating === "hide"
            ? "text-fg-muted line-through"
            : r.rating === "highlight"
              ? "font-medium text-accent"
              : "text-accent"
        }`
  }`;

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
  // Loaded, but without this link (cached before it was added or changed): fetch once more, never spin forever.
  const missing = texts.isSuccess && !entry;
  const retried = useRef<string>(undefined);
  const { refetch } = texts;
  useEffect(() => {
    if (!missing || texts.isFetching || retried.current === hover.key) return;
    retried.current = hover.key;
    void refetch();
  }, [missing, texts.isFetching, hover.key, refetch]);
  const md = useMediaQuery(PREVIEW_CARD_WIDE_QUERY);
  const x = cardLeft(hover.left, md && hasOriginals(entry?.verses));
  return (
    <PassagePreviewCard
      position={{ x, y: hover.y, above: hover.above }}
      verses={entry?.verses}
      error={texts.isError || (missing && retried.current === hover.key && !texts.isFetching)}
      loading={!texts.isError && !(missing && retried.current === hover.key && !texts.isFetching)}
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
  suggest = false,
  link,
  flashKeys,
  onHoverReference,
}: CrossReferenceListProps) {
  const { t, i18n } = useTranslation();
  const [active, setActive] = useState(0);
  const listRef = useRef<HTMLUListElement & HTMLDivElement>(null);
  const [hover, setHover] = useState<Hover | null>(null);
  /** Open editor: one link (`reference`) or the add form (`reference` null); `anchor` gets the focus back. */
  const [editing, setEditing] = useState<{
    reference: CrossReference | null;
    position: Hover;
    anchor: HTMLElement;
  } | null>(null);
  const visible = showHidden ? all : all.filter((r) => r.rating !== "hide");
  const references = visible;
  const hovered = hover && references.find((r) => keyOf(r) === hover.key);
  /** Last plainly clicked checkbox: the other end of a Shift-click run. */
  const selectAnchor = useRef<string>(undefined);
  const show = (r: CrossReference, element: HTMLElement) => {
    onHoverReference?.(r);
    // the texts endpoint leaves hidden links out, so they have nothing to preview; no card while dragging
    if (!preview || r.rating === "hide" || link?.dragging) return;
    const rect = element.getClientRects()[0] ?? element.getBoundingClientRect();
    const above = rect.bottom + 160 > window.innerHeight && rect.top > window.innerHeight / 2;
    setHover({
      key: keyOf(r),
      left: rect.left,
      y: above ? rect.top - CARD_GAP : rect.bottom + CARD_GAP,
      above,
    });
  };
  const hide = () => {
    setHover(null);
    onHoverReference?.(undefined);
  };

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

  /** Rating and origin for screen readers (the star, the "+" and the grey are visual only). */
  const state = (r: CrossReference) =>
    (r.rating === "highlight"
      ? ` (${t("bible.tskCuration.highlighted")})`
      : r.rating === "hide"
        ? ` (${t("bible.tskCuration.hidden")})`
        : "") + (r.origin === "added" ? ` (${t("bible.tskCuration.added")})` : "");
  /** Visible label: a star in front of a highlighted link, a "+" in front of an added one. */
  const content = (r: CrossReference) => (
    <>
      {r.rating === "highlight" && <StarIcon />}
      {r.origin === "added" && (
        <span aria-hidden="true" data-testid="added-marker" className="me-0.5 font-medium">
          +
        </span>
      )}
      {label(r, false)}
      {state(r) && <span className="sr-only">{state(r)}</span>}
    </>
  );

  const openEditor = (r: CrossReference | null, anchor: HTMLElement) => {
    setHover(null);
    const rect = anchor.getBoundingClientRect();
    const above = rect.bottom + 320 > window.innerHeight && rect.top > window.innerHeight / 2;
    setEditing({
      reference: r,
      anchor,
      position: {
        key: r ? keyOf(r) : "add",
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
    const anchor = editing?.anchor;
    setEditing(null);
    if (anchor?.isConnected) anchor.focus();
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
    const select = (r: CrossReference, event: MouseEvent) => {
      if (!link) return;
      const order = references.map(keyOf);
      const key = keyOf(r);
      const next = new Set(link.selected);
      const from = selectAnchor.current ? order.indexOf(selectAnchor.current) : -1;
      if (event.shiftKey && from >= 0) {
        const to = order.indexOf(key);
        for (let i = Math.min(from, to); i <= Math.max(from, to); i++) next.add(order[i] as string);
      } else {
        // plain and Ctrl/Cmd-click toggle one link
        if (next.has(key)) next.delete(key);
        else next.add(key);
        selectAnchor.current = key;
      }
      link.onSelectedChange(order.filter((k) => next.has(k)));
    };
    const item = (r: CrossReference, index: number, last: boolean) => {
      const key = keyOf(r);
      return (
        <li
          key={key}
          {...(link && {
            draggable: true,
            "data-link-draggable": "",
            onDragStart: (event: DragEvent<HTMLLIElement>) => {
              const dragged = link.selected.has(key)
                ? references.filter((x) => link.selected.has(keyOf(x)))
                : [r];
              try {
                event.dataTransfer.effectAllowed = "link";
                event.dataTransfer.setData(
                  "text/plain",
                  dragged.map((x) => label(x, false)).join("; "),
                );
              } catch {
                // no data transfer (tests): the reader keeps the dragged links in its own state
              }
              hide();
              link.onDragStart(dragged);
            },
            onDragEnd: () => link.onDragEnd(),
          })}
        >
          <Link
            to={to(r)}
            draggable={link ? false : undefined}
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
            {...(link && {
              "aria-current": link.selected.has(key) ? ("true" as const) : undefined,
              onClick: (event: MouseEvent<HTMLAnchorElement>) => {
                // link mode: a click selects the pill instead of opening the verse
                event.preventDefault();
                select(r, event);
              },
            })}
            {...(!link &&
              editable &&
              preview && {
                "aria-haspopup": "dialog" as const,
                "aria-expanded": editing?.reference != null && keyOf(editing.reference) === key,
                title: t("bible.tskCuration.edit", { reference: label(r, true) }),
                onClick: (event: MouseEvent<HTMLAnchorElement>) => {
                  event.preventDefault();
                  openEditor(r, event.currentTarget);
                },
              })}
            className={linkClass(r, flashKeys?.has(key), link?.selected.has(key))}
          >
            {content(r)}
          </Link>
          {r.pendingProposal && (
            <span className="ms-1 align-middle" data-testid="pending-proposal">
              <StatusBadge tone="info">{t("bible.tskCuration.proposal.pending")}</StatusBadge>
            </span>
          )}
          {r.word?.state === "drifted" && (
            <span
              className="ms-1 text-xs text-fg-muted"
              title={t("bible.tskLink.driftedTitle")}
              data-testid="drifted-word"
            >
              {t("bible.tskLink.group", { quote: r.word.quotedText })} ({t("bible.tskLink.drifted")}
              )
            </span>
          )}
          {!last && (
            <span aria-hidden="true" className="ms-2 text-fg-muted">
              ·
            </span>
          )}
        </li>
      );
    };
    const addButton = editable && preview && (
      <Button
        variant="ghost"
        size="sm"
        className="px-1 py-0 text-xs text-fg-muted"
        aria-haspopup="dialog"
        aria-expanded={editing !== null && editing.reference === null}
        onClick={(event) => openEditor(null, event.currentTarget)}
      >
        {t("bible.tskCuration.add.action")}
      </Button>
    );
    const overlays: ReactNode = (
      <>
        {hover && preview && !link?.dragging && (
          <PreviewCard hover={hover} preview={preview} comment={hovered?.comment} />
        )}
        {editing && preview && (
          <li className="contents">
            {editing.reference ? (
              <CrossReferenceEditor
                source={preview}
                reference={editing.reference}
                label={label(editing.reference, true)}
                suggest={suggest}
                position={{
                  x: editing.position.left,
                  y: editing.position.y,
                  above: editing.position.above,
                }}
                onClose={closeEditor}
              />
            ) : (
              <CrossReferenceEditor
                mode="add"
                source={preview}
                suggest={suggest}
                level={preview.level}
                position={{
                  x: editing.position.left,
                  y: editing.position.y,
                  above: editing.position.above,
                }}
                onClose={closeEditor}
              />
            )}
          </li>
        )}
      </>
    );

    return (
      <ul
        ref={listRef}
        onKeyDown={move}
        className="flex flex-wrap gap-x-2 gap-y-1 font-sans text-sm"
      >
        {references.map((r, index) => item(r, index, index === references.length - 1))}
        {addButton && <li>{addButton}</li>}
        {overlays}
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
