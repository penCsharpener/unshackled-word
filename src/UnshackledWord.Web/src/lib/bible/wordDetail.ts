import { queryOptions, useQuery } from "@tanstack/react-query";
import { bibleApi } from "./api";
import type { GetWordDetailResponse, MappingProvenance, MappingStatus, WordToken } from "./types";
import { mockWordDetail } from "./wordDetail.mock";

export type WordDetailSource = "mock" | "api";
/** Phase 1 default is "mock"; Phase 3 (S17) flips the default to "api". */
export const wordDetailSource: WordDetailSource =
  import.meta.env.VITE_WORD_DETAIL_SOURCE === "api" ? "api" : "mock";

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
  return source === "api"
    ? bibleApi.wordDetail(word.id, signal)
    : mockWordDetail(word, undefined, verseWords);
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
