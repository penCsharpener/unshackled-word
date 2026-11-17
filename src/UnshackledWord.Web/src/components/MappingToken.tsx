import type { DragEvent, ReactNode } from "react";
import type { NodeKey, Side, WordStatus } from "@/lib/bible/mappingEditor";
import { Button } from "./Button";

export interface MappingTokenProps {
  node: NodeKey;
  side: Side;
  text: string;
  /** group number, `+n` (added word of group n), `?` (German word without original) or `–` (original without German) */
  badge: string;
  status: WordStatus;
  /** German word added by the translator (italic) */
  added?: boolean;
  /** This tile is one part of a split compound word (`Gersten|ernte`); the parts of a word are drawn joined */
  part?: { order: number; count: number };
  unmapped?: boolean;
  /** Every link of this German tile is a `unit` (roadmap 0018: belongs to the head's form, not counted); drawn muted */
  unit?: boolean;
  /** title of the over-assignment hint; the badge shows a `!` when set */
  warning?: string;
  /** short note under the word asking for a second look (revision carry-over: changed or new word, original without link) */
  review?: string;
  /** in the group that is hovered or selected */
  active?: boolean;
  selected?: boolean;
  /** drop target while a valid drag hovers it */
  dropTarget?: boolean;
  dragging?: boolean;
  draggable?: boolean;
  /** accessible name: word, its connections and, for originals, the grammar */
  label: string;
  lang?: string;
  /** dictionary form of an original word, shown under it */
  lemma?: string;
  /** Strong's, morphology and gloss; only drawn when `showDetails` */
  details?: ReactNode;
  showDetails?: boolean;
  onSelect: (node: NodeKey) => void;
  onHover: (node: NodeKey | null) => void;
  onDragStart?: (node: NodeKey, event: DragEvent<HTMLElement>) => void;
  onDragOver?: (node: NodeKey, event: DragEvent<HTMLElement>) => void;
  onDragLeave?: (node: NodeKey) => void;
  onDrop?: (node: NodeKey, event: DragEvent<HTMLElement>) => void;
  onDragEnd?: () => void;
}

/** One word tile of the mapping editor (German or original) with its group badge; click/tap selects, drag connects. */
export function MappingToken({
  node,
  side,
  text,
  badge,
  status,
  added,
  part,
  unmapped,
  unit,
  warning,
  review,
  active,
  selected,
  dropTarget,
  dragging,
  draggable,
  label,
  lang,
  lemma,
  details,
  showDetails,
  onSelect,
  onHover,
  onDragStart,
  onDragOver,
  onDragLeave,
  onDrop,
  onDragEnd,
}: MappingTokenProps) {
  return (
    <Button
      variant="inline"
      draggable={draggable}
      aria-pressed={selected}
      aria-label={label}
      data-node={node}
      data-side={side}
      data-status={status}
      data-added={added ? "" : undefined}
      data-part={
        part
          ? part.order === 1
            ? "first"
            : part.order === part.count
              ? "last"
              : "middle"
          : undefined
      }
      data-unmapped={unmapped ? "" : undefined}
      data-unit={unit ? "" : undefined}
      data-review={review ? "" : undefined}
      data-active={active ? "" : undefined}
      data-selected={selected ? "" : undefined}
      data-drop={dropTarget ? "" : undefined}
      data-dragging={dragging ? "" : undefined}
      className="map-tok"
      onClick={() => onSelect(node)}
      onMouseEnter={() => onHover(node)}
      onMouseLeave={() => onHover(null)}
      onFocus={() => onHover(node)}
      onBlur={() => onHover(null)}
      onDragStart={(event) => onDragStart?.(node, event)}
      onDragOver={(event) => onDragOver?.(node, event)}
      onDragLeave={() => onDragLeave?.(node)}
      onDrop={(event) => onDrop?.(node, event)}
      onDragEnd={() => onDragEnd?.()}
    >
      <span className="map-badge" aria-hidden="true">
        {badge}
        {warning && (
          <span className="map-warn" title={warning}>
            !
          </span>
        )}
      </span>
      <span className="map-tok-text" lang={lang}>
        {text}
      </span>
      {review && (
        <span className="map-tok-sub map-tok-review" aria-hidden="true">
          {review}
        </span>
      )}
      {lemma && (
        <span className="map-tok-sub map-tok-lemma" lang={lang} aria-hidden="true">
          {lemma}
        </span>
      )}
      {showDetails && details && (
        <span className="map-tok-sub map-tok-details" dir="ltr" aria-hidden="true">
          {details}
        </span>
      )}
    </Button>
  );
}
