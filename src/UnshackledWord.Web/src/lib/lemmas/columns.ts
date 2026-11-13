import { MORPH_FIELDS, type MorphField } from "./types";

export type FixedColumnKey = "verse" | "context" | "plain" | "lemma" | "partOfSpeech" | "stts";
export type ColumnKey = FixedColumnKey | MorphField;

/** Columns the user may hide (the menu); Vers, Wort im Kontext, Lemma and Wortart always stay. */
export const HIDEABLE_COLUMNS: readonly ColumnKey[] = ["plain", "stts", ...MORPH_FIELDS];
export const ALL_COLUMNS: readonly ColumnKey[] = [
  "verse",
  "context",
  "plain",
  "lemma",
  "partOfSpeech",
  "stts",
  ...MORPH_FIELDS,
];

/** Columns the cursor can land on (everything but the verse gutter). */
export function navColumns(hidden: ReadonlySet<ColumnKey>): ColumnKey[] {
  return ALL_COLUMNS.filter((key) => key !== "verse" && !hidden.has(key));
}

export function visibleColumns(hidden: ReadonlySet<ColumnKey>): ColumnKey[] {
  return ALL_COLUMNS.filter((key) => !hidden.has(key));
}

const FIXED_WIDTH: Record<FixedColumnKey, string> = {
  verse: "3rem",
  context: "8rem",
  plain: "7rem",
  lemma: "9rem",
  partOfSpeech: "10rem",
  stts: "13rem",
};

/** Horizontal room of a native select besides its text: padding plus the arrow. */
const SELECT_CHROME = "2.25rem";

/**
 * Width of a dropdown column: as wide as its longest label (or header), in `ch` of the row font,
 * plus the select chrome. Two extra characters leave room for the warning sign of invalid values.
 */
export function dropdownWidth(labels: readonly string[], header: string): string {
  const longest = Math.max(header.length, 2, ...labels.map((label) => label.length + 2));
  return `calc(${longest}ch + ${SELECT_CHROME})`;
}

export function columnWidth(
  key: ColumnKey,
  labelsByColumn: Partial<Record<ColumnKey, readonly string[]>>,
  headers: Partial<Record<ColumnKey, string>>,
): string {
  if (key === "partOfSpeech" || (MORPH_FIELDS as readonly string[]).includes(key)) {
    return dropdownWidth(labelsByColumn[key] ?? [], headers[key] ?? "");
  }
  return FIXED_WIDTH[key as FixedColumnKey];
}

export function gridTemplate(widths: readonly string[]): string {
  return widths.join(" ");
}
