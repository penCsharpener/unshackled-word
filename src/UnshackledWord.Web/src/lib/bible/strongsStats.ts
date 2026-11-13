import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { ApiError } from "@/lib/api";
import { qualityApi } from "@/lib/quality/api";
import { bibleApi } from "./api";
import type {
  GetStrongsSummaryResponse,
  OriginalLanguage,
  OriginalsLookupOptions,
  StrongsBookStat,
  StrongsRendering,
  Testament,
  WordStatsTestament,
} from "./types";

export const strongsStatsKeys = {
  summary: (strongs: string) => ["bible", "strongsSummary", strongs] as const,
  wordStats: (word: string, testament: WordStatsTestament, pos?: string) =>
    ["bible", "wordStats", word.toLowerCase(), testament, pos ?? ""] as const,
  originals: (q: string, top: number | undefined, includeFrequent: boolean) =>
    ["bible", "originalsLookup", q, top ?? null, includeFrequent] as const,
};

/** The summary is re-read this often while the server recomputes the stats. */
export const STATS_POLL_MS = 5000;

/** The API accepts a single German word only (1-50 letters, apostrophe or hyphen). */
const STATS_WORD = /^[\p{L}'-]{1,50}$/u;
export const isStatsWord = (word: string): boolean => STATS_WORD.test(word);

export const strongsSummaryQuery = (strongs: string, fresh = false) =>
  queryOptions({
    queryKey: strongsStatsKeys.summary(strongs),
    queryFn: ({ signal }) => bibleApi.strongsSummary(strongs, signal, fresh),
    staleTime: Number.POSITIVE_INFINITY,
  });

export const wordStatsQuery = (word: string, testament: WordStatsTestament, pos?: string) =>
  queryOptions({
    queryKey: strongsStatsKeys.wordStats(word, testament, pos),
    queryFn: ({ signal }) => bibleApi.wordStats(word, testament, pos, signal),
    staleTime: Number.POSITIVE_INFINITY,
  });

/**
 * Totals, occurrences per book and all German renderings of a (normalized) Strong's number.
 * Polls while the server recomputes the stats (`statsRefreshing`) or `poll` is set; polls bypass
 * the HTTP cache (the endpoint is cacheable for a minute).
 */
export function useStrongsSummary(strongs: string | undefined, poll = false) {
  const query = useQuery({
    ...strongsSummaryQuery(strongs ?? "", poll),
    enabled: strongs !== undefined,
    refetchInterval: (q) => (poll || q.state.data?.statsRefreshing ? STATS_POLL_MS : false),
  });
  return query;
}

/** Strong's numbers behind one German word in one testament; stays idle without a valid word. */
export function useWordStats(
  word: string | undefined,
  testament: WordStatsTestament,
  pos?: string,
) {
  return useQuery({
    ...wordStatsQuery(word ?? "", testament, pos),
    enabled: word !== undefined && isStatsWord(word),
  });
}

/** The server accepts 1-200 characters and at most 8 tokens. */
export const LOOKUP_MAX_LENGTH = 200;
export const LOOKUP_MAX_TOKENS = 8;
export function isLookupQuery(q: string): boolean {
  const trimmed = q.trim();
  if (trimmed.length === 0 || trimmed.length > LOOKUP_MAX_LENGTH) return false;
  return trimmed.split(/\s+/).length <= LOOKUP_MAX_TOKENS;
}

/**
 * Smart-search lookup: for each typed token the German lemmas it resolves to and their Strong's
 * originals by frequency. Idle for an invalid query; no retry on 400 / 503 (not refreshed yet).
 */
export function useOriginalsLookup(q: string, options: OriginalsLookupOptions = {}) {
  const trimmed = q.trim();
  const includeFrequent = options.includeFrequent ?? false;
  return useQuery({
    queryKey: strongsStatsKeys.originals(trimmed, options.top, includeFrequent),
    queryFn: ({ signal }) =>
      bibleApi.originalsLookup(trimmed, { top: options.top, includeFrequent }, signal),
    enabled: isLookupQuery(trimmed),
    staleTime: 60_000,
    retry: (count, error) =>
      !(error instanceof ApiError && (error.status === 400 || error.status === 503)) && count < 1,
  });
}

/**
 * Strong's summary plus the "recompute stats" action. `refresh` POSTs the shared refresh endpoint and
 * polls the summary until `statsRefreshedAt` changes (or the server reports it stopped); a 409 means
 * one is already running, a 401/403 sets `forbidden`.
 */
export function useStatsRefresh(strongs: string | undefined) {
  const client = useQueryClient();
  const [awaiting, setAwaiting] = useState<{ from: string | null } | null>(null);
  const seenRunning = useRef(false);
  const summary = useStrongsSummary(strongs, awaiting !== null);
  const data = summary.data;

  const mutation = useMutation({
    mutationFn: async () => {
      try {
        await qualityApi.refresh();
      } catch (error) {
        if (error instanceof ApiError && error.status === 409) return;
        throw error;
      }
    },
    onError: () => setAwaiting(null),
  });

  // While the server runs a refresh started elsewhere, behave as if we started it.
  const running = !!data?.statsRefreshing;
  useEffect(() => {
    if (running) seenRunning.current = true;
  }, [running]);

  const stamp = data?.statsRefreshedAt ?? null;
  useEffect(() => {
    if (!awaiting || !data) return;
    if (stamp !== awaiting.from) {
      // New snapshot: everything derived from the old one is stale.
      setAwaiting(null);
      void client.invalidateQueries({ queryKey: ["bible", "wordStats"] });
      void client.invalidateQueries({ queryKey: ["bible", "strongsOccurrences"] });
    } else if (seenRunning.current && !data.statsRefreshing) {
      setAwaiting(null); // finished without a new snapshot (the stats step failed)
    }
  }, [awaiting, data, stamp, client]);

  const forbidden =
    mutation.error instanceof ApiError &&
    (mutation.error.status === 401 || mutation.error.status === 403);

  return {
    summary,
    refreshing: running || awaiting !== null || mutation.isPending,
    failed: mutation.isError && !forbidden,
    forbidden,
    refresh: () => {
      seenRunning.current = false;
      setAwaiting({ from: stamp });
      mutation.mutate(undefined, {
        onSuccess: () =>
          void client.invalidateQueries({ queryKey: strongsStatsKeys.summary(strongs ?? "") }),
      });
    },
  };
}

const SLICE_SEP = "|";
/** Key of a rendering's donut slice: the same lemma can appear once per part of speech. */
export const renderingSliceKey = (r: StrongsRendering): string =>
  r.pos ? `${r.key}${SLICE_SEP}${r.pos}` : r.key;
/** The filter key (`StrongsRendering.key`) behind a slice key. */
export const sliceRenderingKey = (sliceKey: string): string =>
  sliceKey.split(SLICE_SEP)[0] ?? sliceKey;

/** The most used German renderings (at least half as frequent as the top one, at most 3), for the header gloss. */
export function germanGloss(renderings: StrongsRendering[]): string[] {
  const top = renderings[0];
  if (!top) return [];
  return renderings
    .filter((r) => r.count * 2 >= top.count)
    .slice(0, 3)
    .map((r) => r.word);
}

/** The testaments to chart: that of the original language, plus the other one if the word also occurs there. */
export function statsTestaments(
  language: OriginalLanguage,
  byBook: StrongsBookStat[],
): Testament[] {
  const home: Testament = language === "greek" ? "nt" : "ot";
  const strayed = byBook.some((b) => (b.bibleBookId <= 39 ? "ot" : "nt") !== home);
  return strayed ? ["ot", "nt"] : [home];
}

/** Whether a Strong's number from `wordStats` is the one shown (a lettered number matches exactly, a bare one matches its variants). */
export function isShownStrongs(candidate: string, summary: GetStrongsSummaryResponse): boolean {
  if (candidate === summary.strongs) return true;
  return (
    /^[GH]\d+$/.test(summary.strongs) &&
    candidate.startsWith(summary.strongs) &&
    /^[A-Z]*$/.test(candidate.slice(summary.strongs.length))
  );
}

/** `lang` and `dir` attributes for text in the original language. */
export function originalTextAttributes(language: OriginalLanguage): {
  lang: string;
  dir: "ltr" | "rtl";
} {
  if (language === "greek") return { lang: "el", dir: "ltr" };
  return { lang: language === "aramaic" ? "arc" : "he", dir: "rtl" };
}
