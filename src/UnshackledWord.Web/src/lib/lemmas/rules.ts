import {
  EDITABLE_FIELDS,
  MORPH_FIELDS,
  type MorphField,
  type RuleEntry,
  type WordValues,
} from "./types";

export interface PosRule {
  fields: ReadonlySet<string>;
  byVerbForm: ReadonlyMap<string, ReadonlySet<string>>;
}
export type Rules = ReadonlyMap<string, PosRule>;

/** Same table as the backend (ElbLemmaValueMaps.Rules); only used while the response carries no rules. */
const FALLBACK: RuleEntry[] = [
  {
    partOfSpeech: "VERB",
    fields: ["verbForm"],
    byVerbForm: [
      { verbForm: "Fin", fields: ["tense", "person", "number", "mood"] },
      { verbForm: "Inf", fields: [] },
      { verbForm: "Part", fields: ["tense", "case", "gender", "number", "degree"] },
    ],
  },
  { partOfSpeech: "NOUN", fields: ["number", "case", "gender"] },
  { partOfSpeech: "PROPN", fields: ["number", "case", "gender"] },
  { partOfSpeech: "ADJ", fields: ["degree", "number", "case", "gender"] },
  { partOfSpeech: "ADV", fields: ["degree"] },
  { partOfSpeech: "DET", fields: ["number", "case", "gender"] },
  { partOfSpeech: "PRON", fields: ["person", "number", "case", "gender"] },
];
FALLBACK.push({ ...(FALLBACK[0] as RuleEntry), partOfSpeech: "AUX" });

export function buildRules(entries?: readonly RuleEntry[]): Rules {
  const source = entries && entries.length > 0 ? entries : FALLBACK;
  return new Map(
    source.map((entry) => [
      entry.partOfSpeech,
      {
        fields: new Set(entry.fields),
        byVerbForm: new Map((entry.byVerbForm ?? []).map((v) => [v.verbForm, new Set(v.fields)])),
      },
    ]),
  );
}

/** Morphology fields that apply to a word with this part of speech and verb form. */
export function applicableFields(
  rules: Rules,
  partOfSpeech: string | null | undefined,
  verbForm: string | null | undefined,
): ReadonlySet<MorphField> {
  const rule = partOfSpeech ? rules.get(partOfSpeech) : undefined;
  const result = new Set<MorphField>();
  if (!rule) return result;
  const add = (names: ReadonlySet<string>) => {
    for (const field of MORPH_FIELDS) if (names.has(field)) result.add(field);
  };
  add(rule.fields);
  if (verbForm) {
    const extra = rule.byVerbForm.get(verbForm);
    if (extra) add(extra);
  }
  return result;
}

/** Fields that hold a value although the rules do not allow it (typically AI errors). */
export function invalidFields(rules: Rules, values: WordValues): MorphField[] {
  const allowed = applicableFields(rules, values.partOfSpeech, values.verbForm);
  return MORPH_FIELDS.filter((field) => values[field] !== null && !allowed.has(field));
}

/** Clears every morphology field the rules no longer allow. */
export function clearInapplicable(rules: Rules, values: WordValues): WordValues {
  const next = { ...values };
  for (const field of invalidFields(rules, values)) next[field] = null;
  return next;
}

export { EDITABLE_FIELDS };
