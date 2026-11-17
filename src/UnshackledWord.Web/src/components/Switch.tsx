import type { ButtonHTMLAttributes, ReactNode } from "react";
import { Button } from "./Button";

export interface SwitchProps
  extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "onChange" | "role" | "children"> {
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
  /** Label next to the track; may carry a visual key (e.g. a line sample). */
  children?: ReactNode;
}

/**
 * On/off switch (track with a sliding knob) with an optional label; a `role="switch"` button.
 * Inherits the font size of its context; track and knob geometry live in `.switch` (index.css).
 */
export function Switch({
  checked,
  onCheckedChange,
  children,
  className = "",
  type = "button",
  ...props
}: SwitchProps) {
  return (
    <Button
      variant="inline"
      type={type}
      role="switch"
      aria-checked={checked}
      onClick={() => onCheckedChange(!checked)}
      className={`switch inline-flex items-center gap-2 py-0.5 disabled:cursor-not-allowed disabled:opacity-50 ${className}`}
      {...props}
    >
      <span aria-hidden="true" className="switch-track">
        <span className="switch-knob" />
      </span>
      {children != null && <span className="inline-flex items-center gap-1.5">{children}</span>}
    </Button>
  );
}
