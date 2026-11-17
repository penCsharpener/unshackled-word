import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";

export type LinkRoleMode = "dryRun" | "apply" | "undo";
export type LinkRoleJobState = "running" | "succeeded" | "failed";

/** Rule ids of the backfill report, in the order the rules run. */
export const LINK_ROLE_RULES = ["1", "1-main", "2", "3", "3b", "4", "5", "6"] as const;

/** Counts keyed by language (`greek`, `hebrew`, `german`) and then by rule id. */
export type LinkRoleCounts = Record<string, Record<string, number>>;

export interface LinkRoleSampleChange {
  word: string;
  before: string;
  after: string;
}

export interface LinkRoleSample {
  hebRefId: number;
  reference: string;
  language: string;
  text: string;
  changes: LinkRoleSampleChange[];
}

export interface LinkRoleReport {
  verses: number;
  links: number;
  decided: LinkRoleCounts;
  changed: LinkRoleCounts;
  undecided: number;
  undecidedByReason: LinkRoleCounts;
  mappingRows: number;
  lemmaRows: number;
  samples: Record<string, LinkRoleSample[]>;
}

export interface LinkRoleJobResult {
  report?: LinkRoleReport;
  changeSetId?: number;
  mappingRows: number;
  lemmaRows: number;
  undoChangeSetId?: number;
  mappingSkipped: number;
  lemmasSkipped: number;
}

export interface LinkRoleJob {
  id: string;
  mode: LinkRoleMode;
  status: LinkRoleJobState;
  startedAt: string;
  finishedAt?: string;
  log: string[];
  error?: string;
  result?: LinkRoleJobResult;
}

export interface LinkRoleChangeSet {
  id: number;
  userId: number;
  createdAt: string;
  mappingRows: number;
  lemmaRows: number;
}

export interface LinkRoleStatus {
  job?: LinkRoleJob;
  applied?: LinkRoleChangeSet;
  linkRolesAppliedAt?: string;
}

const POLL_MS = 1000;

export const linkRoleKeys = { status: ["maintainer", "linkRolesStatus"] as const };

/** Status of the running or last link-roles job; polls once a second while a job runs. */
export function useLinkRoleStatus(enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: linkRoleKeys.status,
      queryFn: ({ signal }) => api.get<LinkRoleStatus>("/maintainer/link-roles/status", signal),
      enabled,
      refetchInterval: (query) => (query.state.data?.job?.status === "running" ? POLL_MS : false),
    }),
  );
}

export function useStartLinkRoleJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (mode: LinkRoleMode) =>
      api.post<LinkRoleJob>("/maintainer/link-roles/jobs", { mode }),
    onSuccess: (job) => {
      queryClient.setQueryData<LinkRoleStatus>(linkRoleKeys.status, (previous) => ({
        ...previous,
        job,
      }));
      return queryClient.invalidateQueries({ queryKey: linkRoleKeys.status });
    },
  });
}
