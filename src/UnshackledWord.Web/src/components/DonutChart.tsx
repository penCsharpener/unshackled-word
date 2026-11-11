import { type ReactNode, useId } from "react";
import { useTranslation } from "react-i18next";
import { donutSegments, formatNumber, formatPercent, MAX_DONUT_SLOTS } from "@/lib/chart";
import { Button } from "./Button";
import { Select } from "./Select";

export interface DonutSlice {
  key: string;
  label: ReactNode;
  /** Plain-text name for the filter select and the table; defaults to `label` if that is a string. */
  text?: string;
  value: number;
  /** Emphasised in the legend and ring without dimming the others. */
  highlight?: boolean;
}

export interface DonutChartProps {
  title: ReactNode;
  subtitle?: ReactNode;
  note?: ReactNode;
  /** Slices in colour order (stable; sort by value before). More than 8 fold into "Other". */
  slices: DonutSlice[];
  /** Defaults to the number of shown slices' total. */
  centerValue?: ReactNode;
  centerLabel?: ReactNode;
  /** Selected slice: dims the others and shows its value in the centre. */
  activeKey?: string | null;
  /** Makes legend entries buttons and, with `filterLabel`, adds a select; `null` clears. */
  onSelectSlice?: (key: string | null) => void;
  filterLabel?: string;
}

const OTHER_KEY = "__other";
const SEGMENT_GAP = 0.6;

function plainText(slice: DonutSlice): string {
  return slice.text ?? (typeof slice.label === "string" ? slice.label : slice.key);
}

export function DonutChart({
  title,
  subtitle,
  note,
  slices,
  centerValue,
  centerLabel,
  activeKey = null,
  onSelectSlice,
  filterLabel,
}: DonutChartProps) {
  const { t, i18n } = useTranslation();
  const headingId = useId();
  const language = i18n.language;

  const positive = slices.filter((s) => s.value > 0);
  let shown: DonutSlice[] = positive;
  if (positive.length > MAX_DONUT_SLOTS) {
    const rest = positive.slice(MAX_DONUT_SLOTS - 1);
    shown = [
      ...positive.slice(0, MAX_DONUT_SLOTS - 1),
      {
        key: OTHER_KEY,
        label: t("chart.donut.other"),
        value: rest.reduce((sum, s) => sum + s.value, 0),
      },
    ];
  }

  const total = shown.reduce((sum, s) => sum + s.value, 0);
  const segments = donutSegments(
    shown.map((s) => s.value),
    SEGMENT_GAP,
  );
  const active = shown.find((s) => s.key === activeKey);
  const emphasised = active?.key ?? shown.find((s) => s.highlight)?.key;
  const interactive = onSelectSlice !== undefined;
  const showTable = !interactive;

  const toggle = (key: string) => onSelectSlice?.(activeKey === key ? null : key);

  const filterOptions = [
    { value: "", label: t("chart.donut.showAll") },
    ...shown
      .filter((s) => s.key !== OTHER_KEY)
      .map((s) => ({
        value: s.key,
        label: `${plainText(s)} – ${formatNumber(s.value, language)}×`,
      })),
  ];

  return (
    <figure
      aria-labelledby={headingId}
      data-filtered={active ? "" : undefined}
      className="donut @container m-0 grid content-start gap-3 rounded-lg border border-border bg-surface p-4"
    >
      <div>
        <h3 id={headingId} className="font-scripture text-lg font-semibold">
          {title}
        </h3>
        {subtitle ? <p className="text-sm text-fg-muted">{subtitle}</p> : null}
      </div>

      {interactive && filterLabel ? (
        <Select
          label={filterLabel}
          value={active?.key ?? ""}
          options={filterOptions}
          disabled={shown.length < 2}
          onChange={(value) => onSelectSlice(value === "" ? null : value)}
        />
      ) : null}

      <div className="donut-body">
        <div className="donut-ring" aria-hidden="true">
          <svg viewBox="0 0 100 100" aria-hidden="true" focusable="false">
            <circle className="donut-track" cx="50" cy="50" r="40" />
            {shown.map((slice, i) => {
              const arc = segments[i] ?? { start: 0, length: 0 };
              return (
                <circle
                  key={slice.key}
                  className="donut-seg"
                  data-slot={i + 1}
                  data-active={emphasised === slice.key ? "" : undefined}
                  cx="50"
                  cy="50"
                  r="40"
                  pathLength="100"
                  strokeDasharray={`${arc.length} ${100 - arc.length}`}
                  strokeDashoffset={-arc.start}
                />
              );
            })}
          </svg>
          <div className="donut-center">
            <b className="text-3xl leading-none">
              {active
                ? formatNumber(active.value, language)
                : (centerValue ?? formatNumber(total, language))}
            </b>
            <span className="donut-center-label">
              {active
                ? t("chart.donut.ofTotal", { total: formatNumber(total, language) })
                : centerLabel}
            </span>
          </div>
        </div>

        <ul className="grid gap-1" aria-hidden={showTable ? true : undefined}>
          {shown.map((slice, i) => {
            const selectable = interactive && slice.key !== OTHER_KEY;
            return (
              <li
                key={slice.key}
                className="donut-slice"
                data-slot={i + 1}
                data-active={emphasised === slice.key ? "" : undefined}
              >
                <span className="chart-swatch" />
                {selectable ? (
                  <Button
                    variant="inline"
                    className="donut-slice-label text-left"
                    aria-pressed={activeKey === slice.key}
                    onClick={() => toggle(slice.key)}
                  >
                    {slice.label}
                  </Button>
                ) : (
                  <span className="donut-slice-label">{slice.label}</span>
                )}
                <span className="donut-slice-count">
                  {t("chart.donut.slice", {
                    count: formatNumber(slice.value, language),
                    percent: formatPercent(slice.value, total, language),
                  })}
                </span>
              </li>
            );
          })}
        </ul>
      </div>

      {showTable ? (
        <table className="sr-only">
          <caption>{title}</caption>
          <thead>
            <tr>
              <th scope="col">{t("chart.donut.tableEntry")}</th>
              <th scope="col">{t("chart.donut.tableCount")}</th>
              <th scope="col">{t("chart.donut.tableShare")}</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((slice) => (
              <tr key={slice.key}>
                <th scope="row">{slice.label}</th>
                <td>{formatNumber(slice.value, language)}</td>
                <td>{formatPercent(slice.value, total, language)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      {note ? <figcaption className="text-xs text-fg-muted">{note}</figcaption> : null}
    </figure>
  );
}
