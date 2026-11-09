import { api } from "@/lib/api";
import type {
  GetBibleStructureResponse,
  GetChapterTextResponse,
  GetStrongsOccurrencesResponse,
  GetWordDetailResponse,
  GetWordsOfChapterResponse,
} from "./types";

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
  strongsOccurrences: (strongs: string, page: number, pageSize: number, signal?: AbortSignal) =>
    api.get<GetStrongsOccurrencesResponse>(
      `/elberfelder/strongsOccurrences/strongs/${encodeURIComponent(strongs)}?page=${page}&pageSize=${pageSize}`,
      signal,
    ),
};
