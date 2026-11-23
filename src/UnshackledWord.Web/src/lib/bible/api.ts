import { api } from "@/lib/api";
import type {
  GetBibleStructureResponse,
  GetChapterCrossReferencesResponse,
  GetChapterOriginalsResponse,
  GetChapterTextResponse,
  GetPassageTextResponse,
  GetStrongsOccurrencesResponse,
  GetStrongsSummaryResponse,
  GetVerseCrossReferenceTextsResponse,
  GetVerseMappingResponse,
  GetWordDetailResponse,
  GetWordStatsResponse,
  GetWordsOfChapterResponse,
  OriginalsLookupOptions,
  OriginalsLookupResponse,
  ParseReferenceResponse,
  SaveTskCurationRequest,
  SearchVersesOptions,
  SearchVersesResponse,
  SourceWordsResponse,
  StrongsOccurrenceFilters,
  TskCurationResponse,
  VerseMappingLink,
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
  /** `includeHidden` (curate mode) is honoured by the server for Admin/Maintainer only: hidden links come with `rating` `hide`. */
  chapterCrossReferences: (
    bookId: number,
    chapter: number,
    signal?: AbortSignal,
    includeHidden = false,
  ) =>
    api.get<GetChapterCrossReferencesResponse>(
      `/elberfelder/chapterCrossReferences/bookId/${bookId}/chapterId/${chapter}${includeHidden ? "?includeHidden=true" : ""}`,
      signal,
    ),
  /** Maintainer: rating and comment of one TSK link (roadmap 0024.3). */
  saveTskCuration: (body: SaveTskCurationRequest) =>
    api.put<TskCurationResponse>("/tsk/curation", body),
  chapterOriginals: (bookId: number, chapter: number, signal?: AbortSignal) =>
    api.get<GetChapterOriginalsResponse>(
      `/elberfelder/chapterOriginals/bookId/${bookId}/chapterId/${chapter}`,
      signal,
    ),
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
