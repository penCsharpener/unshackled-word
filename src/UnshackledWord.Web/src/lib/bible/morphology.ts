import type { TFunction } from "i18next";
import type { Morphology } from "./types";

/** Order in which parts are shown. */
export const MORPHOLOGY_PART_ORDER = [
  "partOfSpeech",
  "stem",
  "hebrewForm",
  "tense",
  "voice",
  "mood",
  "person",
  "case",
  "number",
  "gender",
  "state",
  "degree",
] as const;

/** "Substantiv · Nominativ · Singular · maskulin"; falls back to `description`, then to the code. */
export function formatMorphology(morphology: Morphology, t: TFunction): string {
  const parts: string[] = [];
  for (const part of MORPHOLOGY_PART_ORDER) {
    const value = morphology[part];
    if (value) parts.push(t(`bible.grammar.${part}.${value}`));
  }
  if (parts.length > 0) return parts.join(" · ");
  return morphology.description ?? morphology.code;
}
