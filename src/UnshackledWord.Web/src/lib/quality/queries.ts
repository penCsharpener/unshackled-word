import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/api";
import { type CheckQuery, type HitsQuery, qualityApi } from "./api";
import type { CoverageMetric, QualityFacets, UnmappedSide } from "./types";

const POLL_MS = 5000;
const STALE_MS = 60_000; // the API answers with Cache-Control max-age=60

export const qualityKeys = {
  all: ["quality"] as const,
  overview: (facets: QualityFacets) => ["quality", "overview", facets] as const,
  unmapped: (side: UnmappedSide, facets: QualityFacets, content: boolean) =>
    ["quality", "data", "unmapped", side, facets, content] as const,
  coverage: (metric: CoverageMetric, testament: QualityFacets["testament"]) =>
    ["quality", "data", "coverage", metric, testament] as const,
  check: (code: string, query: CheckQuery) => ["quality", "data", "check", code, query] as const,
  originalSearch: (q: string) => ["quality", "data", "originalSearch", q] as const,
  originalDetail: (strongs: string) => ["quality", "data", "originalDetail", strongs] as const,
  germanSearch: (q: string, pos: string) => ["quality", "data", "germanSearch", q, pos] as const,
  germanDetail: (lemma: string, pos: string) =>
    ["quality", "data", "germanDetail", lemma, pos] as const,
  hits: (query: HitsQuery) => ["quality", "data", "hits", query] as const,
};

/** The overview polls every 5 s while the server is recomputing the snapshot. */
export function useQualityOverview(facets: QualityFacets, enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: qualityKeys.overview(facets),
      queryFn: ({ signal }) => qualityApi.overview(facets, signal),
      staleTime: STALE_MS,
      enabled,
      refetchInterval: (query) => (query.state.data?.refreshing ? POLL_MS : false),
    }),
  );
}

export function useUnmapped(
  side: UnmappedSide,
  facets: QualityFacets,
  content: boolean,
  enabled: boolean,
) {
  return useQuery({
    queryKey: qualityKeys.unmapped(side, facets, content),
    queryFn: ({ signal }) => qualityApi.unmapped(side, facets, content, 10, signal),
    staleTime: STALE_MS,
    enabled,
  });
}

export function useCoverage(
  metric: CoverageMetric,
  testament: QualityFacets["testament"],
  enabled: boolean,
) {
  return useQuery({
    queryKey: qualityKeys.coverage(metric, testament),
    queryFn: ({ signal }) => qualityApi.coverage(metric, { testament }, signal),
    staleTime: STALE_MS,
    enabled,
  });
}

export function useCheckFindings(code: string | undefined, query: CheckQuery, enabled: boolean) {
  return useQuery({
    queryKey: qualityKeys.check(code ?? "", query),
    queryFn: ({ signal }) => qualityApi.check(code as string, query, signal),
    staleTime: STALE_MS,
    placeholderData: (previous) => previous,
    enabled: enabled && !!code,
  });
}

export function useOriginalSearch(q: string, enabled: boolean) {
  return useQuery({
    queryKey: qualityKeys.originalSearch(q),
    queryFn: ({ signal }) => qualityApi.originalSearch(q, signal),
    staleTime: STALE_MS,
    enabled: enabled && q.length > 0,
  });
}

export function useOriginalDetail(strongs: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: qualityKeys.originalDetail(strongs ?? ""),
    queryFn: ({ signal }) => qualityApi.originalDetail(strongs as string, signal),
    staleTime: STALE_MS,
    enabled: enabled && !!strongs,
  });
}

export function useGermanSearch(q: string, pos: string, enabled: boolean) {
  return useQuery({
    queryKey: qualityKeys.germanSearch(q, pos),
    queryFn: ({ signal }) => qualityApi.germanSearch(q, pos || undefined, signal),
    staleTime: STALE_MS,
    enabled: enabled && q.length > 0,
  });
}

export function useGermanDetail(lemma: string | undefined, pos: string, enabled: boolean) {
  return useQuery({
    queryKey: qualityKeys.germanDetail(lemma ?? "", pos),
    queryFn: ({ signal }) => qualityApi.germanDetail(lemma as string, pos || undefined, signal),
    staleTime: STALE_MS,
    enabled: enabled && !!lemma,
  });
}

export function useHits(query: HitsQuery, enabled: boolean) {
  return useQuery({
    queryKey: qualityKeys.hits(query),
    queryFn: ({ signal }) => qualityApi.hits(query, signal),
    staleTime: STALE_MS,
    placeholderData: (previous) => previous,
    enabled,
  });
}

/** POST refresh; a 409 (already running) counts as success. Re-reads the overview afterwards. */
export function useRefreshQuality() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      try {
        return await qualityApi.refresh();
      } catch (error) {
        if (error instanceof ApiError && error.status === 409) return { refreshing: true };
        throw error;
      }
    },
    onSettled: () => client.invalidateQueries({ queryKey: [...qualityKeys.all, "overview"] }),
  });
}
