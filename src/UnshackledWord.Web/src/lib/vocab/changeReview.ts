import type { VocabLang } from "./api";
import type { VocabChangedEntry, VocabChangeRow } from "./changeApi";

/** One change of a card as the review list shows it: a label, the values before and after, and a note. */
export interface ChangeReviewChange {
  /** Stable React key: the change time and kind. */
  key: string;
  kind: string;
  before?: string[];
  after?: string[];
  note?: string;
}

/** One card in the review list. A card can carry several changes. */
export interface ChangeReviewItem {
  entryId: number;
  lemma: string;
  form?: string;
  lang: VocabLang;
  removed: boolean;
  changes: ChangeReviewChange[];
}

/** Translates a `learn.changes.*` key; the options fill its placeholders. */
export type ChangeTranslate = (key: string, options?: Record<string, string>) => string;

/** The string values of one JSON field: a string array, or a single string. Anything else is no values. */
function valuesOf(json: Record<string, unknown> | null | undefined, field: string): string[] {
  const value = json?.[field];
  if (Array.isArray(value)) return value.filter((item): item is string => typeof item === "string");
  if (typeof value === "string" && value !== "") return [value];
  return [];
}

type ChangeWithoutKey = Omit<ChangeReviewChange, "key">;

function changeOf(
  row: VocabChangeRow,
  entry: VocabChangedEntry,
  translate: ChangeTranslate,
): ChangeWithoutKey | null {
  // Removed cards show one label, the reason goes into the note.
  if (entry.status === "removed") {
    const removedLabel = translate("learn.changes.removed");
    if (row.kind === "merge") {
      const target = row.targetLemma ?? "";
      return {
        kind: removedLabel,
        note: translate("learn.changes.mergedInto", { lemma: target }),
      };
    }
    if (row.kind === "retire") {
      return { kind: removedLabel, note: translate("learn.changes.retiredNote") };
    }
    if (row.kind === "delete") {
      return { kind: removedLabel, note: translate("learn.changes.deletedNote") };
    }
    return { kind: removedLabel };
  }

  switch (row.kind) {
    case "meanings":
      return {
        kind: translate("learn.changes.kind.meanings"),
        before: valuesOf(row.before, "meanings"),
        after: valuesOf(row.after, "meanings"),
      };
    case "form":
      return {
        kind: translate("learn.changes.kind.form"),
        before: valuesOf(row.before, "form"),
        after: valuesOf(row.after, "form"),
      };
    case "lemma":
      return {
        kind: translate("learn.changes.kind.lemma"),
        before: valuesOf(row.before, "lemma"),
        after: valuesOf(row.after, "lemma"),
      };
    case "keys":
      return {
        kind: translate("learn.changes.kind.keys"),
        before: valuesOf(row.before, "keys"),
        after: valuesOf(row.after, "keys"),
      };
    case "split":
      return {
        kind: translate("learn.changes.kind.split"),
        before: valuesOf(row.before, "keys"),
        after: valuesOf(row.after, "keys"),
      };
    case "merge": {
      // Kept card: both old forms, this card's own and the merged-away one's. The own form stays.
      const own = entry.dictionaryForm || entry.lemma;
      const other = row.targetDictionaryForm || row.targetLemma || "";
      return {
        kind: translate("learn.changes.kind.merge"),
        before: other ? [own, other] : [own],
        after: [own],
      };
    }
    case "retire":
      return {
        kind: translate("learn.changes.kind.retire"),
        note: translate("learn.changes.retiredNote"),
      };
    case "delete":
      return {
        kind: translate("learn.changes.kind.delete"),
        note: translate("learn.changes.deletedNote"),
      };
    default:
      return null;
  }
}

/** Maps `GET api/vocab/changes` items to the review list: one item per card, labels translated. */
export function toChangeReviewItems(
  entries: VocabChangedEntry[],
  translate: ChangeTranslate,
): ChangeReviewItem[] {
  return entries.map((entry) => ({
    entryId: entry.entryId,
    lemma: entry.lemma,
    form: entry.dictionaryForm || undefined,
    lang: entry.lang === "he" ? "he" : "grc",
    removed: entry.status === "removed",
    changes: entry.changes.flatMap((row) => {
      const change = changeOf(row, entry, translate);
      return change ? [{ ...change, key: `${row.changedAt}-${row.kind}` }] : [];
    }),
  }));
}
