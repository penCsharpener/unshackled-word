import { type ReactNode, useLayoutEffect, useRef } from "react";
import { createPortal } from "react-dom";
import { usePickerSlot } from "@/lib/pickerSlot";
import { useMediaQuery } from "@/lib/useMediaQuery";

/**
 * Wraps the chapter picker (and the level slider). From 1200px it is rendered into the header's slot
 * so the menu is one row; below that it stays in the page as a sticky bar under the header.
 * While the sticky bar is shown its measured height is published as `--picker-bar-h` on the root, so
 * `--reader-sticky-top` (index.css) places further sticky rows directly below it.
 */
export function HeaderPickerBar({ children }: { children: ReactNode }) {
  const slot = usePickerSlot();
  const inHeader = useMediaQuery("(min-width: 75rem)");
  const portal = inHeader && slot;
  const barRef = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const bar = barRef.current;
    if (portal || !bar) return;
    const root = document.documentElement;
    const publish = () => root.style.setProperty("--picker-bar-h", `${bar.offsetHeight}px`);
    publish();
    const observer =
      typeof ResizeObserver === "undefined" ? undefined : new ResizeObserver(publish);
    observer?.observe(bar);
    return () => {
      observer?.disconnect();
      root.style.removeProperty("--picker-bar-h");
    };
  }, [portal]);

  if (portal) {
    return createPortal(<div className="flex items-center gap-4">{children}</div>, slot);
  }
  return (
    <div
      ref={barRef}
      className="sticky-picker -mx-4 -mt-8 flex items-start gap-2 px-4 md:-mx-8 md:px-8"
    >
      {children}
    </div>
  );
}
