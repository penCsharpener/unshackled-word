import type { ButtonHTMLAttributes } from "react";

const variants = {
  primary: "bg-accent text-accent-fg hover:opacity-90",
  secondary: "border border-border bg-surface text-fg hover:bg-surface-muted",
  ghost: "text-fg hover:bg-surface-muted",
} as const;

const sizes = {
  sm: "px-2 py-1 text-sm",
  md: "px-4 py-2 text-base",
} as const;

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: keyof typeof variants;
  size?: keyof typeof sizes;
}

export type ButtonStyle = Pick<ButtonProps, "variant" | "size">;

/** Class string of a Button, for elements that must look like one (e.g. router links). */
export function buttonClasses({ variant = "secondary", size = "md" }: ButtonStyle = {}): string {
  return `inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${sizes[size]}`;
}

export function Button({
  variant = "secondary",
  size = "md",
  type = "button",
  className = "",
  ...props
}: ButtonProps) {
  return (
    // biome-ignore lint/plugin: this is the one place the native button is wrapped
    <button type={type} className={`${buttonClasses({ variant, size })} ${className}`} {...props} />
  );
}
