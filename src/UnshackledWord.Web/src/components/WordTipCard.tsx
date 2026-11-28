/** Card width in px (`w-80`). */
export const WORD_TIP_WIDTH = 320;
const GAP = 8;
/** Room the card may need below its anchor before it flips above. */
const ROOM_BELOW = 240;

export interface WordTipPosition {
  x: number;
  y: number;
  /** `y` is the card's bottom edge (the card sits above its anchor). */
  above: boolean;
}

/**
 * Where the card goes for an anchor box: under it (above when the room below is short and there is more room above), its left edge on the
 * anchor's left edge but kept inside the viewport.
 */
export function wordTipPosition(
  rect: { left: number; top: number; bottom: number },
  viewport: { width: number; height: number },
): WordTipPosition {
  const above = rect.bottom + ROOM_BELOW > viewport.height && rect.top > viewport.height / 2;
  return {
    x: Math.max(GAP, Math.min(rect.left, viewport.width - WORD_TIP_WIDTH - GAP)),
    y: above ? rect.top - GAP : rect.bottom + GAP,
    above,
  };
}

export interface WordTipCardProps {
  /** Id for the anchor's `aria-describedby`. */
  id: string;
  position: WordTipPosition;
  /** The original word, shown large. */
  original: string;
  /** `he`, `arc` or `grc`; Hebrew/Aramaic run right to left inside the left-aligned card. */
  lang: string;
  /** Muted line under the word, e.g. "H1961 · to be · Verb · Qal". */
  head?: string;
  /** Mapped German words, shown after `germanLabel` ("Deutsch"). */
  german?: string;
  germanLabel: string;
  /** Lexicon note, plain text (line breaks kept), clamped to five lines. */
  note?: string;
}

/** Hover/focus card for one original word (LTR, left-aligned, top layer). Presentational: the caller owns position and visibility. */
export function WordTipCard({
  id,
  position,
  original,
  lang,
  head,
  german,
  germanLabel,
  note,
}: WordTipCardProps) {
  const rtl = lang !== "grc";
  return (
    <div
      id={id}
      role="tooltip"
      popover="manual"
      dir="ltr"
      ref={(element) => {
        // measured pixel position: the only value that cannot be a design token
        element?.style.setProperty("left", `${position.x}px`);
        element?.style.setProperty("top", `${position.y}px`);
        try {
          if (element && !element.matches(":popover-open")) element.showPopover();
        } catch {
          // no popover support (jsdom, old browsers): the plain fixed card is shown
        }
      }}
      className={`pointer-events-none fixed inset-auto z-50 m-0 h-fit w-80 overflow-visible rounded-lg border border-border bg-surface p-3 text-start font-sans text-sm text-fg shadow-lg ${position.above ? "-translate-y-full" : ""}`}
    >
      <p className="mb-1">
        <span lang={lang} dir={rtl ? "rtl" : "ltr"} className="font-original text-3xl leading-snug">
          {original}
        </span>
      </p>
      {head ? <p className="text-fg-muted">{head}</p> : null}
      {german ? (
        <p className="mt-2">
          <span className="font-medium">{germanLabel}:</span> {german}
        </p>
      ) : null}
      {note ? (
        <p
          data-testid="word-tip-note"
          className="mt-2 line-clamp-5 whitespace-pre-line border-t border-border pt-2 text-fg-muted"
        >
          {note}
        </p>
      ) : null}
    </div>
  );
}
