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
  strongsSummary: (strongs: string, signal?: AbortSignal) =>
    api.get<GetStrongsSummaryResponse>(
      `/elberfelder/strongsSummary/strongs/${encodeURIComponent(strongs)}`,
      signal,
    ),
  wordStats: (word: string, testament: WordStatsTestament, signal?: AbortSignal) =>
    api.get<GetWordStatsResponse>(
      `/elberfelder/wordStats?${new URLSearchParams({ word, testament })}`,
      signal,
    ),
};
