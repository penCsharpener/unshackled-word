import { type InputHTMLAttributes, type Ref, useId } from "react";

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
}

const sizes = {
  sm: "px-2 py-1 text-sm",
  md: "px-3 py-2 text-base",
} as const;

/** Labelled text input (also email / password) styled with tokens; required by default. */
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
  className = "",
  ...props
}: TextFieldProps) {
  const id = useId();
  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={id} className={labelHidden ? "sr-only" : undefined}>
        {label}
      </label>
      <input
        id={id}
        ref={inputRef}
        type={type}
        value={value}
        required={required}
        aria-invalid={invalid || undefined}
        onChange={(event) => onChange(event.target.value)}
        className={`rounded-md border bg-surface text-fg ${invalid ? "border-accent" : "border-border"} ${sizes[size]} ${className}`}
        {...props}
      />
    </div>
  );
}
