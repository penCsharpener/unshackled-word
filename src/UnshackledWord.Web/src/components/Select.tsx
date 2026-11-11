import { useId } from "react";

export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface SelectProps {
  /** Visible label, rendered before the select. */
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: readonly SelectOption[];
  disabled?: boolean;
  size?: "sm" | "md";
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
  className = "",
}: SelectProps) {
  const id = useId();
  return (
    <div className={`flex items-center gap-2 text-sm ${className}`}>
      <label htmlFor={id} className="shrink-0">
        {label}:
      </label>
      <select
        id={id}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        className={`min-w-0 max-w-72 flex-1 rounded-md border border-border bg-surface text-fg disabled:cursor-not-allowed disabled:opacity-50 ${sizes[size]}`}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value} disabled={option.disabled}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}
