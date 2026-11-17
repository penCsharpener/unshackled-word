import type { ReactNode } from "react";

export type StatusTone = "neutral" | "info" | "ok" | "warning" | "error";

export interface StatusBadgeProps {
  tone: StatusTone;
  /** The status in words: the text carries the meaning, the tone only supports it. */
  children: ReactNode;
}

const TONES: Record<StatusTone, { border: string; icon: string }> = {
  neutral: { border: "border-border", icon: "•" },
  info: { border: "border-sev-info", icon: "○" },
  ok: { border: "border-sev-ok", icon: "✓" },
  warning: { border: "border-sev-warning", icon: "▲" },
  error: { border: "border-sev-error", icon: "✕" },
};

/** Small status label (run state, token state): icon + text on a toned border, never colour alone. */
export function StatusBadge({ tone, children }: StatusBadgeProps) {
  const { border, icon } = TONES[tone];
  return (
    <span
      data-tone={tone}
      className={`inline-flex items-center gap-1 rounded-md border bg-surface px-2 py-0.5 text-xs font-medium text-fg ${border}`}
    >
      <span aria-hidden="true">{icon}</span>
      {children}
    </span>
  );
}
