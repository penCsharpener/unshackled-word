import { keepPreviousData, useInfiniteQuery, useQuery } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { ApiError, api } from "@/lib/api";
import type { OriginalLanguage, VerseSearchHit } from "@/lib/bible/types";
import { retryUnlessRateLimited } from "@/lib/queryClient";

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
  lemmas: GrammarLemma[];
  filters: GrammarFilter[];
}

export interface GrammarOriginalWord {
  form: string;
  lemma: string;
  strongs: string;
  code: string;
  /** Decoded parts by group (`partOfSpeech`, `tense`, `case` …), values are `bible.grammar.*` keys. */
  keys: Record<string, string>;
}

export interface GrammarMatch {
  slot: number;
  /** Elb1871 positions mapped to the original word; empty when it has no German rendering. */
  germanPositions: number[];
  original: GrammarOriginalWord;
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

/** Key names and aliases (folded). Static for now; row 0029.3f switches to the vocabulary endpoint. */
const GRAMMAR_KEYS = new Set(
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
    "Sprache",
    "Code",
    "Strong",
    "Lemma",
  ].map(foldGrammar),
);

const FILTER_TOKEN = /^-?(?:dt\.)?([^:\s]+):/i;

/** True when one token is `key:` with a known key or alias (with or without `dt.`, optionally negated with `-`). */
export function isGrammarQuery(input: string): boolean {
  return input
    .trim()
    .split(/\s+/)
    .some((token) => {
      const key = FILTER_TOKEN.exec(token)?.[1];
      return key !== undefined && GRAMMAR_KEYS.has(foldGrammar(key));
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
  "tense",
  "voice",
  "mood",
  "person",
  "case",
  "number",
  "gender",
  "degree",
] as const;

/** "Substantiv, Nominativ Singular feminin": part of speech, then the other parts; the raw code when nothing is decoded. */
export function formatGrammarWords(keys: Record<string, string>, t: TFunction, code = ""): string {
  const rest = GRAMMAR_PART_ORDER.filter((part) => keys[part]).map((part) =>
    t(`bible.grammar.${part}.${keys[part]}`),
  );
  const head = keys.partOfSpeech ? t(`bible.grammar.partOfSpeech.${keys.partOfSpeech}`) : "";
  const text = [head, rest.join(" ")].filter(Boolean).join(", ");
  return text || code;
}

/** "Nominativ" or "nicht Partizip", with "dt." for a German filter. */
export function formatGrammarFilter(filter: GrammarFilter, t: TFunction): string {
  const values = filter.values.join(t("search.grammar.or"));
  const text = filter.key === "Code" ? `${filter.key} ${values}` : values;
  const german = filter.german ? `${t("search.grammar.germanPrefix")} ${text}` : text;
  return filter.negated ? t("search.grammar.not", { text: german }) : german;
}

/** Language of an original word by its Strong's prefix (`G26` → Greek). */
export const languageOfStrongs = (strongs: string): OriginalLanguage =>
  strongs.startsWith("H") ? "hebrew" : "greek";

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

/** Matched words of one verse: Elb1871 positions and `StepGreekWords` ids. */
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
