import { useEffect, useId, useRef, useState } from "react";
import { Button } from "./Button";

export interface ToggleMenuItem {
  key: string;
  label: string;
  checked: boolean;
  disabled?: boolean;
}

export interface ToggleMenuProps {
  label: string;
  items: readonly ToggleMenuItem[];
  onChange: (key: string, checked: boolean) => void;
  /** Adds a reset action at the end of the list when set together with `resetLabel`. */
  onReset?: () => void;
  resetLabel?: string;
  className?: string;
}

/** Button with a popover list of checkboxes, e.g. to show or hide table columns. */
export function ToggleMenu({
  label,
  items,
  onChange,
  onReset,
  resetLabel,
  className = "",
}: ToggleMenuProps) {
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const panelId = useId();

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

  return (
    <div ref={root} className={`relative ${className}`}>
      <Button
        size="sm"
        aria-haspopup="true"
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        onClick={() => setOpen((value) => !value)}
      >
        {label} <span aria-hidden="true">▾</span>
      </Button>
      {open && (
        <fieldset
          id={panelId}
          className="absolute left-0 top-full z-40 mt-1 flex min-w-48 flex-col gap-1 rounded-md border border-border bg-surface p-2 text-sm"
        >
          <legend className="sr-only">{label}</legend>
          {items.map((item) => (
            <label key={item.key} className="flex items-center gap-2 rounded-md px-2 py-1">
              <input
                type="checkbox"
                checked={item.checked}
                disabled={item.disabled}
                onChange={(event) => onChange(item.key, event.target.checked)}
              />
              {item.label}
            </label>
          ))}
          {onReset && resetLabel ? (
            <Button variant="ghost" size="sm" className="justify-start" onClick={onReset}>
              {resetLabel}
            </Button>
          ) : null}
        </fieldset>
      )}
    </div>
  );
}
