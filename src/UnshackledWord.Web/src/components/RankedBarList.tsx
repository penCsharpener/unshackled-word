import { type ReactNode, useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatNumber, formatPercent } from "@/lib/chart";
import { Button } from "./Button";

export interface RankedFlag {
  key: string;
  label: string;
  severity: "error" | "warning" | "info";
}

export interface RankedItem {
  key: string;
  label: ReactNode;
  sub?: ReactNode;
  value: number;
  /** 0-1; defaults to the share of the shown total */
  share?: number;
  flags?: RankedFlag[];
  /** dimmed (legit classes, function words) */
  muted?: boolean;
  /** `false` keeps the label plain text although the list has `onSelect` */
  selectable?: boolean;
  /** Extra control after the share (e.g. a link to the verses behind the row). */
  action?: ReactNode;
}

export interface RankedBarListProps {
  title: ReactNode;
  items: RankedItem[];
  /** Rows shown before "Show more" (default 10). */
  max?: number;
  /** The label becomes a button (e.g. explorer filter). */
  onSelect?: (key: string) => void;
  selectedKey?: string | null;
  /** Content under an expanded row; adds an expand button with `expandLabel` per row. */
  renderExpanded?: (item: RankedItem) => ReactNode;
  expandLabel?: string;
  /** Formats the value text; default locale number. */
  valueFormat?: (value: number) => string;
}

const FLAG_CLASSES = {
  error: "border-sev-error",
  warning: "border-sev-warning",
  info: "border-sev-info",
} as const;

const DEFAULT_MAX = 10;

/** Horizontal bars as an ordered list; the bar is decoration, value and share are always text. */
export function RankedBarList({
  title,
  items,
  max = DEFAULT_MAX,
  onSelect,
  selectedKey = null,
  renderExpanded,
  expandLabel,
  valueFormat,
}: RankedBarListProps) {
  const { t, i18n } = useTranslation();
  const headingId = useId();
  const [showAll, setShowAll] = useState(false);
  const [expandedKey, setExpandedKey] = useState<string | null>(null);

  const shown = showAll ? items : items.slice(0, max);
  const largest = items.reduce((m, item) => Math.max(m, item.value), 0);
  const total = items.reduce((sum, item) => sum + item.value, 0);
  const format = valueFormat ?? ((value: number) => formatNumber(value, i18n.language));

  return (
    <section aria-labelledby={headingId} className="grid gap-2">
      <h3 id={headingId} className="text-sm font-medium text-fg">
        {title}
      </h3>
      <ol className="grid gap-1">
        {shown.map((item) => {
          const share = item.share ?? (total > 0 ? item.value / total : 0);
          const width = largest > 0 ? Math.max((item.value / largest) * 100, 1.5) : 0;
          const selected = item.key === selectedKey;
          const expanded = expandedKey === item.key;
          const selectable = onSelect && item.selectable !== false;
          const label = selectable ? (
            <Button
              variant="inline"
              aria-pressed={selected}
              className="min-w-0 truncate text-left text-accent underline aria-pressed:font-semibold"
              onClick={() => onSelect(item.key)}
            >
              {item.label}
            </Button>
          ) : (
            <span className="min-w-0 truncate">{item.label}</span>
          );
          return (
            <li
              key={item.key}
              className={`grid gap-1 rounded-md px-1 py-1 ${item.muted ? "opacity-60" : ""} ${
                selected ? "bg-mark-same" : ""
              }`}
            >
              <div className="flex items-baseline justify-between gap-2 text-sm">
                <span className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
                  {label}
                  {item.sub ? <span className="text-fg-muted">{item.sub}</span> : null}
                  {item.flags?.map((flag) => (
                    <span
                      key={flag.key}
                      className={`rounded-sm border px-1 text-xs text-fg-muted ${FLAG_CLASSES[flag.severity]}`}
                    >
                      {flag.label}
                    </span>
                  ))}
                </span>
                <span className="flex shrink-0 items-center gap-2 tabular-nums">
                  <span>{format(item.value)}</span>
                  <span className="w-12 text-right text-fg-muted">
                    {formatPercent(share, 1, i18n.language)}
                  </span>
                  {item.action}
                  {renderExpanded ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      aria-expanded={expanded}
                      onClick={() => setExpandedKey(expanded ? null : item.key)}
                    >
                      {expandLabel ?? t("chart.ranked.expand")}
                    </Button>
                  ) : null}
                </span>
              </div>
              <svg aria-hidden="true" className="h-2 w-full rounded-sm bg-chart-empty">
                <rect width={`${width}%`} height="100%" rx="2" className="fill-chart-1" />
              </svg>
              {expanded && renderExpanded ? (
                <div className="pt-1">{renderExpanded(item)}</div>
              ) : null}
            </li>
          );
        })}
      </ol>
      {items.length > max ? (
        <Button
          variant="ghost"
          size="sm"
          className="justify-self-start"
          onClick={() => setShowAll((value) => !value)}
        >
          {showAll ? t("chart.ranked.less") : t("chart.ranked.more", { count: items.length - max })}
        </Button>
      ) : null}
    </section>
  );
}
