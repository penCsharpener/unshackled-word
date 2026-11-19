import { useQuery } from "@tanstack/react-query";
import { ApiError } from "@/lib/api";
import { bibleApi } from "./api";

/** The server accepts at most 5 words of at most 50 characters. */
export const SOURCE_WORDS_MAX_TOKENS = 5;
const MAX_TOKEN_LENGTH = 50;

const SCRIPT = /[\p{Script=Hebrew}\p{Script=Greek}]/u;
const STRONGS = /^[GgHh]0*\d{1,4}[A-Za-z]?$/;

/** A search word the source word lookup answers: Hebrew / Greek script or a Strong's number (`G1577`, `h430`). */
export const isSourceWord = (word: string): boolean => SCRIPT.test(word) || STRONGS.test(word);

/**
 * Splits a (normalised, space separated) query into the words for the source word lookup and the rest,
 * which stays the German originals lookup: `Gott θεός G3056` gives `θεός G3056` and `Gott`.
 */
export function splitSourceWords(words: string): { source: string; german: string } {
  const parts = words.split(/\s+/).filter((word) => word !== "");
  return {
    source: parts.filter(isSourceWord).slice(0, SOURCE_WORDS_MAX_TOKENS).join(" "),
    german: parts.filter((word) => !isSourceWord(word)).join(" "),
  };
}

export const sourceWordsKeys = {
  lookup: (q: string, limit: number | undefined) =>
    ["bible", "sourceWordsLookup", q, limit ?? null] as const,
};

const isValid = (q: string): boolean => {
  const words = q.split(/\s+/).filter((word) => word !== "");
  return (
    words.length > 0 &&
    words.length <= SOURCE_WORDS_MAX_TOKENS &&
    words.every((word) => word.length <= MAX_TOKEN_LENGTH)
  );
};

/**
 * Hebrew / Greek words by script (diacritics optional) or Strong's number: per word the lemma entries and
 * the matching inflected forms. Idle for an empty or invalid query; no retry on 400 / 503 (facts not computed yet).
 */
export function useSourceWordsLookup(q: string, limit?: number) {
  const trimmed = q.trim();
  return useQuery({
    queryKey: sourceWordsKeys.lookup(trimmed, limit),
    queryFn: ({ signal }) => bibleApi.sourceWordsLookup(trimmed, limit, signal),
    enabled: isValid(trimmed),
    staleTime: 60_000,
    retry: (count, error) =>
      !(error instanceof ApiError && (error.status === 400 || error.status === 503)) && count < 1,
  });
}
