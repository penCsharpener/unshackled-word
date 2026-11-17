export type ProgressTone = "info" | "ok" | "warning" | "error";

export interface ProgressBarProps {
  /** Done so far; ignored (the bar is indeterminate) when `max` is 0. */
  value: number;
  max: number;
  /** Accessible name and the visible caption above the bar. */
  label: string;
  tone?: ProgressTone;
}

/** Horizontal progress bar on the native `<progress>` (role `progressbar`), styled with tokens; no value while `max` is 0. */
export function ProgressBar({ value, max, label, tone = "info" }: ProgressBarProps) {
  const known = max > 0;
  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-baseline justify-between gap-2 text-sm">
        <span>{label}</span>
        {known ? (
          <span className="tabular-nums text-fg-muted">
            {Math.min(value, max)} / {max}
          </span>
        ) : null}
      </div>
      <progress
        className="uw-progress"
        data-tone={tone}
        aria-label={label}
        value={known ? Math.min(value, max) : undefined}
        max={known ? max : undefined}
      />
    </div>
  );
}
