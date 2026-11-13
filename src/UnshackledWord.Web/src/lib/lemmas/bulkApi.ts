import { api } from "@/lib/api";
import type { BulkQuery } from "./bulk";
import type {
  BulkApplyResponse,
  BulkOccurrencesResponse,
  BulkRevertResponse,
  BulkWordIdsResponse,
  LemmaCountsResponse,
  LemmaVerseResponse,
} from "./bulkTypes";

/** Filter part of the query string shared by the occurrence list and the id list. */
export function filterParams(
  query: BulkQuery,
  extra?: { plainWord?: string; pos?: string; original?: string },
) {
  const params = new URLSearchParams();
  for (const lemma of query.from) params.append("from", lemma);
  if (query.scope === "testament") params.set("testament", query.testament);
  if (query.scope === "book" || query.scope === "chapter")
    params.set("bookId", String(query.bookId));
  if (query.scope === "chapter") params.set("chapter", String(query.chapter));
  const pos = extra?.pos ?? query.pos;
  if (pos) params.set("partOfSpeech", pos);
  // A group request narrows to its own original, replacing the search's original filter.
  const originals = extra?.original ? [extra.original] : query.original;
  for (const key of originals) params.append("original", key);
  if (extra?.plainWord !== undefined) params.set("plainWord", extra.plainWord);
  return params;
}

export const bulkApi = {
  lemmaCounts: (q: string, limit = 20, signal?: AbortSignal) =>
    api.get<LemmaCountsResponse>(
      `/lemmas/lemma-counts?${new URLSearchParams({ q, limit: String(limit) })}`,
      signal,
    ),
  occurrences: (query: BulkQuery, page: number, pageSize: number, signal?: AbortSignal) => {
    const params = filterParams(query);
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    return api.get<BulkOccurrencesResponse>(`/lemmas/bulk/occurrences?${params}`, signal);
  },
  wordIds: (
    query: BulkQuery,
    group?: { plainWord: string; pos: string; original?: string },
    signal?: AbortSignal,
  ) => {
    const params = filterParams(
      query,
      group ? { plainWord: group.plainWord, pos: group.pos, original: group.original } : undefined,
    );
    return api.get<BulkWordIdsResponse>(`/lemmas/bulk/word-ids?${params}`, signal);
  },
  verse: (bookId: number, chapter: number, verse: number, signal?: AbortSignal) =>
    api.get<LemmaVerseResponse>(
      `/lemmas/verse?${new URLSearchParams({ bookId: String(bookId), chapter: String(chapter), verse: String(verse) })}`,
      signal,
    ),
  apply: (body: {
    from: string[];
    to: string;
    /** optional STTS tag for every listed word (must fit its part of speech) */
    stts?: string;
    wordIds: number[];
    /** parallel to wordIds: the original key the user saw ("" = added / none) */
    expectedOriginal: string[];
    note?: string;
  }) => api.post<BulkApplyResponse>("/lemmas/bulk/apply", body),
  revert: (changeSetId: number) =>
    api.post<BulkRevertResponse>(`/lemmas/bulk/${changeSetId}/revert`, {}),
};
