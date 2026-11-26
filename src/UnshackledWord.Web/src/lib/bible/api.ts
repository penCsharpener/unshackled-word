import { api } from "@/lib/api";
import type {
  AcceptTskProposalResponse,
  AddTskCurationRequest,
  DeleteAddedTskCurationRequest,
  DeleteTskWordLinkRequest,
  GetBibleStructureResponse,
  GetChapterCrossReferencesResponse,
  GetChapterOriginalsResponse,
  GetChapterTextResponse,
  GetGreekEditionsResponse,
  GetPassageTextResponse,
  GetSimilarVersesResponse,
  GetStrongsOccurrencesResponse,
  GetStrongsSummaryResponse,
  GetVerseCrossReferenceTextsResponse,
  GetVerseMappingResponse,
  GetWordDetailResponse,
  GetWordStatsResponse,
  GetWordsOfChapterResponse,
  MyTskProposalsResponse,
  OriginalsLookupOptions,
  OriginalsLookupResponse,
  ParseReferenceResponse,
  SaveTskCurationRequest,
  SaveTskWordLinkRequest,
  SearchVersesOptions,
  SearchVersesResponse,
  SourceWordsResponse,
  StrongsOccurrenceFilters,
  TskCurationResponse,
  TskProposal,
  TskProposalSubmitted,
  TskProposalsSubmitted,
  TskReviewProposalsResponse,
  TskWordLinkResponse,
  VerseMappingLink,
  VerseSimilarVerses,
  WordStatsTestament,
} from "./types";

/** The unrevised Elb1871 level; sent without a `level` parameter. */
/** Default reader level (position of Elb1871 / Elberfelder 2027); omitted from the query. */
export const BASE_LEVEL = 200;
/** Pseudo level: only the unrevised Elb1871 text, no variant. */
export const ELB1871_ONLY_LEVEL = 0;
const levelQuery = (level: number) => (level === BASE_LEVEL ? "" : `?level=${level}`);

const verseMappingPath = (bookId: number, chapter: number, verse: number) =>
  `/elberfelder/verseMapping/bookId/${bookId}/chapterId/${chapter}/verseId/${verse}`;

export const bibleApi = {
  structure: (signal?: AbortSignal) =>
    api.get<GetBibleStructureResponse>("/meta-bible/structure", signal),
  chapterText: (bookId: number, chapter: number, level = BASE_LEVEL, signal?: AbortSignal) =>
    api.get<GetChapterTextResponse>(
      `/elberfelder/chapterText/bookId/${bookId}/chapterId/${chapter}${levelQuery(level)}`,
      signal,
    ),
  chapterWords: (bookId: number, chapter: number, level = BASE_LEVEL, signal?: AbortSignal) =>
    api.get<GetWordsOfChapterResponse>(
      `/elberfelder/chapterWords/bookId/${bookId}/chapterId/${chapter}${levelQuery(level)}`,
      signal,
    ),
  verseCrossReferenceTexts: (
    bookId: number,
    chapter: number,
    verse: number,
    level = BASE_LEVEL,
    signal?: AbortSignal,
  ) =>
    api.get<GetVerseCrossReferenceTextsResponse>(
      `/elberfelder/crossReferenceTexts/bookId/${bookId}/chapterId/${chapter}/verseId/${verse}${levelQuery(level)}`,
      signal,
    ),
  /**
   * `includeHidden` (curate mode) is honoured by the server for Admin/Maintainer only: hidden links come with `rating`
   * `hide`. `level` (reader level, omitted for the default) adds `word.mappedWords` on revised verses (roadmap 0024.5).
   */
  chapterCrossReferences: (
    bookId: number,
    chapter: number,
    signal?: AbortSignal,
    includeHidden = false,
    level = BASE_LEVEL,
  ) => {
    const query = new URLSearchParams();
    if (includeHidden) query.set("includeHidden", "true");
    if (level !== BASE_LEVEL) query.set("level", String(level));
    const search = query.size > 0 ? `?${query}` : "";
    return api.get<GetChapterCrossReferencesResponse>(
      `/elberfelder/chapterCrossReferences/bookId/${bookId}/chapterId/${chapter}${search}`,
      signal,
    );
  },
  /** Maintainer: rating and comment of one TSK link (roadmap 0024.3). */
  saveTskCuration: (body: SaveTskCurationRequest) =>
    api.put<TskCurationResponse | TskProposalSubmitted>("/tsk/curation", body),
  /** Maintainer: a new link from a verse to a parsed German reference (roadmap 0024.4). */
  addTskCuration: (body: AddTskCurationRequest) =>
    api.post<TskCurationResponse | TskProposalSubmitted>("/tsk/curation/added", body),
  /** Maintainer: removes an added link (204); the key goes in the body. */
  deleteAddedTskCuration: (body: DeleteAddedTskCurationRequest) =>
    api.delete<TskProposalSubmitted | undefined>("/tsk/curation/added", undefined, body),
  /** Maintainer: links refs of one source verse to a word range of it (roadmap 0024.5). */
  saveTskWordLink: (body: SaveTskWordLinkRequest) =>
    api.put<TskWordLinkResponse | TskProposalsSubmitted>("/tsk/curation/word", body),
  /** Maintainer: removes the word link of refs (key list in the body); answers the states like the PUT. */
  deleteTskWordLink: (body: DeleteTskWordLinkRequest) =>
    api.delete<TskWordLinkResponse | TskProposalsSubmitted>("/tsk/curation/word", undefined, body),
  /** Logged in: the caller's own TSK suggestions, newest first (roadmap 0024.6). */
  myTskProposals: (signal?: AbortSignal) =>
    api.get<MyTskProposalsResponse>("/tsk/proposals/mine", signal),
  /** Admin/Maintainer: pending TSK suggestions in source verse order. */
  tskReviewProposals: (signal?: AbortSignal) =>
    api.get<TskReviewProposalsResponse>("/maintainer/tsk/proposals", signal),
  /** Admin/Maintainer: applies a suggestion; 400 when stale unless `force`. */
  acceptTskProposal: (id: number, force: boolean) =>
    api.post<AcceptTskProposalResponse>(
      `/maintainer/tsk/proposals/${id}/accept`,
      force ? { force: true } : {},
    ),
  /** Admin/Maintainer: rejects a suggestion with an optional reason (max 500). */
  rejectTskProposal: (id: number, reason?: string) =>
    api.post<TskProposal>(`/maintainer/tsk/proposals/${id}/reject`, reason ? { reason } : {}),
  /** Similar verses of every verse of a chapter (roadmap 0027.3), `limit` hits per verse (1–30). */
  similarVerses: (bookId: number, chapter: number, limit: number, signal?: AbortSignal) =>
    api.get<GetSimilarVersesResponse>(
      `/elberfelder/similarVerses/${bookId}/${chapter}?limit=${limit}`,
      signal,
    ),
  /** Up to 30 similar verses of one verse ("mehr anzeigen"). */
  similarVersesOfVerse: (hebRefId: number, signal?: AbortSignal) =>
    api.get<VerseSimilarVerses>(`/elberfelder/similarVerses/verse/${hebRefId}`, signal),
  chapterOriginals: (bookId: number, chapter: number, signal?: AbortSignal) =>
    api.get<GetChapterOriginalsResponse>(
      `/elberfelder/chapterOriginals/bookId/${bookId}/chapterId/${chapter}`,
      signal,
    ),
  /** Catalog of the Greek text editions (roadmap 0025), public and cached for a day by the server. */
  greekEditions: (signal?: AbortSignal) =>
    api.get<GetGreekEditionsResponse>("/greekEditions", signal),
  wordDetail: (wordId: number, signal?: AbortSignal) =>
    api.get<GetWordDetailResponse>(`/elberfelder/wordDetail/wordId/${wordId}`, signal),
  verseMapping: (bookId: number, chapter: number, verse: number, signal?: AbortSignal) =>
    api.get<GetVerseMappingResponse>(verseMappingPath(bookId, chapter, verse), signal),
  saveVerseMapping: (
    bookId: number,
    chapter: number,
    verse: number,
    links: VerseMappingLink[],
    clearParts?: boolean,
  ) =>
    api.put<GetVerseMappingResponse>(verseMappingPath(bookId, chapter, verse), {
      links,
      ...(clearParts ? { clearParts: true } : {}),
    }),
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
    if (filters.grammar !== undefined) query.set("grammar", filters.grammar);
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
  sourceWordsLookup: (q: string, limit?: number, signal?: AbortSignal) => {
    const query = new URLSearchParams({ q });
    if (limit !== undefined) query.set("limit", String(limit));
    return api.get<SourceWordsResponse>(`/elberfelder/search/sourcewords?${query}`, signal);
  },
  passageText: (spans: string, level = BASE_LEVEL, signal?: AbortSignal) =>
    api.get<GetPassageTextResponse>(
      `/passageText?${new URLSearchParams({ spans, level: String(level) })}`,
      signal,
    ),
  parseReference: (q: string, signal?: AbortSignal) =>
    api.get<ParseReferenceResponse>(`/references/parse?${new URLSearchParams({ q })}`, signal),
  searchVerses: (q: string, options: SearchVersesOptions = {}, signal?: AbortSignal) => {
    const query = new URLSearchParams({
      q,
      page: String(options.page ?? 1),
      pageSize: String(options.pageSize ?? 20),
    });
    if (options.bookId) query.set("bookId", String(options.bookId));
    if (options.level && options.level !== BASE_LEVEL) query.set("level", String(options.level));
    return api.get<SearchVersesResponse>(`/elberfelder/search/verses?${query}`, signal);
  },
};
