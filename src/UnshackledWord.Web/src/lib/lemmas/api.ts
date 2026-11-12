import { api } from "@/lib/api";
import type {
  LemmaChapterResponse,
  LemmaWordChange,
  UpdateLemmaChapterResponse,
  ValueMapsResponse,
} from "./types";

export const lemmaApi = {
  valueMaps: (signal?: AbortSignal) => api.get<ValueMapsResponse>("/lemmas/value-maps", signal),
  chapter: (bookId: number, chapter: number, signal?: AbortSignal) =>
    api.get<LemmaChapterResponse>(`/lemmas/chapter?bookId=${bookId}&chapter=${chapter}`, signal),
  saveChapter: (changes: LemmaWordChange[]) =>
    api.put<UpdateLemmaChapterResponse>("/lemmas/chapter", { changes }),
};
