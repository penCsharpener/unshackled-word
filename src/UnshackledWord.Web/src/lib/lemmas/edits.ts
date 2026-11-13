import { clearInapplicable, type Rules } from "./rules";
import {
  EDITABLE_FIELDS,
  type EditableField,
  type LemmaWord,
  type LemmaWordChange,
  MORPH_FIELDS,
  type WordValues,
} from "./types";

/** Dirty rows only: wordId -> full editable values of the row. */
export type Edits = ReadonlyMap<number, WordValues>;

export function valuesOf(word: LemmaWord): WordValues {
  const values = {} as WordValues;
  for (const field of EDITABLE_FIELDS) values[field] = word[field] ?? null;
  return values;
}

export function dirtyFields(base: WordValues, current: WordValues): EditableField[] {
  return EDITABLE_FIELDS.filter((field) => base[field] !== current[field]);
}

/**
 * Sets one value. Changing the part of speech or verb form drops the fields that no longer apply.
 * A row that ends up equal to the loaded values is no longer dirty.
 */
export function applyEdit(
  edits: Edits,
  word: LemmaWord,
  rules: Rules,
  field: EditableField,
  value: string | null,
): Edits {
  const base = valuesOf(word);
  const current = edits.get(word.wordId) ?? base;
  const normalized = value === null || value === "" ? null : value;
  if (current[field] === normalized) return edits;
  let next: WordValues = { ...current, [field]: normalized };
  if (field === "partOfSpeech" || field === "verbForm") next = clearInapplicable(rules, next);
  const result = new Map(edits);
  if (dirtyFields(base, next).length === 0) result.delete(word.wordId);
  else result.set(word.wordId, next);
  return result;
}

export function resetRow(edits: Edits, wordId: number): Edits {
  if (!edits.has(wordId)) return edits;
  const result = new Map(edits);
  result.delete(wordId);
  return result;
}

export function dropEdits(edits: Edits, wordIds: Iterable<number>): Edits {
  const result = new Map(edits);
  for (const id of wordIds) result.delete(id);
  return result;
}

export interface BuiltChanges {
  changes: LemmaWordChange[];
  /** Dirty words whose lemma or part of speech is empty: the backend rejects those. */
  incompleteWordIds: number[];
}

/**
 * `bases` are the loaded values per word: the STTS tag is only sent when the user changed it (null = keep the
 * stored tag), so a row that keeps a legacy pair is not rejected when only its part of speech changes.
 */
export function buildChanges(edits: Edits, bases?: ReadonlyMap<number, WordValues>): BuiltChanges {
  const changes: LemmaWordChange[] = [];
  const incompleteWordIds: number[] = [];
  for (const [wordId, values] of edits) {
    const lemma = values.lemma?.trim() ?? "";
    if (!lemma || !values.partOfSpeech) {
      incompleteWordIds.push(wordId);
      continue;
    }
    const change: LemmaWordChange = { wordId, lemma, partOfSpeech: values.partOfSpeech };
    if (values.stts !== null && values.stts !== bases?.get(wordId)?.stts) change.stts = values.stts;
    for (const field of MORPH_FIELDS) {
      const value = values[field];
      if (value !== null) change[field] = value;
    }
    changes.push(change);
  }
  return { changes, incompleteWordIds };
}

export function chunk<T>(items: readonly T[], size: number): T[][] {
  const result: T[][] = [];
  for (let i = 0; i < items.length; i += size) result.push(items.slice(i, i + size));
  return result;
}
