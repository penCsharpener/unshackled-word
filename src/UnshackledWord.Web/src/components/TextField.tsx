import { type InputHTMLAttributes, type ReactNode, type Ref, useId } from "react";
import { SEARCH_ICON_PATHS } from "@/lib/search/icon";

export interface TextFieldProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, "onChange" | "value" | "size"> {
  label: string;
  value: string;
  onChange: (value: string) => void;
  size?: "sm" | "md";
  /** Keeps the label for assistive tech only, e.g. for an editor inside a table cell. */
  labelHidden?: boolean;
  /** Error style (accent border) */
  invalid?: boolean;
  inputRef?: Ref<HTMLInputElement>;
  /** Leading icon inside the field; `type="search"` gets a magnifier by default. */
  icon?: ReactNode;
}

const sizes = {
  sm: "px-2 py-1 text-sm",
  md: "px-3 py-2 text-base",
} as const;

function SearchIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-5"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {SEARCH_ICON_PATHS.map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  );
}

/** Labelled text input (also email / password / search) styled with tokens; required by default. */
export function TextField({
  label,
  value,
  onChange,
  type = "text",
  required = true,
  size = "md",
  labelHidden = false,
  invalid = false,
  inputRef,
  icon,
  className = "",
  ...props
}: TextFieldProps) {
  const id = useId();
  const leading = icon ?? (type === "search" ? <SearchIcon /> : null);
  const input = (
    <input
      id={id}
      ref={inputRef}
      type={type}
      value={value}
      required={required}
      aria-invalid={invalid || undefined}
      onChange={(event) => onChange(event.target.value)}
      className={`rounded-md border bg-surface text-fg ${invalid ? "border-accent" : "border-border"} ${sizes[size]} ${leading ? "w-full ps-10" : ""} ${className}`}
      {...props}
    />
  );
  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={id} className={labelHidden ? "sr-only" : undefined}>
        {label}
      </label>
      {leading ? (
        <div className="relative">
          <span
            aria-hidden="true"
            className="pointer-events-none absolute inset-y-0 start-3 flex items-center text-fg-muted"
          >
            {leading}
          </span>
          {input}
        </div>
      ) : (
        input
      )}
    </div>
  );
}
