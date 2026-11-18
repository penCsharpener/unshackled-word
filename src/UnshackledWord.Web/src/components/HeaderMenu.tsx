import { type ReactNode, useEffect, useRef, useState } from "react";
import { NavLink } from "react-router";
import { Button } from "./Button";

export type HeaderMenuItem = { key: string; to: string; label: string };

type HeaderMenuProps = {
  /** Content of the trigger button (icon or initial). */
  trigger: ReactNode;
  /** Accessible name of the trigger button. */
  label: string;
  items: HeaderMenuItem[];
  /** Muted line above the items (e.g. the e-mail address). */
  heading?: string;
  /** Extra menu content below the items; `close` closes the menu. */
  footer?: (close: () => void) => ReactNode;
  triggerVariant?: "primary" | "secondary" | "ghost";
  triggerClassName?: string;
};

/** Header trigger button with a popover menu of links; closes on outside click, Escape and selection. */
export function HeaderMenu({
  trigger,
  label,
  items,
  heading,
  footer,
  triggerVariant = "ghost",
  triggerClassName,
}: HeaderMenuProps) {
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onPointer = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const close = () => setOpen(false);

  return (
    <div ref={root} className="relative">
      <Button
        variant={triggerVariant}
        size="sm"
        className={triggerClassName}
        aria-label={label}
        title={label}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((value) => !value)}
      >
        {trigger}
      </Button>
      {open && (
        <div
          role="menu"
          className="absolute right-0 top-full z-40 mt-2 flex min-w-48 flex-col gap-1 rounded-md border border-border bg-surface p-2 text-sm"
        >
          {heading && <p className="truncate px-2 py-1 text-fg-muted">{heading}</p>}
          {items.map((item) => (
            <NavLink
              key={item.key}
              role="menuitem"
              to={item.to}
              className={({ isActive }) =>
                `rounded-md px-2 py-1 hover:bg-surface-muted ${isActive ? "bg-surface-muted font-medium" : ""}`
              }
              onClick={close}
            >
              {item.label}
            </NavLink>
          ))}
          {footer?.(close)}
        </div>
      )}
    </div>
  );
}
