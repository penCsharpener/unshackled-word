import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router";
import { passageTextQuery } from "@/lib/bible/queries";
import { passageSpansParam } from "@/lib/bible/referenceParse";
import type { DefinitionSegment } from "@/lib/bible/types";
import { normalizeStrongs } from "@/lib/bible/wordDetail";
import { useCoarsePointer } from "@/lib/useCoarsePointer";
import { PassagePreviewCard } from "./PassagePreviewCard";

const STRONGS_IN_TEXT = /\b([GH]\d{1,4}[A-Za-z]?)\b/g;
const CARD_WIDTH = 320;
const CARD_GAP = 8;

/** A Strong's number as a link to its page; plain text when it is not a valid number. */
export function StrongsLink({ value }: { value: string }) {
  const strongs = normalizeStrongs(value);
  if (!strongs) return <>{value}</>;
  return (
    <Link to={`/bible/strongs/${strongs}`} className="text-accent underline">
      {value}
    </Link>
  );
}

/** Plain text with every Strong's number in it (e.g. "H7225H") turned into a link. */
function LinkedStrongs({ text }: { text: string }) {
  return text.split(STRONGS_IN_TEXT).map((part, index) =>
    index % 2 === 1 ? (
      // biome-ignore lint/suspicious/noArrayIndexKey: static split of one string
      <StrongsLink key={index} value={part} />
    ) : (
      part
    ),
  );
}

interface Hover {
  x: number;
  y: number;
  above: boolean;
}

type Ref = NonNullable<DefinitionSegment["ref"]>;

/** Mounted only while a reference is hovered or focused, so the text is fetched on demand. */
function ReferencePreview({
  segment,
  spans,
  hover,
}: {
  segment: string;
  spans: string;
  hover: Hover;
}) {
  const passage = useQuery(passageTextQuery(spans));
  const verses = passage.data?.passages.flatMap((p) => p.verses);
  // portal to <body>: a transformed ancestor (popover) would otherwise become the containing block of `fixed`
  return createPortal(
    <PassagePreviewCard
      position={hover}
      title={segment.trim()}
      verses={verses && verses.length > 0 ? verses : undefined}
      error={passage.isError}
      loading={!passage.isError}
    />,
    document.body,
  );
}

function ReferenceLink({ text, reference }: { text: string; reference: Ref }) {
  const coarse = useCoarsePointer();
  const [hover, setHover] = useState<Hover | null>(null);
  const spans = passageSpansParam({
    bookId: reference.bookId,
    bookOnly: false,
    display: text,
    spans: reference.spans,
    groups: [],
  });
  const canPreview = !coarse && spans !== undefined;
  const show = (element: HTMLElement, pointer?: { x: number; y: number }) => {
    if (!canPreview) return;
    // a link wrapped over several lines has one box per line; its bounding box would put the card far away
    const lines = Array.from(element.getClientRects());
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
      element.getBoundingClientRect();
    const above = rect.bottom + 160 > window.innerHeight && rect.top > window.innerHeight / 2;
    setHover({
      x: Math.max(CARD_GAP, Math.min(rect.left, window.innerWidth - CARD_WIDTH - CARD_GAP)),
      y: above ? rect.top - CARD_GAP : rect.bottom + CARD_GAP,
      above,
    });
  };
  const hide = () => setHover(null);
  return (
    <>
      <Link
        to={reference.path}
        onMouseEnter={(event) => show(event.currentTarget, { x: event.clientX, y: event.clientY })}
        onMouseLeave={hide}
        onFocus={(event) => show(event.currentTarget)}
        onBlur={hide}
        onKeyDown={(event) => event.key === "Escape" && hide()}
        className="text-accent underline"
      >
        {text}
      </Link>
      {hover && spans && <ReferencePreview segment={text} spans={spans} hover={hover} />}
    </>
  );
}

export interface ScriptureTextProps {
  /** The plain lexicon text; shown (with Strong's links) when `segments` is absent. */
  text: string;
  /** `definitionSegments` from the API: plain pieces and recognised scripture references. */
  segments?: DefinitionSegment[];
}

/**
 * Lexicon text with its links: Strong's numbers go to their page, scripture references to the reader
 * (`path`) with a text preview on hover and focus (not on touch devices, where a tap follows the link).
 */
export function ScriptureText({ text, segments }: ScriptureTextProps) {
  if (!segments || segments.length === 0) return <LinkedStrongs text={text} />;
  return segments.map((segment, index) =>
    segment.ref ? (
      // biome-ignore lint/suspicious/noArrayIndexKey: static list from one response
      <ReferenceLink key={index} text={segment.text} reference={segment.ref} />
    ) : (
      // biome-ignore lint/suspicious/noArrayIndexKey: static list from one response
      <LinkedStrongs key={index} text={segment.text} />
    ),
  );
}
