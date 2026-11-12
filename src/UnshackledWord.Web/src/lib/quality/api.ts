import { api } from "@/lib/api";
import type {
  CheckResponse,
  CoverageMetric,
  CoverageResponse,
  GermanDetailResponse,
  GermanSearchResponse,
  HitsResponse,
  OriginalDetailResponse,
  OriginalSearchResponse,
  OverviewResponse,
  QualityFacets,
  RefreshResponse,
  UnmappedResponse,
  UnmappedSide,
} from "./types";

const BASE = "/dataQuality";

type QueryValue = string | number | boolean | undefined | null;

/** `?a=1&b=2` from the defined, non-empty values; empty string when there are none. */
export function toQuery(values: Record<string, QueryValue>): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value === undefined || value === null || value === "") continue;
    params.set(key, String(value));
  }
  const text = params.toString();
  return text ? `?${text}` : "";
}

const facetQuery = (facets: QualityFacets) => ({
  testament: facets.testament,
  bookId: facets.bookId,
});

export interface CheckQuery extends QualityFacets {
  page: number;
  pageSize: number;
  /** pos, lang, class, chapter, minCount, group, content ... (the `f` facets of the URL) */
  filters: Record<string, string>;
}

export interface HitsQuery extends QualityFacets {
  strongs?: string;
  lemma?: string;
  pos?: string;
  page: number;
  pageSize: number;
}

export const qualityApi = {
  overview: (facets: QualityFacets, signal?: AbortSignal) =>
    api.get<OverviewResponse>(`${BASE}/overview${toQuery(facetQuery(facets))}`, signal),
  unmapped: (
    side: UnmappedSide,
    facets: QualityFacets,
    content: boolean,
    top: number,
    signal?: AbortSignal,
  ) =>
    api.get<UnmappedResponse>(
      `${BASE}/unmapped/${side}${toQuery({ ...facetQuery(facets), content, top })}`,
      signal,
    ),
  coverage: (metric: CoverageMetric, facets: QualityFacets, signal?: AbortSignal) =>
    api.get<CoverageResponse>(
      `${BASE}/coverage${toQuery({ metric, testament: facets.testament })}`,
      signal,
    ),
  check: (code: string, query: CheckQuery, signal?: AbortSignal) =>
    api.get<CheckResponse>(
      `${BASE}/checks/${encodeURIComponent(code)}${toQuery({
        ...query.filters,
        ...facetQuery(query),
        page: query.page,
        pageSize: query.pageSize,
      })}`,
      signal,
    ),
  originalSearch: (q: string, signal?: AbortSignal) =>
    api.get<OriginalSearchResponse>(`${BASE}/original/search${toQuery({ q, limit: 20 })}`, signal),
  originalDetail: (strongs: string, signal?: AbortSignal) =>
    api.get<OriginalDetailResponse>(`${BASE}/original/${encodeURIComponent(strongs)}`, signal),
  germanSearch: (q: string, pos: string | undefined, signal?: AbortSignal) =>
    api.get<GermanSearchResponse>(`${BASE}/german/search${toQuery({ q, pos, limit: 20 })}`, signal),
  germanDetail: (lemma: string, pos: string | undefined, signal?: AbortSignal) =>
    api.get<GermanDetailResponse>(`${BASE}/german${toQuery({ lemma, pos })}`, signal),
  hits: (query: HitsQuery, signal?: AbortSignal) =>
    api.get<HitsResponse>(
      `${BASE}/hits${toQuery({
        ...facetQuery(query),
        strongs: query.strongs,
        lemma: query.lemma,
        pos: query.pos,
        page: query.page,
        pageSize: query.pageSize,
      })}`,
      signal,
    ),
  refresh: () => api.post<RefreshResponse>(`${BASE}/refresh`, {}),
};
