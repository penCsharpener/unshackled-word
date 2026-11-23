import { useQuery } from "@tanstack/react-query";
import {
  type DragEvent,
  type KeyboardEvent,
  type MouseEvent,
  type ReactNode,
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
   * `preview`), and "Querverweis hinzufügen" after the list opens it in `add` mode (roadmap 0024.4f). Maintainers only
   * for now; suggestions by other users follow in 0024.6f.
   */
  editable?: boolean;
  /**
   * Link mode (roadmap 0024.5f, `inline` only): a checkbox per link (click / Ctrl/Cmd-click toggles, Shift-click
   * selects a run), every link is draggable (native HTML5 drag and drop) onto a word of the source verse; the hover
   * card stays, but is suppressed while a drag is in progress. Also groups the links by word.
   */
  link?: CrossReferenceLinkMode;
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

/** One word group of the panel: links attached to the same words (`quote`), or the verse-level rest (`quote` absent). */
export interface CrossReferenceGroup {
  key: string;
  quote?: string;
  references: CrossReference[];
}

/**
 * Links grouped by their word (roadmap 0024.5f): one group per linked word range (ordered by position, API order
 * within), then the verse-level rest ("Ohne Wort"), which includes drifted links.
 */
export function groupByWord(references: CrossReference[]): CrossReferenceGroup[] {
  const groups = new Map<string, CrossReferenceGroup & { start: number; end: number }>();
  const rest: CrossReference[] = [];
  for (const r of references) {
    const w = r.word;
    if (w?.state !== "ok" || w.startPosition === undefined) {
      rest.push(r);
      continue;
    }
    const end = w.endPosition ?? w.startPosition;
    const key = `${w.startPosition}-${end}`;
    const group = groups.get(key);
    if (group) group.references.push(r);
    else
      groups.set(key, {
        key,
        quote: w.quotedText,
        references: [r],
        start: w.startPosition,
        end,
      });
  }
  const sorted = [...groups.values()]
    .sort((a, b) => a.start - b.start || a.end - b.end)
    .map(({ key, quote, references: refs }) => ({ key, quote, references: refs }));
  return rest.length > 0 ? [...sorted, { key: "none", references: rest }] : sorted;
}

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
  link,
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
  /** Word groups (inline): in link mode, and whenever a link of the verse has a word (decision 0024.5f). */
  const groups =
    variant === "inline" && (link || visible.some((r) => r.word))
      ? groupByWord(visible)
      : undefined;
  const references = groups ? groups.flatMap((g) => g.references) : visible;
  const hovered = hover && references.find((r) => keyOf(r) === hover.key);
  /** Last plainly clicked checkbox: the other end of a Shift-click run. */
  const selectAnchor = useRef<string>(undefined);
  const show = (r: CrossReference, element: HTMLElement) => {
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
    const select = (r: CrossReference, event: MouseEvent<HTMLInputElement>) => {
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
          {link && (
            <input
              type="checkbox"
              checked={link.selected.has(key)}
              readOnly
              onClick={(event) => select(r, event)}
              aria-label={t("bible.tskLink.select", { reference: label(r, true) })}
              className="me-1 size-3.5 align-middle accent-accent"
            />
          )}
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
            className={linkClass(r)}
          >
            {content(r)}
          </Link>
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
          {editable && preview && (
            <Button
              variant="ghost"
              size="sm"
              className="ms-0.5 inline-flex px-1 py-0 align-middle text-fg-muted"
              aria-label={t("bible.tskCuration.edit", { reference: label(r, true) })}
              title={t("bible.tskCuration.edit", { reference: label(r, true) })}
              aria-haspopup="dialog"
              aria-expanded={editing?.reference != null && keyOf(editing.reference) === key}
              onClick={(event) => openEditor(r, event.currentTarget)}
            >
              <PencilIcon />
            </Button>
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

    if (groups) {
      let offset = 0;
      return (
        // biome-ignore lint/a11y/noStaticElementInteractions: delegated arrow-key navigation between the links of the groups
        <div
          ref={listRef}
          onKeyDown={move}
          className="flex flex-col gap-1 font-sans text-sm"
          data-testid="cross-reference-groups"
        >
          {groups.map((group) => {
            const start = offset;
            offset += group.references.length;
            const heading = group.quote
              ? t("bible.tskLink.group", { quote: group.quote })
              : t("bible.tskLink.noWord");
            return (
              <fieldset
                key={group.key}
                className="m-0 min-w-0 border-0 p-0"
                aria-label={
                  group.quote
                    ? t("bible.tskLink.groupLabel", { quote: group.quote })
                    : t("bible.tskLink.noWordLabel")
                }
                data-word-group={group.quote ?? ""}
              >
                <legend aria-hidden="true" className="p-0 text-xs text-fg-muted">
                  {heading}
                </legend>
                <ul className="flex flex-wrap gap-x-2 gap-y-1">
                  {group.references.map((r, i) =>
                    item(r, start + i, i === group.references.length - 1),
                  )}
                </ul>
              </fieldset>
            );
          })}
          {addButton && <div>{addButton}</div>}
          <ul className="contents">{overlays}</ul>
        </div>
      );
    }

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
