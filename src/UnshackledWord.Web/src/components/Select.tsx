import { type KeyboardEventHandler, type Ref, useId } from "react";

export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
  /** native tooltip, e.g. the code behind a label */
  title?: string;
}

export interface SelectProps {
  /** Visible label, rendered before the select (screen-reader only with `labelHidden`). */
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: readonly SelectOption[];
  disabled?: boolean;
  size?: "sm" | "md";
  /** Keeps the label for assistive tech only, e.g. for an editor inside a table cell. */
  labelHidden?: boolean;
  /** Error style (accent border) */
  invalid?: boolean;
  selectRef?: Ref<HTMLSelectElement>;
  onBlur?: () => void;
  onKeyDown?: KeyboardEventHandler<HTMLSelectElement>;
  className?: string;
}

const sizes = {
  sm: "px-2 py-1 text-sm",
  md: "px-3 py-2 text-base",
} as const;

export function Select({
  label,
  value,
  onChange,
  options,
  disabled,
  size = "sm",
  labelHidden = false,
  invalid = false,
  selectRef,
  onBlur,
  onKeyDown,
  className = "",
}: SelectProps) {
  const id = useId();
  return (
    <div className={`flex items-center gap-2 text-sm ${className}`}>
      <label htmlFor={id} className={labelHidden ? "sr-only" : "shrink-0"}>
        {labelHidden ? label : `${label}:`}
      </label>
      <select
        id={id}
        ref={selectRef}
        value={value}
        disabled={disabled}
        aria-invalid={invalid || undefined}
        onBlur={onBlur}
        onKeyDown={onKeyDown}
        onChange={(event) => onChange(event.target.value)}
        className={`min-w-0 max-w-72 flex-1 rounded-md border bg-surface text-fg disabled:cursor-not-allowed disabled:opacity-50 ${
          invalid ? "border-accent" : "border-border"
        } ${sizes[size]}`}
      >
        {options.map((option) => (
          <option
            key={option.value}
            value={option.value}
            disabled={option.disabled}
            title={option.title}
          >
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}
