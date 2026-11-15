import { api } from "@/lib/api";
import type { GetWordDetailResponse, VerseMappingLink } from "@/lib/bible/types";
import type {
  BulkReplaceSearchRequest,
  BulkReplaceSearchResponse,
  RevisionHistoryResponse,
  RevisionLevel,
  RevisionLevelDetail,
  RevisionVerseMappingResponse,
  RevisionVerseRef,
  RevisionVerseResponse,
  SaveRevisionLevelRequest,
  SaveRevisionRequest,
  SaveRevisionResponse,
} from "./types";

const versePath = (ref: RevisionVerseRef) =>
  `/revision/verse/bookId/${ref.bookId}/chapterId/${ref.chapter}/verseId/${ref.verse}`;

export const revisionApi = {
  levels: (signal?: AbortSignal) => api.get<RevisionLevel[]>("/revision/levels", signal),
  verse: (ref: RevisionVerseRef, signal?: AbortSignal) =>
    api.get<RevisionVerseResponse>(versePath(ref), signal),
  saveVerse: (ref: RevisionVerseRef, body: SaveRevisionRequest) =>
    api.post<SaveRevisionResponse>(versePath(ref), body),
  history: (ref: RevisionVerseRef, level: number, signal?: AbortSignal) =>
    api.get<RevisionHistoryResponse>(`${versePath(ref)}/history?level=${level}`, signal),
  withdraw: (revisionVerseId: number) => api.delete<void>(`/revision/verse/${revisionVerseId}`),
  levelDetails: (signal?: AbortSignal) =>
    api.get<RevisionLevelDetail[]>("/revision/levels/manage", signal),
  createLevel: (body: SaveRevisionLevelRequest) =>
    api.post<RevisionLevelDetail>("/revision/levels", body),
  updateLevel: (currentLevel: number, body: SaveRevisionLevelRequest) =>
    api.put<RevisionLevelDetail>(`/revision/levels/${currentLevel}`, body),
  verseMapping: (revisionVerseId: number, signal?: AbortSignal) =>
    api.get<RevisionVerseMappingResponse>(`/revision/verseMapping/${revisionVerseId}`, signal),
  saveVerseMapping: (revisionVerseId: number, links: VerseMappingLink[]) =>
    api.put<RevisionVerseMappingResponse>(`/revision/verseMapping/${revisionVerseId}`, { links }),
  bulkReplaceSearch: (body: BulkReplaceSearchRequest, signal?: AbortSignal) =>
    api.post<BulkReplaceSearchResponse>("/revision/bulkReplace/search", body, signal),
  wordDetail: (revisionWordId: number, signal?: AbortSignal) =>
    api.get<GetWordDetailResponse>(`/revision/wordDetail/wordId/${revisionWordId}`, signal),
};
