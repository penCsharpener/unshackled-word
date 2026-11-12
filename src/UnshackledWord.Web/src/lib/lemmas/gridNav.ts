export interface Cell {
  row: number;
  col: number;
}

export const PAGE_ROWS = 15;

export type NavKey =
  | "ArrowUp"
  | "ArrowDown"
  | "ArrowLeft"
  | "ArrowRight"
  | "PageUp"
  | "PageDown"
  | "Home"
  | "End";

const clamp = (value: number, max: number) => Math.max(0, Math.min(max, value));

/** Cursor movement of the arrow / page / home / end keys; Ctrl+Home/End jump to the first/last cell. */
export function moveCell(
  cell: Cell,
  key: NavKey,
  size: { rows: number; cols: number },
  ctrl = false,
): Cell {
  const lastRow = size.rows - 1;
  const lastCol = size.cols - 1;
  switch (key) {
    case "ArrowUp":
      return { ...cell, row: clamp(cell.row - 1, lastRow) };
    case "ArrowDown":
      return { ...cell, row: clamp(cell.row + 1, lastRow) };
    case "ArrowLeft":
      return { ...cell, col: clamp(cell.col - 1, lastCol) };
    case "ArrowRight":
      return { ...cell, col: clamp(cell.col + 1, lastCol) };
    case "PageUp":
      return { ...cell, row: clamp(cell.row - PAGE_ROWS, lastRow) };
    case "PageDown":
      return { ...cell, row: clamp(cell.row + PAGE_ROWS, lastRow) };
    case "Home":
      return ctrl ? { row: 0, col: 0 } : { ...cell, col: 0 };
    case "End":
      return ctrl ? { row: lastRow, col: lastCol } : { ...cell, col: lastCol };
  }
}

/**
 * Next (direction 1) or previous (-1) editable cell in reading order, wrapping to the next/previous
 * row. Returns null at the end of the grid so Tab can leave it.
 */
export function nextEditable(
  cell: Cell,
  direction: 1 | -1,
  size: { rows: number; cols: number },
  isEditable: (cell: Cell) => boolean,
): Cell | null {
  let { row, col } = cell;
  for (;;) {
    col += direction;
    if (col >= size.cols) {
      col = 0;
      row++;
    } else if (col < 0) {
      col = size.cols - 1;
      row--;
    }
    if (row < 0 || row >= size.rows) return null;
    if (isEditable({ row, col })) return { row, col };
  }
}

export interface PickOption {
  value: string;
  label: string;
}

/**
 * Type-to-pick on a closed dropdown cell: the next option (after the current one, cycling) whose
 * label or code starts with the typed text. Returns null when nothing matches.
 */
export function typeToPick(
  options: readonly PickOption[],
  current: string | null,
  typed: string,
): string | null {
  const needle = typed.toLocaleLowerCase();
  const matches = options.filter(
    (option) =>
      option.label.toLocaleLowerCase().startsWith(needle) ||
      option.value.toLocaleLowerCase().startsWith(needle),
  );
  if (matches.length === 0) return null;
  const index = matches.findIndex((option) => option.value === current);
  return (matches[(index + 1) % matches.length] as PickOption).value;
}
