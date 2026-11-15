import { type ReactNode, type RefObject, useLayoutEffect, useRef } from "react";

export interface MarginStackProps {
  /** verse the stack lines up with (its top edge) */
  verse: number;
  /** the `reader-col` the verses live in; the stack never reaches below its bottom edge */
  colRef: RefObject<HTMLElement | null>;
  children: ReactNode;
}

/** A column of cards in the right page margin next to the text, level with a verse and pushed up near the chapter end. */
export function MarginStack({ verse, colRef, children }: MarginStackProps) {
  const ref = useRef<HTMLDivElement>(null);
  // every render: the cards change size when the word detail arrives
  useLayoutEffect(() => {
    const el = ref.current;
    const col = colRef.current;
    if (!el || !col) return;
    const verseEl = col.querySelector<HTMLElement>(`#v${verse}`);
    const colRect = col.getBoundingClientRect();
    const wanted = verseEl ? verseEl.getBoundingClientRect().top - colRect.top : 0;
    const max = Math.max(0, colRect.height - el.offsetHeight);
    el.style.top = `${Math.min(Math.max(0, wanted), max)}px`;
    // room the cards may grow into, away from the text: up to the viewport edge minus a gutter
    const rect = el.getBoundingClientRect();
    const gutter = parseFloat(getComputedStyle(el).fontSize);
    const room = document.documentElement.clientWidth - rect.left;
    el.style.setProperty("--margin-room", `${Math.max(0, room - gutter)}px`);
  });
  return (
    <div ref={ref} className="margin-stack">
      {children}
    </div>
  );
}
