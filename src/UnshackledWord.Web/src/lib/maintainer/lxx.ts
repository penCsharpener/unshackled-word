import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { SeedJob } from "./seedJobs";

export type LxxTagStatus = "proposed" | "confirmed";
export type LxxTagSource = "nt-form" | "ai" | "human";

/** Tag rows of one (status, source) pair. */
export interface LxxTagCount {
  status: LxxTagStatus;
  source: LxxTagSource;
  count: number;
}

/** `GET api/maintainer/lxx/status` (roadmap 0028.6). */
export interface LxxStatus {
  verses: number;
  words: number;
  tags: LxxTagCount[];
  running: boolean;
  job?: SeedJob;
}

export const lxxMaintainerKeys = { status: ["maintainer", "lxx"] as const };

const POLL_MS = 1000;

/** Query key prefixes of the reader's LXX data; a prefill changes the proposed tags in it. */
const READER_KEYS = [
  ["bible", "chapterLxx"],
  ["bible", "lxxWord"],
] as const;

/** Sum of the tag rows matching the filter (status and/or source). */
export function sumTags(
  tags: LxxTagCount[],
  filter: { status?: LxxTagStatus; source?: LxxTagSource } = {},
): number {
  return tags
    .filter(
      (tag) =>
        (filter.status === undefined || tag.status === filter.status) &&
        (filter.source === undefined || tag.source === filter.source),
    )
    .reduce((total, tag) => total + tag.count, 0);
}

/** Status of the LXX prefill; polls once a second while the job runs. */
export function useLxxStatus(enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: lxxMaintainerKeys.status,
      queryFn: ({ signal }) => api.get<LxxStatus>("/maintainer/lxx/status", signal),
      enabled,
      refetchInterval: (query) => (query.state.data?.running ? POLL_MS : false),
    }),
  );
}

/** Starts the prefill; the job is then followed through `useLxxStatus`. */
export function useStartLxxPrefill() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<{ job: SeedJob }>("/maintainer/lxx/prefill", {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: lxxMaintainerKeys.status }),
  });
}

/** After a finished prefill: the reader's cached LXX chapters and word cards are stale. */
export function invalidateReaderLxx(queryClient: ReturnType<typeof useQueryClient>) {
  return Promise.all(READER_KEYS.map((queryKey) => queryClient.invalidateQueries({ queryKey })));
}
