import { keepPreviousData, useInfiniteQuery, useQuery } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { ApiError, api } from "@/lib/api";
import type { OriginalLanguage, VerseSearchHit } from "@/lib/bible/types";
import { retryUnlessRateLimited } from "@/lib/queryClient";
import type { GrammarFacetRequest, GrammarVocabulary } from "./grammarQuery";

/** How a slot's word was read: Strong's / lemma, German lemma, or no word (filters only). */
export type GrammarReadAs = "lemma" | "german" | "any";

export interface GrammarLemma {
  strongs: string;
  lemma: string;
  transliteration: string;
  gloss: string;
}

export interface GrammarFilter {
  /** Canonical key name, e.g. `Kasus`. */
  key: string;
  /** German labels of the values (OR); for `Code` the code patterns. */
  values: string[];
  negated: boolean;
  /** A `dt.` filter (the German word). */
  german: boolean;
}

export interface GrammarSlot {
  text: string;
  readAs: GrammarReadAs;
  /** The German lemma (lower case) of a slot read as `german`. */
  germanLemma?: string;
  lemmas: GrammarLemma[];
  filters: GrammarFilter[];
}

export interface GrammarOriginalWord {
  form: string;
  lemma: string;
  strongs: string;
  code: string;
  /** Language of the word; absent in answers of older servers (then the Strong's prefix decides). */
  language?: OriginalLanguage;
  /** Decoded parts by group (`partOfSpeech`, `tense`, `case` …), values are `bible.grammar.*` keys. */
  keys: Record<string, string>;
}

export interface GrammarMatch {
  slot: number;
  /**
   * Elb1871 positions of the match: the German words the slot constrains (German lemma or `dt.` filters), otherwise
   * the words mapped to the original word; empty when the original word has no German rendering.
   */
  germanPositions: number[];
  /** The matched original word; absent when the slot constrains only the German word. */
  original?: GrammarOriginalWord;
}

export interface GrammarHit {
  bibleBookId: number;
  chapter: number;
  verse: number;
  words: { text: string; position: number }[];
  matches: GrammarMatch[];
}

export interface SearchGrammarResponse {
  slots: GrammarSlot[];
  totalWords: number;
  totalVerses: number;
  page: number;
  pageSize: number;
  books: { bibleBookId: number; count: number }[];
  hits: GrammarHit[];
}

export type GrammarProblemCode =
  | "unknownKey"
  | "unknownValue"
  | "ambiguousValue"
  | "tooManySlots"
  | "tooManyFilters"
  | "emptyQuery"
  | "unknownWord";

/** The 400 body of a query that cannot be searched. */
export interface GrammarProblem {
  code: string;
  /** The offending token as typed (omitted for `emptyQuery`). */
  token?: string;
  /** German labels (or key names) that would have been valid. */
  candidates?: string[];
}

/** Case-, umlaut- and accent-insensitive form, like the server's `GrammarVocabulary.Fold`. */
export const foldGrammar = (text: string) =>
  text
    .trim()
    .toLowerCase()
    .replace(/ß/g, "ss")
    .normalize("NFD")
    .replace(/\p{Mn}/gu, "");

/** Key names and aliases (folded) used until the vocabulary endpoint has answered. */
const STATIC_GRAMMAR_KEYS: ReadonlySet<string> = new Set(
  [
    "Wortart",
    "Wa",
    "Tempus",
    "Zeit",
    "Diathese",
    "GenusVerbi",
    "Modus",
    "Person",
    "Kasus",
    "Fall",
    "Numerus",
    "Zahl",
    "Geschlecht",
    "Genus",
    "Steigerung",
    "Stamm",
    "Form",
    "Status",
    "Sprache",
    "Code",
    "Strong",
    "Lemma",
  ].map(foldGrammar),
);

let grammarKeys: ReadonlySet<string> = STATIC_GRAMMAR_KEYS;

/** Switches `isGrammarQuery` to the keys and aliases of the loaded vocabulary (called by `useGrammarVocabulary`). */
export function registerGrammarVocabulary(vocabulary: GrammarVocabulary): void {
  const names = vocabulary.keys.flatMap((key) => [key.name, ...key.aliases]).map(foldGrammar);
  grammarKeys = names.length > 0 ? new Set(names) : STATIC_GRAMMAR_KEYS;
}

const FILTER_TOKEN = /^-?(?:dt\.)?([^:\s]+):/i;

/**
 * True when one token is `key:` with a known key or alias (with or without `dt.`, optionally negated with `-`).
 * Uses the vocabulary once it is loaded, a static key list before.
 */
export function isGrammarQuery(input: string, keys: ReadonlySet<string> = grammarKeys): boolean {
  return input
    .trim()
    .split(/\s+/)
    .some((token) => {
      const key = FILTER_TOKEN.exec(token)?.[1];
      return key !== undefined && keys.has(foldGrammar(key));
    });
}

export const grammarVocabularyKeys = {
  vocabulary: ["search", "grammar", "vocabulary"] as const,
  facets: (q: string, slot: number, key: string) =>
    ["search", "grammar", "facets", q, slot, key] as const,
};

/** The keys and values of the grammar query (static per deployment): fetched once, never stale. */
export function useGrammarVocabulary(enabled = true) {
  return useQuery({
    queryKey: grammarVocabularyKeys.vocabulary,
    queryFn: async ({ signal }) => {
      const vocabulary = await api.get<GrammarVocabulary>(
        "/elberfelder/search/grammar/vocabulary",
        signal,
      );
      registerGrammarVocabulary(vocabulary);
      return vocabulary;
    },
    enabled,
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: Number.POSITIVE_INFINITY,
    retry: retryUnlessRateLimited,
  });
}

/** `GET api/elberfelder/search/grammar/facets`: word counts per value of one key for one slot. */
export interface GrammarFacetsResponse {
  slot: number;
  facets: { key: string; values: { key: string; labelDe: string; count: number }[] }[];
}

/** Counts per value of `key` in `slot` of `q` (an empty last value like `Kasus:` is allowed); idle for an empty query. */
export function useGrammarFacets(request: GrammarFacetRequest | null) {
  return useQuery({
    queryKey: grammarVocabularyKeys.facets(
      request?.q ?? "",
      request?.slot ?? 0,
      request?.key ?? "",
    ),
    queryFn: ({ signal }) => {
      const { q, slot, key } = request as GrammarFacetRequest;
      return api.get<GrammarFacetsResponse>(
        `/elberfelder/search/grammar/facets?${new URLSearchParams({ q, slot: String(slot), key })}`,
        signal,
      );
    },
    enabled: request !== null && request.q !== "",
    staleTime: 5 * 60_000,
    retry: noRetryOn400,
  });
}

/** The grammar problem of a failed search, or null when the error is not a readable 400 problem body. */
export function parseGrammarProblem(error: unknown): GrammarProblem | null {
  if (!(error instanceof ApiError) || error.status !== 400) return null;
  try {
    const body = JSON.parse(error.body) as Partial<GrammarProblem>;
    if (typeof body.code !== "string" || body.code === "") return null;
    return {
      code: body.code,
      ...(typeof body.token === "string" && { token: body.token }),
      ...(Array.isArray(body.candidates) && {
        candidates: body.candidates.filter((c): c is string => typeof c === "string"),
      }),
    };
  } catch {
    return null;
  }
}

/**
 * The query with the offending token replaced by a candidate: for `unknownKey` the key part
 * (`-dt.Kasu:nom` → `-dt.Kasus:nom`), for a value problem the value part (`Tempus:p` → `Tempus:Präsens`),
 * otherwise the whole token. Whitespace in a candidate is dropped (tokens are split at whitespace).
 */
export function replaceGrammarToken(
  input: string,
  problem: GrammarProblem,
  candidate: string,
): string {
  const token = problem.token;
  if (!token) return input;
  const value = candidate.replace(/\s+/g, "");
  const colon = token.indexOf(":");
  let replacement = value;
  if (colon >= 0 && problem.code === "unknownKey") {
    const prefix = /^-?(?:dt\.)?/i.exec(token)?.[0] ?? "";
    replacement = `${prefix}${value}${token.slice(colon)}`;
  } else if (colon >= 0) {
    replacement = `${token.slice(0, colon + 1)}${value}`;
  }
  const tokens = input.trim().split(/\s+/);
  const index = tokens.indexOf(token);
  if (index < 0) return input;
  tokens[index] = replacement;
  return tokens.join(" ");
}

/** Group order of the decoded grammar in words. */
const GRAMMAR_PART_ORDER = [
  "hebrewForm",
  "tense",
  "voice",
  "mood",
  "person",
  "case",
  "number",
  "gender",
  "degree",
] as const;

/** "Substantiv, Nominativ Singular feminin", "Verb, Qal, Perfekt (Qatal) 3. Person Singular maskulin": part of speech, then the other parts; the raw code when nothing is decoded. */
export function formatGrammarWords(keys: Record<string, string>, t: TFunction, code = ""): string {
  const label = (part: string) => (keys[part] ? t(`bible.grammar.${part}.${keys[part]}`) : "");
  const rest = GRAMMAR_PART_ORDER.map(label).filter(Boolean).join(" ");
  const text = [label("partOfSpeech"), label("stem"), rest, label("state")]
    .filter(Boolean)
    .join(", ");
  return text || code;
}

/** "Nominativ" or "nicht Partizip", with "dt." for a German filter. */
export function formatGrammarFilter(filter: GrammarFilter, t: TFunction): string {
  const values = filter.values.join(t("search.grammar.or"));
  const text = filter.key === "Code" ? `${filter.key} ${values}` : values;
  const german = filter.german ? `${t("search.grammar.germanPrefix")} ${text}` : text;
  return filter.negated ? t("search.grammar.not", { text: german }) : german;
}

/**
 * The German word a slot was read as, spelled as typed: the token whose folded form is the slot's German lemma
 * (also after `dt.Wort:`), else the lemma itself; the first token for servers without `germanLemma`.
 */
export function germanReading(slot: Pick<GrammarSlot, "text" | "germanLemma">): string {
  const tokens = slot.text.split(/\s+/).filter(Boolean);
  if (!slot.germanLemma) return tokens[0] ?? "";
  const lemma = foldGrammar(slot.germanLemma);
  const typed = tokens
    .map((token) => token.replace(/^(?:dt\.)?Wort:/i, ""))
    .find((token) => foldGrammar(token) === lemma);
  return typed ?? slot.germanLemma;
}

/** "geliebt" for a match that is only a German word: its words in verse order. */
export function germanMatchText(hit: Pick<GrammarHit, "words">, match: GrammarMatch): string {
  const positions = new Set(match.germanPositions);
  return hit.words
    .filter((word) => positions.has(word.position))
    .map((word) => word.text)
    .join(" ");
}

/** Language of an original word by its Strong's prefix (`G26` → Greek); Aramaic shares the `H` prefix and needs the server's `language`. */
export const languageOfStrongs = (strongs: string): OriginalLanguage =>
  strongs.startsWith("H") ? "hebrew" : "greek";

/** Language of a matched word: the server's `language`, else by Strong's prefix. */
export const languageOfOriginal = (word: Pick<GrammarOriginalWord, "language" | "strongs">) =>
  word.language ?? languageOfStrongs(word.strongs);

/** A grammar hit as a verse hit: the German words of the matches are marked. */
export function toVerseHit(hit: GrammarHit): VerseSearchHit {
  const marked = new Set(hit.matches.flatMap((m) => m.germanPositions));
  return {
    bibleBookId: hit.bibleBookId,
    chapter: hit.chapter,
    verse: hit.verse,
    tokens: hit.words.map((w) => ({ text: w.text, matched: marked.has(w.position) })),
  };
}

export const grammarSearchKeys = {
  search: (q: string, bookId: number | null) => ["search", "grammar", q, bookId ?? 0] as const,
  chapter: (q: string, bookId: number, chapter: number) =>
    ["search", "grammar", "chapter", q, bookId, chapter] as const,
  neighbours: (q: string, bookId: number, chapter: number) =>
    ["search", "grammar", "neighbours", q, bookId, chapter] as const,
};

/** Matched words of one verse: Elb1871 positions and `StepGreekWords` / `StepHebrewWords` ids (OT). */
export interface GrammarChapterVerse {
  verse: number;
  germanPositions: number[];
  originalWordIds: number[];
}

export interface SearchGrammarChapterResponse {
  verses: GrammarChapterVerse[];
  /** Matched original words in the chapter. */
  total: number;
}

export interface GrammarChapterRef {
  bibleBookId: number;
  chapter: number;
}

export interface SearchGrammarNeighboursResponse {
  previous?: GrammarChapterRef;
  next?: GrammarChapterRef;
}

const noRetryOn400 = (count: number, error: unknown) =>
  !(error instanceof ApiError && error.status === 400) && retryUnlessRateLimited(count, error);

/** Matches of a grammar query in one chapter (reader marks, roadmap 0029.2f); idle for an empty query. */
export function useGrammarChapterMatches(q: string, bookId: number, chapter: number) {
  return useQuery({
    queryKey: grammarSearchKeys.chapter(q, bookId, chapter),
    queryFn: ({ signal }) =>
      api.get<SearchGrammarChapterResponse>(
        `/elberfelder/search/grammar/chapter/${bookId}/${chapter}?${new URLSearchParams({ q })}`,
        signal,
      ),
    enabled: q !== "",
    staleTime: 5 * 60_000,
    retry: noRetryOn400,
  });
}

/** The previous and next chapter with a match of a grammar query; idle for an empty query. */
export function useGrammarNeighbours(q: string, bookId: number, chapter: number) {
  return useQuery({
    queryKey: grammarSearchKeys.neighbours(q, bookId, chapter),
    queryFn: ({ signal }) =>
      api.get<SearchGrammarNeighboursResponse>(
        `/elberfelder/search/grammar/neighbours?${new URLSearchParams({
          q,
          bibleBookId: String(bookId),
          chapter: String(chapter),
        })}`,
        signal,
      ),
    enabled: q !== "",
    staleTime: 5 * 60_000,
    retry: noRetryOn400,
  });
}

/** `GET api/elberfelder/search/grammar` for one page. */
export function searchGrammar(
  q: string,
  options: { page?: number; bookId?: number | null } = {},
  signal?: AbortSignal,
) {
  const query = new URLSearchParams({ q, page: String(options.page ?? 1) });
  if (options.bookId) query.set("book", String(options.bookId));
  return api.get<SearchGrammarResponse>(`/elberfelder/search/grammar?${query}`, signal);
}

/**
 * Grammar hits for a query, one page per fetch (page size from the API). Idle unless the query is a grammar
 * query; the previous result stays while the next one loads; no retry on 400 (the query itself is wrong).
 */
export function useGrammarSearch(q: string, bookId: number | null = null) {
  const trimmed = q.trim();
  return useInfiniteQuery({
    queryKey: grammarSearchKeys.search(trimmed, bookId),
    queryFn: ({ pageParam, signal }) => searchGrammar(trimmed, { page: pageParam, bookId }, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => {
      const total = bookId
        ? (last.books.find((b) => b.bibleBookId === bookId)?.count ?? 0)
        : last.totalVerses;
      return last.page * last.pageSize < total ? last.page + 1 : undefined;
    },
    enabled: trimmed !== "" && isGrammarQuery(trimmed),
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
    retry: noRetryOn400,
  });
}
