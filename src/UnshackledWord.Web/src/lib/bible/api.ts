import { api } from "@/lib/api";
import type { GetBibleStructureResponse, GetChapterTextResponse } from "./types";

export const bibleApi = {
  structure: (signal?: AbortSignal) =>
    api.get<GetBibleStructureResponse>("/meta-bible/structure", signal),
  chapterText: (bookId: number, chapter: number, signal?: AbortSignal) =>
    api.get<GetChapterTextResponse>(
      `/elberfelder/chapterText/bookId/${bookId}/chapterId/${chapter}`,
      signal,
    ),
};
