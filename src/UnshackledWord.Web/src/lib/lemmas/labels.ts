import type { SelectOption } from "@/components/Select";
import type { ValueMapsResponse } from "./types";

export interface FieldOption extends SelectOption {
  /** "label (Code)" tooltip */
  title: string;
}
export type Labels = Readonly<Record<string, readonly FieldOption[]>>;

/** Dropdown options per field in the UI language (German labels for DE, English for EN). */
export function buildLabels(maps: ValueMapsResponse | undefined, language: string): Labels {
  const english = language.startsWith("en");
  const result: Record<string, FieldOption[]> = {};
  for (const field of maps?.fields ?? []) {
    result[field.field] = field.values.map((entry) => {
      const label = english ? entry.labelEn : entry.labelDe;
      return { value: entry.code, label, title: `${label} (${entry.code})` };
    });
  }
  return result;
}

export function labelOf(labels: Labels, field: string, code: string | null | undefined): string {
  if (!code) return "";
  return labels[field]?.find((option) => option.value === code)?.label ?? code;
}

export function titleOf(labels: Labels, field: string, code: string | null | undefined): string {
  if (!code) return "";
  return labels[field]?.find((option) => option.value === code)?.title ?? code;
}
