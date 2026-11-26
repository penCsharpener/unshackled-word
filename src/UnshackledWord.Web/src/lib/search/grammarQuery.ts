import type { SourceWordLemma } from "@/lib/bible/types";
import { foldGrammar } from "./grammarSearch";

/** One value of a vocabulary key (`GET api/elberfelder/search/grammar/vocabulary`). */
export interface GrammarVocabularyValue {
  /** The `grammar.*` key of the translations, or a language id. */
  key: string;
  labelDe: string;
  labelEn: string;
  aliases: string[];
  languages: string[];
}

export interface GrammarVocabularyKey {
  /** Canonical name, e.g. `Kasus`. */
  name: string;
  aliases: string[];
  /** Whether `dt.` may precede the key (and the parser accepts it today). */
  germanPrefixAllowed: boolean;
  languages: string[];
  /** `filter` limits a word, `word` (`Strong`, `Lemma`) forces how a word token is read. */
  kind: "filter" | "word";
  /** Free text value (`Code`, `Strong`, `Lemma`): no value list. */
  freeValue: boolean;
  values: GrammarVocabularyValue[];
}

export interface GrammarVocabulary {
  /** `dt.` */
  germanPrefix: string;
  germanKeysEnabled: boolean;
  keys: GrammarVocabularyKey[];
}

/** The whitespace token under the caret, split into the parts the autocomplete works on. */
export interface QueryToken {
  text: string;
  /** Offsets of the token in the input (end exclusive). */
  start: number;
  end: number;
  /** Leading `-`. */
  negated: boolean;
  /** `dt.` after the optional `-`. */
  german: boolean;
  /** Offset where the key or word starts (after `-` and `dt.`). */
  bodyStart: number;
  /** The text after `-` and `dt.` up to the caret. */
  bodyPrefix: string;
  /** The key as typed, when the token has a colon and the caret is behind it. */
  key?: string;
  /** The comma part of the value the caret is in: offsets and the typed text up to the caret. */
  part?: { start: number; end: number; prefix: string };
  /** The slot the token belongs to (0-based), as the server parser counts it. */
  slot: number;
}

const DEFAULT_GERMAN_PREFIX = "dt.";

/** The key of the vocabulary named `name` (canonical or alias, folded); with `german` only keys that allow `dt.`. */
export function findVocabularyKey(
  vocabulary: GrammarVocabulary,
  name: string,
  german = false,
): GrammarVocabularyKey | undefined {
  const folded = foldGrammar(name);
  if (folded === "") return undefined;
  return vocabulary.keys.find(
    (key) =>
      (!german || (vocabulary.germanKeysEnabled && key.germanPrefixAllowed)) &&
      [key.name, ...key.aliases].some((n) => foldGrammar(n) === folded),
  );
}

interface TokenParts {
  negated: boolean;
  german: boolean;
  /** Length of `-` and `dt.` in front of the body. */
  lead: number;
  body: string;
}

function splitLead(text: string, germanPrefix: string): TokenParts {
  const negated = text.length > 1 && text.startsWith("-");
  let lead = negated ? 1 : 0;
  const german = text.slice(lead).toLowerCase().startsWith(germanPrefix.toLowerCase());
  if (german) lead += germanPrefix.length;
  return { negated, german, lead, body: text.slice(lead) };
}

/** Whether a token opens a slot: a plain word or a `word` key (`Strong:G26`); other `key:` tokens are filters. */
function opensSlot(text: string, vocabulary: GrammarVocabulary | undefined): boolean {
  const { body, german } = splitLead(text, vocabulary?.germanPrefix ?? DEFAULT_GERMAN_PREFIX);
  const colon = body.indexOf(":");
  if (colon < 0) return true;
  const key = vocabulary ? findVocabularyKey(vocabulary, body.slice(0, colon), german) : undefined;
  return key?.kind === "word";
}

/**
 * The token under the caret (`caret` between `start` and `end`, both inclusive), or null when the caret stands in
 * whitespace. Slots are counted like the server parser: every word opens a slot, filters bind to the slot before them,
 * filters before the first word form slot 0.
 */
export function tokenAtCaret(
  input: string,
  caret: number,
  vocabulary?: GrammarVocabulary,
): QueryToken | null {
  const germanPrefix = vocabulary?.germanPrefix ?? DEFAULT_GERMAN_PREFIX;
  let slots = 0;
  for (const match of input.matchAll(/\S+/g)) {
    const text = match[0];
    const start = match.index;
    const end = start + text.length;
    if (opensSlot(text, vocabulary)) slots++;
    else if (slots === 0) slots = 1;
    if (caret < start) return null;
    if (caret > end) continue;

    const { negated, german, lead, body } = splitLead(text, germanPrefix);
    const bodyStart = start + lead;
    const token: QueryToken = {
      text,
      start,
      end,
      negated,
      german,
      bodyStart,
      bodyPrefix: input.slice(bodyStart, Math.max(caret, bodyStart)),
      slot: Math.max(slots - 1, 0),
    };
    const colon = body.indexOf(":");
    if (colon >= 0 && caret > bodyStart + colon) {
      token.key = body.slice(0, colon);
      const valueStart = bodyStart + colon + 1;
      const partStart = input.lastIndexOf(",", caret - 1) + 1;
      const from = Math.max(partStart, valueStart);
      const comma = input.indexOf(",", caret);
      const to = comma >= 0 && comma < end ? comma : end;
      token.part = { start: from, end: to, prefix: input.slice(from, caret) };
    }
    return token;
  }
  return null;
}

export type GrammarSuggestionKind = "key" | "value" | "keyedValue" | "lemma";

/** One entry of the autocomplete: `insert` replaces `start`–`end` of the input. */
export interface GrammarSuggestion {
  kind: GrammarSuggestionKind;
  insert: string;
  start: number;
  end: number;
  key?: GrammarVocabularyKey;
  value?: GrammarVocabularyValue;
  lemma?: SourceWordLemma;
}

/** The text a value is typed as: the label, or a space-less alias when the label has whitespace. */
export function valueText(value: GrammarVocabularyValue): string {
  if (!/\s/.test(value.labelDe)) return value.labelDe;
  return value.aliases.find((alias) => !/\s/.test(alias)) ?? value.labelDe.replace(/\s+/g, "");
}

const MIN_VALUE_PREFIX = 3;
const MIN_KEY_PREFIX = 2;
export const MAX_SUGGESTIONS = 8;

/** The folded names a value is found by: label (also without spaces) and aliases. */
const valueNames = (value: GrammarVocabularyValue) => [
  foldGrammar(value.labelDe),
  foldGrammar(value.labelDe).replace(/\s+/g, ""),
  ...value.aliases.map(foldGrammar),
];

/** 0 exact, 1 prefix, -1 no match. */
function valueRank(value: GrammarVocabularyValue, prefix: string): number {
  const names = valueNames(value);
  if (names.includes(prefix)) return 0;
  return names.some((name) => name.startsWith(prefix)) ? 1 : -1;
}

/** Values of `key` matching `prefix` (folded), exact matches first, else in vocabulary order. */
function matchingValues(key: GrammarVocabularyKey, prefix: string): GrammarVocabularyValue[] {
  return key.values
    .map((value, index) => ({ value, index, rank: valueRank(value, prefix) }))
    .filter((v) => v.rank >= 0)
    .sort((a, b) => a.rank - b.rank || a.index - b.index)
    .map((v) => v.value);
}

const keyAllowed = (vocabulary: GrammarVocabulary, key: GrammarVocabularyKey, token: QueryToken) =>
  (!token.german || (vocabulary.germanKeysEnabled && key.germanPrefixAllowed)) &&
  !(token.negated && key.kind === "word");

/**
 * Suggestions from the vocabulary for the token under the caret (instant, no request):
 * after `Key:` the matching values; for a bare prefix the keys it starts (2+ letters) and, from 3 letters, the values
 * it starts in their keyed form (`aor` → `Tempus:Aorist`). A list whose only entry would not change the text is empty.
 */
export function vocabularySuggestions(
  input: string,
  token: QueryToken,
  vocabulary: GrammarVocabulary,
): GrammarSuggestion[] {
  const lead = input.slice(token.start, token.bodyStart);
  let suggestions: GrammarSuggestion[];
  if (token.part && token.key !== undefined) {
    const key = findVocabularyKey(vocabulary, token.key, token.german);
    if (!key || key.freeValue || key.kind === "word") return [];
    const { start, end, prefix } = token.part;
    suggestions = matchingValues(key, foldGrammar(prefix)).map((value) => ({
      kind: "value",
      insert: valueText(value),
      start,
      end,
      key,
      value,
    }));
  } else if (token.key === undefined) {
    const prefix = foldGrammar(token.bodyPrefix);
    const end = token.text.includes(":") ? token.start + token.text.indexOf(":") : token.end;
    const keys: GrammarSuggestion[] =
      prefix.length >= MIN_KEY_PREFIX
        ? vocabulary.keys
            .filter(
              (key) =>
                keyAllowed(vocabulary, key, token) &&
                [key.name, ...key.aliases].some((n) => foldGrammar(n).startsWith(prefix)),
            )
            .map((key) => ({
              kind: "key",
              insert: token.text.includes(":") ? `${lead}${key.name}` : `${lead}${key.name}:`,
              start: token.start,
              end,
              key,
            }))
        : [];
    const values: GrammarSuggestion[] =
      prefix.length >= MIN_VALUE_PREFIX && !token.text.includes(":")
        ? vocabulary.keys
            .filter((key) => keyAllowed(vocabulary, key, token) && !key.freeValue)
            .flatMap((key) =>
              matchingValues(key, prefix).map((value) => ({
                kind: "keyedValue" as const,
                insert: `${lead}${key.name}:${valueText(value)}`,
                start: token.start,
                end: token.end,
                key,
                value,
              })),
            )
            .sort((a, b) => valueRank(a.value, prefix) - valueRank(b.value, prefix))
        : [];
    suggestions = [...keys, ...values];
  } else {
    return [];
  }
  const [only] = suggestions;
  if (suggestions.length === 1 && only && isNoOp(input, only)) return [];
  return suggestions.slice(0, MAX_SUGGESTIONS);
}

/** Whether accepting the suggestion would leave the text as it is (case aside). */
export const isNoOp = (input: string, suggestion: GrammarSuggestion) =>
  foldGrammar(input.slice(suggestion.start, suggestion.end)) === foldGrammar(suggestion.insert);

const STRONGS = /^[GgHh]\d+[A-Za-z]?$/;
export const MIN_LEMMA_PREFIX = 3;
export const MAX_LEMMA_SUGGESTIONS = 5;

/** The word to look up lemmas for: a plain word token of 3+ letters (not a key, not negated, not a Strong's number). */
export function lemmaLookupWord(token: QueryToken | null): string {
  if (!token || token.negated || token.german || token.text.includes(":")) return "";
  const word = token.text;
  return word.length >= MIN_LEMMA_PREFIX && !STRONGS.test(word) ? word : "";
}

const MATCH_RANK: Partial<Record<SourceWordLemma["matchedBy"], number>> = {
  strongs: 0,
  script: 1,
  form: 2,
  translit: 3,
};

/**
 * Lemma suggestions for a word token: lemmas found by Strong's, script, form or transliteration that occur at all;
 * the shortest transliteration completion first (`agap` → ἀγάπη before ἀγαπάω), then by occurrences. Accepting
 * inserts the Strong's number.
 */
export function lemmaSuggestions(
  token: QueryToken,
  lemmas: SourceWordLemma[],
): GrammarSuggestion[] {
  const typed = foldGrammar(token.text);
  return lemmas
    .filter((lemma) => MATCH_RANK[lemma.matchedBy] !== undefined && lemma.occurrences > 0)
    .map((lemma) => {
      const translit = foldGrammar(lemma.transliteration).replace(/[^\p{L}\p{N}]/gu, "");
      const completion = translit.startsWith(typed) ? translit.length - typed.length : 99;
      return { lemma, completion };
    })
    .sort(
      (a, b) =>
        (MATCH_RANK[a.lemma.matchedBy] ?? 9) - (MATCH_RANK[b.lemma.matchedBy] ?? 9) ||
        a.completion - b.completion ||
        b.lemma.occurrences - a.lemma.occurrences,
    )
    .slice(0, MAX_LEMMA_SUGGESTIONS)
    .map(({ lemma }) => ({
      kind: "lemma" as const,
      insert: lemma.strongs,
      start: token.start,
      end: token.end,
      lemma,
    }))
    .filter((s) => s.insert.toLowerCase() !== token.text.toLowerCase());
}

/** The input with a suggestion applied and the caret position after it. */
export function applySuggestion(
  input: string,
  suggestion: GrammarSuggestion,
): { value: string; caret: number } {
  const value = input.slice(0, suggestion.start) + suggestion.insert + input.slice(suggestion.end);
  return { value, caret: suggestion.start + suggestion.insert.length };
}

/** A facet request for the value under the caret: query (the token's value emptied), slot and canonical key. */
export interface GrammarFacetRequest {
  q: string;
  slot: number;
  key: string;
}

/** The facet request for a token whose caret is in the value of a key with a value list, else null. */
export function facetRequest(
  input: string,
  token: QueryToken | null,
  vocabulary: GrammarVocabulary,
): GrammarFacetRequest | null {
  if (!token?.part || token.key === undefined) return null;
  const key = findVocabularyKey(vocabulary, token.key, token.german);
  if (!key || key.kind !== "filter" || key.freeValue || key.values.length === 0) return null;
  // The slot's own filter on the key is ignored by the server, so the whole value is emptied (`Kasus:nom,a` → `Kasus:`).
  const emptied = input.slice(token.start, token.bodyStart + token.key.length + 1);
  const q = `${input.slice(0, token.start)}${emptied}${input.slice(token.end)}`
    .trim()
    .replace(/\s+/g, " ");
  return { q, slot: token.slot, key: key.name };
}

/** Which original languages a key or value is limited to: `greek`, `hebrew`, `aramaic`, or null for all. */
export function languageLimit(languages: string[]): "greek" | "hebrew" | "aramaic" | null {
  const greek = languages.includes("greek");
  const semitic = languages.includes("hebrew") || languages.includes("aramaic");
  if (greek && semitic) return null;
  if (greek) return "greek";
  if (languages.includes("hebrew")) return "hebrew";
  if (languages.includes("aramaic")) return "aramaic";
  return null;
}
