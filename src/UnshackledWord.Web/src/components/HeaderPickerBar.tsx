import type { ReactNode } from "react";
import { createPortal } from "react-dom";
import { usePickerSlot } from "@/lib/pickerSlot";
import { useMediaQuery } from "@/lib/useMediaQuery";

/**
 * Wraps the chapter picker (and the level slider). From 1200px it is rendered into the header's slot
 * so the menu is one row; below that it stays in the page as a sticky bar under the header.
 */
export function HeaderPickerBar({ children }: { children: ReactNode }) {
  const slot = usePickerSlot();
  const inHeader = useMediaQuery("(min-width: 75rem)");
  if (inHeader && slot) {
    return createPortal(<div className="flex items-center gap-4">{children}</div>, slot);
  }
  return (
    <div className="sticky-picker -mx-4 -mt-8 flex items-start gap-2 px-4 md:-mx-8 md:px-8">
      {children}
    </div>
  );
}
