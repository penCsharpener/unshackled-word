import { api } from "@/lib/api";
import type {
  GetBibleStructureResponse,
  GetChapterTextResponse,
  GetStrongsOccurrencesResponse,
  GetVerseMappingResponse,
  GetWordDetailResponse,
  GetWordsOfChapterResponse,
  VerseMappingLink,
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
  strongsOccurrences: (strongs: string, page: number, pageSize: number, signal?: AbortSignal) =>
    api.get<GetStrongsOccurrencesResponse>(
      `/elberfelder/strongsOccurrences/strongs/${encodeURIComponent(strongs)}?page=${page}&pageSize=${pageSize}`,
      signal,
    ),
};
