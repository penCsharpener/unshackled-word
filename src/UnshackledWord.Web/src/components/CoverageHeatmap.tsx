import { type KeyboardEvent, type ReactNode, useId, useRef, useState } from "react";
import { heatLevel } from "@/lib/quality/checks";
import { Button } from "./Button";

export interface HeatCell {
  key: string;
  /** Short label inside the cell (matrix) and in the screen-reader table. */
  label: string;
  value: number;
  /** Denominator in `share` mode. */
  total?: number;
  /** Tooltip and accessible name of the cell. */
  title: string;
}

export interface HeatRow {
  key: string;
  label: ReactNode;
  /** Plain text for the screen-reader table; defaults to `label` if a string. */
  text?: string;
  cells: HeatCell[];
}

export interface CoverageHeatmapProps {
  title: ReactNode;
  rows: HeatRow[];
  /** Column headers of a matrix (cells in `rows` follow this order). Chapter grids have none. */
  columns?: { key: string; label: string }[];
  /** `share` colours `value / total` (0-1), `count` colours `value`. */
  mode: "share" | "count";
  /** Lower bounds of levels 1-5. */
  scale: readonly number[];
  legend?: ReactNode;
  onSelectCell?: (rowKey: string, cellKey: string) => void;
  selected?: { rowKey: string; cellKey: string } | null;
  /** Outlined cells (e.g. combinations that should not occur). */
  outlined?: (rowKey: string, cellKey: string) => boolean;
  /** Prints `cell.label` inside the cells (matrices). */
  showLabels?: boolean;
  /** Caption of the screen-reader table. */
  tableCaption?: string;
}

const LEVEL_CLASSES = [
  "bg-heat-0 text-fg",
  "bg-heat-1 text-fg",
  "bg-heat-2 text-fg",
  "bg-heat-3 text-accent-fg",
  "bg-heat-4 text-accent-fg",
  "bg-heat-5 text-accent-fg",
] as const;

function levelOf(cell: HeatCell, mode: "share" | "count", scale: readonly number[]): number {
  const value = mode === "share" ? (cell.total ? cell.value / cell.total : 0) : cell.value;
  return heatLevel(value, scale);
}

/**
 * Grid of coloured, button-based cells. One tab stop: arrow keys move between cells, Enter selects.
 * Colour is never the only carrier: every cell has a title and the screen-reader table lists all values.
 */
export function CoverageHeatmap({
  title,
  rows,
  columns,
  mode,
  scale,
  legend,
  onSelectCell,
  selected = null,
  outlined,
  showLabels = false,
  tableCaption,
}: CoverageHeatmapProps) {
  const headingId = useId();
  const grid = useRef<HTMLDivElement>(null);
  const firstRow = rows.find((row) => row.cells.length > 0);
  const [active, setActive] = useState<string | null>(null);
  const firstKey = firstRow?.cells[0] ? `${firstRow.key}\u0000${firstRow.cells[0].key}` : null;
  const activeKey = active ?? firstKey;

  const focusCell = (rowIndex: number, cellIndex: number) => {
    const row = rows[rowIndex];
    const cell = row?.cells[cellIndex];
    if (!row || !cell) return;
    const key = `${row.key}\u0000${cell.key}`;
    setActive(key);
    grid.current?.querySelector<HTMLElement>(`[data-pos="${rowIndex}-${cellIndex}"]`)?.focus();
  };

  const onKeyDown = (event: KeyboardEvent, rowIndex: number, cellIndex: number) => {
    const deltas: Record<string, [number, number]> = {
      ArrowLeft: [0, -1],
      ArrowRight: [0, 1],
      ArrowUp: [-1, 0],
      ArrowDown: [1, 0],
    };
    const delta = deltas[event.key];
    if (!delta) return;
    event.preventDefault();
    const [dr, dc] = delta;
    if (dr === 0) {
      focusCell(rowIndex, cellIndex + dc);
      return;
    }
    // Rows of different length (chapter counts): clamp to the last cell of the target row.
    let target = rowIndex + dr;
    while (rows[target] && rows[target]?.cells.length === 0) target += dr;
    const targetRow = rows[target];
    if (targetRow) focusCell(target, Math.min(cellIndex, targetRow.cells.length - 1));
  };

  const compact = !showLabels;

  return (
    <section aria-labelledby={headingId} className="grid gap-3">
      <h3 id={headingId} className="text-sm font-medium text-fg">
        {title}
      </h3>
      {/* with column headers the rows do not wrap; wide matrices (M3: 30 columns) scroll sideways */}
      <div className={columns ? "overflow-x-auto pb-2" : undefined}>
        <div ref={grid} className={`grid gap-2 ${columns ? "w-max min-w-full" : ""}`}>
          {columns ? (
            <div className="flex gap-1 pl-0 text-xs text-fg-muted @2xl:pl-35" aria-hidden="true">
              {columns.map((column) => (
                <span key={column.key} className="w-10 shrink-0 truncate text-center">
                  {column.label}
                </span>
              ))}
            </div>
          ) : null}
          {rows.map((row, rowIndex) => (
            <div
              key={row.key}
              className="flex flex-col gap-1 @2xl:flex-row @2xl:items-center @2xl:gap-3"
            >
              <span className="shrink-0 text-sm text-fg @2xl:w-32 @2xl:truncate">{row.label}</span>
              <div className={`flex gap-1 ${columns ? "" : "flex-wrap"}`}>
                {row.cells.map((cell, cellIndex) => {
                  const key = `${row.key}\u0000${cell.key}`;
                  const isSelected = selected?.rowKey === row.key && selected.cellKey === cell.key;
                  const isOutlined = outlined?.(row.key, cell.key);
                  return (
                    <Button
                      key={cell.key}
                      variant="inline"
                      title={cell.title}
                      aria-label={cell.title}
                      aria-pressed={onSelectCell ? isSelected : undefined}
                      tabIndex={key === activeKey ? 0 : -1}
                      onKeyDown={(event) => onKeyDown(event, rowIndex, cellIndex)}
                      onFocus={() => setActive(key)}
                      onClick={() => onSelectCell?.(row.key, cell.key)}
                      className={`flex items-center justify-center rounded-sm text-xs tabular-nums ${
                        compact ? "size-5" : "size-10"
                      } ${LEVEL_CLASSES[levelOf(cell, mode, scale)]} ${
                        isSelected ? "outline-2 outline-focus" : ""
                      } ${isOutlined ? "border-2 border-dashed border-fg" : ""}`}
                      data-pos={`${rowIndex}-${cellIndex}`}
                    >
                      {showLabels ? cell.label : null}
                    </Button>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      </div>
      {legend ? (
        <div className="flex flex-wrap items-center gap-2 text-xs text-fg-muted">
          <span>{legend}</span>
          <span aria-hidden="true" className="flex gap-1">
            {LEVEL_CLASSES.map((classes) => (
              <span key={classes} className={`size-3 rounded-sm ${classes.split(" ")[0]}`} />
            ))}
          </span>
        </div>
      ) : null}
      {/* the clipping wrapper keeps the wide table from widening the page */}
      <div className="sr-only">
        <table>
          {tableCaption ? <caption>{tableCaption}</caption> : null}
          <tbody>
            {rows.map((row) => (
              <tr key={row.key}>
                <th scope="row">
                  {row.text ?? (typeof row.label === "string" ? row.label : row.key)}
                </th>
                {row.cells.map((cell) => (
                  <td key={cell.key}>{cell.title}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
