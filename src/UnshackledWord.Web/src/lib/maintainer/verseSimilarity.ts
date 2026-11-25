import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { SeedJob } from "./seedJobs";

/** The four rebuild parameters (roadmap 0027). */
export interface VerseSimilarityParameters {
  stopDfShare: number;
  topK: number;
  minScore: number;
  phraseWeight: number;
}

export type VerseSimilarityRunStatus = "running" | "succeeded" | "failed";

/** One row of `VerseSimilarityRuns`. */
export interface VerseSimilarityRun {
  id: number;
  startedAt: string;
  finishedAt?: string;
  status: VerseSimilarityRunStatus;
  startedBy?: number;
  startedByName?: string;
  parameters: VerseSimilarityParameters;
  pairCount?: number;
  stopWordCount?: number;
  durationMs?: number;
  /** Raw JSON of the evaluation (recall@30, golden pairs); filled from roadmap 0027.6. */
  evaluation?: string;
  error?: string;
}

/** `GET api/maintainer/verse-similarity`. */
export interface VerseSimilarityStatus {
  defaults: VerseSimilarityParameters;
  /** The last five runs, newest first. */
  runs: VerseSimilarityRun[];
  running: boolean;
  job?: SeedJob;
}

/** Bounds the server enforces; the form mirrors them. */
export const VERSE_SIMILARITY_BOUNDS = {
  stopDfShare: { min: 0, max: 1, minExclusive: true, maxExclusive: false },
  topK: { min: 1, max: 1000, minExclusive: false, maxExclusive: false },
  minScore: { min: 0, max: 100, minExclusive: false, maxExclusive: true },
  phraseWeight: { min: 0, max: 100, minExclusive: false, maxExclusive: true },
} as const;

export const verseSimilarityKeys = { status: ["maintainer", "verseSimilarity"] as const };

const POLL_MS = 1000;

/** Query key prefixes of the reader's similar verses; they change only with a rebuild. */
const READER_KEYS = [
  ["bible", "chapterSimilarVerses"],
  ["bible", "similarVersesOfVerse"],
] as const;

/** Status of the verse similarity rebuild; polls once a second while a rebuild runs. */
export function useVerseSimilarityStatus(enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: verseSimilarityKeys.status,
      queryFn: ({ signal }) =>
        api.get<VerseSimilarityStatus>("/maintainer/verse-similarity", signal),
      enabled,
      refetchInterval: (query) => (query.state.data?.running ? POLL_MS : false),
    }),
  );
}

/** Starts a rebuild; the job is then followed through `useVerseSimilarityStatus`. */
export function useStartVerseSimilarityRebuild() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (parameters: VerseSimilarityParameters) =>
      api.post<{ job: SeedJob }>("/maintainer/verse-similarity/rebuild", parameters),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: verseSimilarityKeys.status }),
  });
}

/** After a succeeded rebuild: the reader's cached similar verses are stale. */
export function invalidateReaderSimilarVerses(queryClient: ReturnType<typeof useQueryClient>) {
  return Promise.all(READER_KEYS.map((queryKey) => queryClient.invalidateQueries({ queryKey })));
}
