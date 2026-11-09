import { queryOptions, useQuery } from "@tanstack/react-query";
import { ApiError } from "@/lib/api";
import { bibleApi } from "./api";
import type { GetWordDetailResponse, MappingProvenance, MappingStatus, WordToken } from "./types";
import { mockWordDetail } from "./wordDetail.mock";

export type WordDetailSource = "mock" | "api";
/** Default is the real API; `VITE_WORD_DETAIL_SOURCE=mock` (tests) switches to the hand-written mock. */
export const wordDetailSource: WordDetailSource =
  import.meta.env.VITE_WORD_DETAIL_SOURCE === "mock" ? "mock" : "api";

export const wordDetailKeys = {
  detail: (wordId: number) => ["bible", "wordDetail", wordId] as const,
};

const STRONGS = /^([GH])0*(\d{1,5})([A-Z]{0,3})$/i;

/** "G0444" → "G444", "{H7225G}" → "H7225G", "H9003/{H7225G}" → "H7225G", "h0430g_a" → "H430G"; undefined if not a Strong's number. */
export function normalizeStrongs(value: string): string | undefined {
  const braced = /\{([^}]*)\}/.exec(value);
  const part = braced ? (braced[1] ?? "") : (value.split("/").pop() ?? "");
  const match = STRONGS.exec(part.split("_")[0] ?? "");
  if (!match) return undefined;
  return `${(match[1] ?? "").toUpperCase()}${match[2]}${(match[3] ?? "").toUpperCase()}`;
}

/** Badge state of a mapping: a human-written mapping is `manual`, an AI one `confirmed` once a human confirmed it. */
export function mappingStatus(mapping: MappingProvenance): MappingStatus {
  if (mapping.source === "human") return "manual";
  return mapping.confirmedAt ? "confirmed" : "aiSuggested";
}

/** `verseWords` (the word's verse) only feeds the mock; the API knows the verse itself. */
export function fetchWordDetail(
  word: WordToken,
  signal?: AbortSignal,
  source: WordDetailSource = wordDetailSource,
  verseWords: WordToken[] = [],
): Promise<GetWordDetailResponse> {
  if (source !== "api") return mockWordDetail(word, undefined, verseWords);
  return bibleApi.wordDetail(word.id, signal).catch((error: unknown) => {
    // 404: the word vanished (e.g. after a re-seed); show the "not linked" state instead of an error.
    if (error instanceof ApiError && error.status === 404) {
      return { wordId: word.id, wordInContext: word.text, positionInVerse: 0, readings: [] };
    }
    throw error;
  });
}

export const wordDetailQuery = (word: WordToken, verseWords: WordToken[] = []) =>
  queryOptions({
    queryKey: wordDetailKeys.detail(word.id),
    queryFn: ({ signal }) => fetchWordDetail(word, signal, wordDetailSource, verseWords),
    staleTime: Number.POSITIVE_INFINITY,
  });

/** Detail of the open word; disabled while `word` is undefined. */
export function useWordDetail(word: WordToken | undefined, verseWords: WordToken[] = []) {
  return useQuery({
    ...wordDetailQuery(word ?? { id: 0, text: "", senses: [] }, verseWords),
    enabled: word !== undefined,
  });
}
