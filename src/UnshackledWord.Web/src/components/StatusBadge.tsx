import type { ReactNode } from "react";

export type StatusTone = "neutral" | "info" | "ok" | "warning" | "error";

export interface StatusBadgeProps {
  tone: StatusTone;
  /** The status in words: the text carries the meaning, the tone only supports it. */
  children: ReactNode;
  /** `badge` (default): bordered label with icon and text. `dot`: small round mark, the text is screen-reader only. */
  variant?: "badge" | "dot";
  /** Dot only: pulses (not under reduced motion) for a running state. */
  busy?: boolean;
}

const TONES: Record<StatusTone, { border: string; icon: string; dot: string }> = {
  neutral: { border: "border-border", icon: "•", dot: "bg-fg-muted" },
  info: { border: "border-sev-info", icon: "○", dot: "bg-sev-info" },
  ok: { border: "border-sev-ok", icon: "✓", dot: "bg-sev-ok" },
  warning: { border: "border-sev-warning", icon: "▲", dot: "bg-sev-warning" },
  error: { border: "border-sev-error", icon: "✕", dot: "bg-sev-error" },
};

/**
 * Small status label (run state, token state): icon + text on a toned border, never colour alone.
 * The `dot` variant is only a mark: show the status word visibly next to it.
 */
export function StatusBadge({ tone, children, variant = "badge", busy = false }: StatusBadgeProps) {
  const { border, icon, dot } = TONES[tone];
  if (variant === "dot") {
    return (
      <span
        data-tone={tone}
        className={`inline-block size-2.5 shrink-0 rounded-full ${dot} ${busy ? "motion-safe:animate-pulse" : ""}`}
      >
        <span className="sr-only">{children}</span>
      </span>
    );
  }
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
