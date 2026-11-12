import type { Edits } from "./edits";
import { valuesOf } from "./edits";
import { invalidFields, type Rules } from "./rules";
import type { LemmaWord, WordValues } from "./types";

export interface Filters {
  search: string;
  /** "" = all, "none" = no part of speech, else a UD code */
  partOfSpeech: string;
  onlyMissingLemma: boolean;
}

export const NO_FILTERS: Filters = { search: "", partOfSpeech: "", onlyMissingLemma: false };

export function hasActiveFilters(filters: Filters): boolean {
  return filters.search.trim() !== "" || filters.partOfSpeech !== "" || filters.onlyMissingLemma;
}

export function currentValues(word: LemmaWord, edits: Edits): WordValues {
  return edits.get(word.wordId) ?? valuesOf(word);
}

/**
 * Filters the words. `edits` should be the snapshot taken when the filter last changed, so a row
 * that stops matching because of an edit stays in the list until the filter changes.
 */
export function filterWords(
  words: readonly LemmaWord[],
  edits: Edits,
  filters: Filters,
): readonly LemmaWord[] {
  if (!hasActiveFilters(filters)) return words;
  const needle = filters.search.trim().toLocaleLowerCase();
  return words.filter((word) => {
    const values = currentValues(word, edits);
    if (filters.onlyMissingLemma && values.lemma) return false;
    if (filters.partOfSpeech === "none" && values.partOfSpeech) return false;
    if (
      filters.partOfSpeech !== "" &&
      filters.partOfSpeech !== "none" &&
      values.partOfSpeech !== filters.partOfSpeech
    ) {
      return false;
    }
    if (needle) {
      const haystack = `${word.wordInContext} ${values.lemma ?? ""}`.toLocaleLowerCase();
      if (!haystack.includes(needle)) return false;
    }
    return true;
  });
}

export interface ChapterStats {
  total: number;
  missingLemma: number;
  inconsistencies: number;
}

export function chapterStats(
  words: readonly LemmaWord[],
  edits: Edits,
  rules: Rules,
): ChapterStats {
  let missingLemma = 0;
  let inconsistencies = 0;
  for (const word of words) {
    const values = currentValues(word, edits);
    if (!values.lemma) missingLemma++;
    inconsistencies += invalidFields(rules, values).length;
  }
  return { total: words.length, missingLemma, inconsistencies };
}
