import { type InputHTMLAttributes, useId } from "react";

export interface TextFieldProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, "onChange" | "value"> {
  label: string;
  value: string;
  onChange: (value: string) => void;
}

/** Labelled text input (also email / password) styled with tokens; required by default. */
export function TextField({
  label,
  value,
  onChange,
  type = "text",
  required = true,
  className = "",
  ...props
}: TextFieldProps) {
  const id = useId();
  return (
    <div className="flex flex-col gap-1 text-sm">
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        type={type}
        value={value}
        required={required}
        onChange={(event) => onChange(event.target.value)}
        className={`rounded-md border border-border bg-surface px-3 py-2 text-base text-fg ${className}`}
        {...props}
      />
    </div>
  );
}
