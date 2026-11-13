import { buildLabels } from "@/lib/lemmas/labels";
import type { LemmaWord, ValueMapsResponse } from "@/lib/lemmas/types";

export const valueMaps: ValueMapsResponse = {
  fields: [
    {
      field: "partOfSpeech",
      values: [
        { code: "NOUN", labelDe: "substantiv", labelEn: "noun" },
        { code: "VERB", labelDe: "verb", labelEn: "verb" },
        { code: "PRON", labelDe: "pronomen", labelEn: "pronoun" },
        { code: "ADP", labelDe: "präposition", labelEn: "adposition" },
      ],
    },
    { field: "verbForm", values: [{ code: "Fin", labelDe: "finit", labelEn: "finite" }] },
    {
      field: "tense",
      values: [
        { code: "Pres", labelDe: "Präsens", labelEn: "present" },
        { code: "Past", labelDe: "Präteritum", labelEn: "past" },
      ],
    },
    { field: "case", values: [{ code: "Dat", labelDe: "Dativ", labelEn: "dative" }] },
    { field: "number", values: [{ code: "S", labelDe: "Singular", labelEn: "singular" }] },
    {
      field: "stts",
      values: [
        { code: "NN", labelDe: "normales Nomen", labelEn: "common noun" },
        { code: "VVFIN", labelDe: "finites Vollverb", labelEn: "finite full verb" },
        { code: "VVINF", labelDe: "Infinitiv Vollverb", labelEn: "full verb infinitive" },
        { code: "APPR", labelDe: "Präposition", labelEn: "preposition" },
      ],
    },
  ],
  sttsTags: [
    { tag: "NN", upos: "NOUN", allowedUpos: ["NOUN", "PROPN"] },
    { tag: "VVFIN", upos: "VERB", allowedUpos: ["VERB", "AUX"] },
    { tag: "VVINF", upos: "VERB", allowedUpos: ["VERB", "AUX"] },
    { tag: "APPR", upos: "ADP", allowedUpos: ["ADP"] },
  ],
  sttsProposals: [
    { partOfSpeech: "VERB", verbForm: "Fin", stts: "VVFIN" },
    { partOfSpeech: "VERB", stts: "VVFIN" },
    { partOfSpeech: "NOUN", stts: "NN" },
    { partOfSpeech: "ADP", stts: "APPR" },
  ],
  rules: [
    {
      partOfSpeech: "VERB",
      fields: ["verbForm"],
      byVerbForm: [
        { verbForm: "Fin", fields: ["tense", "person", "number", "mood"] },
        { verbForm: "Inf", fields: [] },
      ],
    },
    { partOfSpeech: "NOUN", fields: ["number", "case", "gender"] },
    { partOfSpeech: "ADP", fields: [] },
  ],
};

export const labelsDe = buildLabels(valueMaps, "de");

export function word(wordId: number, patch: Partial<LemmaWord> = {}): LemmaWord {
  return {
    wordId,
    verse: 1,
    positionInVerse: wordId,
    wordInContext: `Wort${wordId}`,
    hasMorphology: true,
    ...patch,
  };
}

export const chapterWords: LemmaWord[] = [
  word(1, { lemma: "Licht", partOfSpeech: "NOUN", number: "S", case: "Dat" }),
  word(2, { lemma: "scheinen", partOfSpeech: "VERB", verbForm: "Fin", tense: "Pres" }),
  word(3, { verse: 2, partOfSpeech: "ADP" }),
];
