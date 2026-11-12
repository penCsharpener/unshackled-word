import type { ReactNode } from "react";
import { Link } from "react-router";
import { Button } from "./Button";

export type StatTileSeverity = "error" | "warning" | "info" | "ok";

export interface StatTileProps {
  code?: string;
  title: ReactNode;
  /** Plain-text title for the accessible name; defaults to `title` if that is a string. */
  titleText?: string;
  value: ReactNode;
  sub?: ReactNode;
  /** Change since the previous run, already formatted with sign (e.g. "+12 since ..."); no colour judgement. */
  delta?: ReactNode;
  severity?: StatTileSeverity;
  /** Visible severity label (icon + text, never colour alone). */
  severityLabel?: string;
  selected?: boolean;
  /** A link tile; otherwise `onSelect` makes it a button; with neither it is a plain card. */
  href?: string;
  onSelect?: () => void;
  loading?: boolean;
}

const BORDER: Record<StatTileSeverity, string> = {
  error: "border-l-sev-error",
  warning: "border-l-sev-warning",
  info: "border-l-sev-info",
  ok: "border-l-sev-ok",
};

const ICON: Record<StatTileSeverity, string> = {
  error: "✕",
  warning: "▲",
  info: "ℹ",
  ok: "✓",
};

const CARD =
  "flex min-w-0 flex-col items-start gap-1 rounded-md border border-l-4 border-border bg-surface p-3 text-left";
const INTERACTIVE =
  "cursor-pointer transition-colors hover:bg-surface-muted aria-pressed:bg-mark-same";

/** Overview card of one number: code, title, big count, sub line, delta and severity (icon + label + border). */
export function StatTile({
  code,
  title,
  titleText,
  value,
  sub,
  delta,
  severity,
  severityLabel,
  selected = false,
  href,
  onSelect,
  loading = false,
}: StatTileProps) {
  const border = severity ? BORDER[severity] : "border-l-border";
  const name = [
    code,
    titleText ?? (typeof title === "string" ? title : undefined),
    loading ? undefined : `${typeof value === "string" || typeof value === "number" ? value : ""}`,
    severityLabel,
  ]
    .filter(Boolean)
    .join(" ");

  const body = loading ? (
    <span aria-hidden="true" className="flex w-full flex-col gap-2">
      <span className="h-3 w-10 rounded-sm bg-border" />
      <span className="h-3 w-3/4 rounded-sm bg-border" />
      <span className="h-6 w-1/2 rounded-sm bg-border" />
    </span>
  ) : (
    <>
      <span className="flex w-full items-baseline gap-2 text-sm text-fg-muted">
        {code ? <span className="font-medium text-fg">{code}</span> : null}
        <span className="min-w-0 truncate">{title}</span>
      </span>
      <span className="text-2xl font-semibold tabular-nums text-fg @6xl:text-3xl">{value}</span>
      {sub ? <span className="hidden text-sm text-fg-muted @2xl:block">{sub}</span> : null}
      {delta ? <span className="text-sm tabular-nums text-fg-muted">{delta}</span> : null}
      {severity && severityLabel ? (
        <span className="flex items-center gap-1 text-sm text-fg-muted">
          <span aria-hidden="true">{ICON[severity]}</span>
          {severityLabel}
        </span>
      ) : null}
    </>
  );

  if (loading || (!href && !onSelect)) {
    return (
      <div className={`${CARD} ${border}`} aria-busy={loading || undefined}>
        {body}
      </div>
    );
  }
  if (href) {
    return (
      <Link
        to={href}
        aria-label={name}
        aria-current={selected ? "true" : undefined}
        className={`${CARD} ${border} ${INTERACTIVE}`}
      >
        {body}
      </Link>
    );
  }
  return (
    <Button
      variant="secondary"
      aria-label={name}
      aria-pressed={selected}
      onClick={onSelect}
      className={`${CARD} ${border} ${INTERACTIVE} !items-start !justify-start`}
    >
      {body}
    </Button>
  );
}
