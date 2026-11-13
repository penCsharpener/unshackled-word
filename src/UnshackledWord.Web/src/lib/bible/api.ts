import { api } from "@/lib/api";
import type {
  GetBibleStructureResponse,
  GetChapterTextResponse,
  GetStrongsOccurrencesResponse,
  GetStrongsSummaryResponse,
  GetVerseMappingResponse,
  GetWordDetailResponse,
  GetWordStatsResponse,
  GetWordsOfChapterResponse,
  OriginalsLookupOptions,
  OriginalsLookupResponse,
  SearchVersesOptions,
  SearchVersesResponse,
  StrongsOccurrenceFilters,
  VerseMappingLink,
  WordStatsTestament,
} from "./types";

const verseMappingPath = (bookId: number, chapter: number, verse: number) =>
  `/elberfelder/verseMapping/bookId/${bookId}/chapterId/${chapter}/verseId/${verse}`;

export const bibleApi = {
  structure: (signal?: AbortSignal) =>
    api.get<GetBibleStructureResponse>("/meta-bible/structure", signal),
  chapterText: (bookId: number, chapter: number, signal?: AbortSignal) =>
    api.get<GetChapterTextResponse>(
      `/elberfelder/chapterText/bookId/${bookId}/chapterId/${chapter}`,
      signal,
    ),
  chapterWords: (bookId: number, chapter: number, signal?: AbortSignal) =>
    api.get<GetWordsOfChapterResponse>(
      `/elberfelder/chapterWords/bookId/${bookId}/chapterId/${chapter}`,
      signal,
    ),
  wordDetail: (wordId: number, signal?: AbortSignal) =>
    api.get<GetWordDetailResponse>(`/elberfelder/wordDetail/wordId/${wordId}`, signal),
  verseMapping: (bookId: number, chapter: number, verse: number, signal?: AbortSignal) =>
    api.get<GetVerseMappingResponse>(verseMappingPath(bookId, chapter, verse), signal),
  saveVerseMapping: (bookId: number, chapter: number, verse: number, links: VerseMappingLink[]) =>
    api.put<GetVerseMappingResponse>(verseMappingPath(bookId, chapter, verse), { links }),
  strongsOccurrences: (
    strongs: string,
    page: number,
    pageSize: number,
    filters: StrongsOccurrenceFilters = {},
    signal?: AbortSignal,
  ) => {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters.book !== undefined) query.set("book", String(filters.book));
    if (filters.rendering !== undefined) query.set("rendering", filters.rendering);
    return api.get<GetStrongsOccurrencesResponse>(
      `/elberfelder/strongsOccurrences/strongs/${encodeURIComponent(strongs)}?${query}`,
      signal,
    );
  },
  strongsSummary: (strongs: string, signal?: AbortSignal, fresh = false) =>
    api.get<GetStrongsSummaryResponse>(
      `/elberfelder/strongsSummary/strongs/${encodeURIComponent(strongs)}`,
      signal,
      fresh ? { cache: "no-cache" } : undefined,
    ),
  wordStats: (word: string, testament: WordStatsTestament, pos?: string, signal?: AbortSignal) => {
    const query = new URLSearchParams({ word, testament });
    if (pos) query.set("pos", pos);
    return api.get<GetWordStatsResponse>(`/elberfelder/wordStats?${query}`, signal);
  },
  originalsLookup: (q: string, options: OriginalsLookupOptions = {}, signal?: AbortSignal) => {
    const query = new URLSearchParams({ q });
    if (options.top !== undefined) query.set("top", String(options.top));
    if (options.includeFrequent) query.set("includeFrequent", "true");
    return api.get<OriginalsLookupResponse>(`/elberfelder/search/originals?${query}`, signal);
  },
  searchVerses: (q: string, options: SearchVersesOptions = {}, signal?: AbortSignal) => {
    const query = new URLSearchParams({
      q,
      page: String(options.page ?? 1),
      pageSize: String(options.pageSize ?? 20),
    });
    if (options.bookId) query.set("bookId", String(options.bookId));
    return api.get<SearchVersesResponse>(`/elberfelder/search/verses?${query}`, signal);
  },
};
