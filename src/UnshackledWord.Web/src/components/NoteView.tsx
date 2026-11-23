import "@mdxeditor/editor/style.css";
import { headingsPlugin, linkPlugin, listsPlugin, MDXEditor, quotePlugin } from "@mdxeditor/editor";
import { useQuery } from "@tanstack/react-query";
import { type FocusEvent, type MouseEvent, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { passageTextQuery } from "@/lib/bible/queries";
import { formatStamp } from "@/lib/format";
import { classifyHref, dropUnsafeLinks, passageSpansOfPath } from "@/lib/notes/bibleLink";
import { type NoteStatus, noteStatusBadges, noteStatusReason } from "@/lib/notes/display";
import { mdxTranslation } from "@/lib/notes/mdxTranslation";
import { useCoarsePointer } from "@/lib/useCoarsePointer";
import { PassagePreviewCard } from "./PassagePreviewCard";
import { StatusBadge } from "./StatusBadge";

export interface NoteViewProps {
  /** the body with scripture references already turned into `/bible/…` links (`bodyLinked` of the API) */
  markdown: string;
  authorDisplayName: string;
  /** ISO timestamps */
  createdAt: string;
  updatedAt?: string;
  /** own notes: visibility badge (public with its review state), "ausgeblendet" and the maintainer's reason */
  status?: NoteStatus;
}

interface Hover {
  path: string;
  title: string;
  x: number;
  y: number;
  above: boolean;
}

const CARD_WIDTH = 320;
const CARD_GAP = 8;

/** Mounted only while a reference is hovered, so the text is fetched on demand. */
function LinkPreview({ hover }: { hover: Hover }) {
  const spans = passageSpansOfPath(hover.path) ?? "";
  const passage = useQuery({ ...passageTextQuery(spans), enabled: spans !== "" });
  const verses = passage.data?.passages.flatMap((p) => p.verses);
  return createPortal(
    <PassagePreviewCard
      position={hover}
      title={hover.title}
      verses={verses && verses.length > 0 ? verses : undefined}
      error={passage.isError}
      loading={!passage.isError}
    />,
    document.body,
  );
}

/** Sets target and rel on external links; the editor renders its anchors after mount, so this runs on every change. */
function decorateLinks(root: HTMLElement) {
  for (const anchor of root.querySelectorAll("a[href]")) {
    if (classifyHref(anchor.getAttribute("href")) === "external") {
      anchor.setAttribute("target", "_blank");
      anchor.setAttribute("rel", "noopener noreferrer");
    }
  }
}

/**
 * Read-only rendering of one note (MDXEditor in read-only mode, lazy-loaded chunk) with author and date.
 * `/bible/…` links navigate inside the SPA and show a passage preview on hover (not on touch devices),
 * http(s) links open in a new tab, links with any other scheme are shown as plain text.
 */
export function NoteView({
  markdown,
  authorDisplayName,
  createdAt,
  updatedAt,
  status,
}: NoteViewProps) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const coarse = useCoarsePointer();
  const plugins = useMemo(() => [headingsPlugin(), listsPlugin(), quotePlugin(), linkPlugin()], []);
  const safeMarkdown = useMemo(() => dropUnsafeLinks(markdown), [markdown]);
  const stamp = formatStamp(updatedAt ?? createdAt, i18n.language);
  const body = useRef<HTMLDivElement>(null);
  const [hover, setHover] = useState<Hover | null>(null);
  const reason = status ? noteStatusReason(status) : undefined;

  // biome-ignore lint/correctness/useExhaustiveDependencies: the observer must restart when the body is replaced
  useEffect(() => {
    const root = body.current;
    if (!root) return;
    decorateLinks(root);
    const observer = new MutationObserver(() => decorateLinks(root));
    observer.observe(root, { childList: true, subtree: true });
    return () => observer.disconnect();
  }, [safeMarkdown]);

  const anchorOf = (event: MouseEvent | FocusEvent) =>
    event.target instanceof Element ? event.target.closest("a[href]") : null;

  const onClick = (event: MouseEvent) => {
    const anchor = anchorOf(event);
    if (!anchor) return;
    const href = anchor.getAttribute("href");
    const kind = classifyHref(href);
    if (kind === "bible" && href) {
      // plain click only: modifier clicks keep the browser's "open in new tab"
      if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)
        return;
      event.preventDefault();
      setHover(null);
      navigate(href);
    } else if (kind === "unsafe") {
      event.preventDefault();
    }
  };

  const showPreview = (anchor: Element, pointer?: { x: number; y: number }) => {
    const href = anchor.getAttribute("href");
    if (coarse || !href || classifyHref(href) !== "bible" || !passageSpansOfPath(href)) return;
    // a link wrapped over several lines has one box per line; the one under the pointer places the card
    const lines = Array.from(anchor.getClientRects());
    const rect =
      (pointer &&
        lines.find(
          (r) =>
            pointer.x >= r.left &&
            pointer.x <= r.right &&
            pointer.y >= r.top &&
            pointer.y <= r.bottom,
        )) ||
      lines[0] ||
      anchor.getBoundingClientRect();
    const above = rect.bottom + 160 > window.innerHeight && rect.top > window.innerHeight / 2;
    setHover({
      path: href,
      title: (anchor.textContent ?? "").trim(),
      x: Math.max(CARD_GAP, Math.min(rect.left, window.innerWidth - CARD_WIDTH - CARD_GAP)),
      y: above ? rect.top - CARD_GAP : rect.bottom + CARD_GAP,
      above,
    });
  };

  const onMouseOver = (event: MouseEvent) => {
    const anchor = anchorOf(event);
    if (anchor) showPreview(anchor, { x: event.clientX, y: event.clientY });
  };

  const onFocus = (event: FocusEvent) => {
    const anchor = anchorOf(event);
    if (anchor) showPreview(anchor);
  };

  const onLeave = (event: MouseEvent | FocusEvent) => {
    if (anchorOf(event)) setHover(null);
  };

  return (
    <div className="flex flex-col gap-1 font-sans text-sm">
      {/* biome-ignore lint/a11y/noStaticElementInteractions: delegated handler for the anchors the editor renders; the anchors themselves are focusable links */}
      <div
        ref={body}
        onClick={onClick}
        onMouseOver={onMouseOver}
        onMouseOut={onLeave}
        onFocus={onFocus}
        onBlur={onLeave}
        onKeyDown={(event) => event.key === "Escape" && setHover(null)}
      >
        {/* the editor only reads `markdown` on mount, so a changed body remounts it */}
        <MDXEditor
          key={safeMarkdown}
          markdown={safeMarkdown}
          readOnly
          plugins={plugins}
          translation={mdxTranslation}
          suppressHtmlProcessing
          className="note-md"
          contentEditableClassName="note-md-content"
        />
      </div>
      {hover && <LinkPreview hover={hover} />}
      {status && (
        <div className="flex flex-wrap items-center gap-1">
          {noteStatusBadges(status).map((badge) => (
            <StatusBadge key={badge.key} tone={badge.tone}>
              {t(badge.key)}
            </StatusBadge>
          ))}
        </div>
      )}
      {reason && <p className="text-xs text-fg-muted">{t(reason.key, { reason: reason.text })}</p>}
      <p className="text-xs text-fg-muted">
        {t(updatedAt ? "notes.authorEdited" : "notes.author", {
          author: authorDisplayName,
          date: stamp,
        })}
      </p>
    </div>
  );
}

export default NoteView;
