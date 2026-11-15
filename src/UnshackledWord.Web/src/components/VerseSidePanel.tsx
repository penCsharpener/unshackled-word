import { type ReactNode, useEffect, useLayoutEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "./Button";

export interface VerseSidePanelProps {
  /** `tsk` lists clip at the verse height with a "+N" overlay; `original` grows the verse row instead and is never clipped. */
  mode: "tsk" | "original";
  /** accessible name, e.g. "Querverweise zu Vers 16" */
  label: string;
  /** `tsk` only: the clipped list is shown in full as an overlay */
  expanded?: boolean;
  onExpandedChange?: (expanded: boolean) => void;
  children: ReactNode;
}

/** One ResizeObserver for all panels of a chapter (up to 176 verses). */
const callbacks = new WeakMap<Element, () => void>();
let observer: ResizeObserver | undefined;

function observe(element: Element, callback: () => void): () => void {
  if (typeof ResizeObserver === "undefined") return () => {};
  observer ??= new ResizeObserver((entries) => {
    for (const entry of entries) callbacks.get(entry.target)?.();
  });
  callbacks.set(element, callback);
  observer.observe(element);
  return () => {
    observer?.unobserve(element);
    callbacks.delete(element);
  };
}

/**
 * Panel next to one verse in the left margin (from 120rem, placed by the `.verse-side` CSS). The verse row is a grid
 * with the panel in the same cell, so the panel lines up with its verse; in `original` mode the row is as tall as
 * the taller of the two.
 */
export function VerseSidePanel({
  mode,
  label,
  expanded = false,
  onExpandedChange,
  children,
}: VerseSidePanelProps) {
  const { t } = useTranslation();
  const rootRef = useRef<HTMLDivElement>(null);
  const clipRef = useRef<HTMLDivElement>(null);
  const moreRef = useRef<HTMLButtonElement>(null);
  const [hidden, setHidden] = useState(0);
  const clipped = mode === "tsk";

  // Counts the references that do not fit whole lines into the verse height and cuts the list after the last
  // fitting line (no half lines); one line is kept free for the "+N" button.
  const measure = useRef(() => {});
  measure.current = () => {
    const root = rootRef.current;
    const clip = clipRef.current;
    // while expanded the panel is as tall as its content: measuring then would see "everything fits" and collapse it
    if (!clipped || !root || !clip) return;
    if (expanded) {
      clip.style.maxHeight = "none";
      return;
    }
    clip.style.maxHeight = "none";
    const items = Array.from(clip.querySelectorAll<HTMLElement>("li"));
    const room = root.clientHeight;
    const fits = (limit: number) => items.filter((li) => li.offsetTop + li.offsetHeight <= limit);
    let visible = fits(room);
    if (visible.length < items.length) {
      const more = moreRef.current?.offsetHeight ?? 0;
      visible = fits(room - more);
    }
    const hiddenNow = items.length - visible.length;
    const bottom = visible.reduce((max, li) => Math.max(max, li.offsetTop + li.offsetHeight), 0);
    clip.style.maxHeight = hiddenNow > 0 && !expanded ? `${bottom}px` : "none";
    setHidden((previous) => (previous === hiddenNow ? previous : hiddenNow));
  };
  useLayoutEffect(() => {
    measure.current();
  });
  useEffect(() => {
    const root = rootRef.current;
    return root && clipped ? observe(root, () => measure.current()) : undefined;
  }, [clipped]);

  const expandedNow = clipped && expanded && hidden > 0;
  const collapse = () => onExpandedChange?.(false);

  useEffect(() => {
    if (!expandedNow) return;
    const away = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) collapse();
    };
    document.addEventListener("pointerdown", away);
    return () => document.removeEventListener("pointerdown", away);
  });

  return (
    // biome-ignore lint/a11y/useSemanticElements: a labelled group of links, not a form fieldset
    <div
      ref={rootRef}
      role="group"
      aria-label={label}
      data-mode={mode}
      data-expanded={expandedNow ? "" : undefined}
      className="verse-side font-sans"
      onFocusCapture={() => {
        if (clipped && hidden > 0) onExpandedChange?.(true);
      }}
      onBlurCapture={(event) => {
        if (!rootRef.current?.contains(event.relatedTarget as Node | null)) collapse();
      }}
      onKeyDown={(event) => {
        if (event.key === "Escape" && expandedNow) {
          event.stopPropagation();
          collapse();
        }
      }}
    >
      <div ref={clipRef} className="verse-side-clip">
        {children}
      </div>
      {clipped && hidden > 0 && (
        <Button
          ref={moreRef}
          variant="ghost"
          size="sm"
          className="verse-side-more"
          aria-expanded={expandedNow}
          aria-label={t("bible.sidePanel.moreLabel", { count: hidden })}
          onClick={() => onExpandedChange?.(!expandedNow)}
        >
          {expandedNow ? t("bible.sidePanel.less") : t("bible.sidePanel.more", { count: hidden })}
        </Button>
      )}
    </div>
  );
}
