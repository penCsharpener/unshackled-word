import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { SeedKind } from "./api";

export type SeedOperation =
  | "export"
  | "import"
  | "update"
  | "rebase"
  | "roundTrip"
  | "notesExport"
  | "notesImport"
  | "tskExport"
  | "tskImport"
  | "verseSimilarity";
export type SeedJobState = "running" | "succeeded" | "failed";

export interface SeedReportList {
  total: number;
  items: string[];
}

/** What one import or update of one kind did; `reason` is set when it was skipped. */
export interface SeedStepReport {
  kind: string;
  action: string;
  skipped?: boolean;
  reason?: string;
  seedVersion?: string;
  counts: Record<string, number>;
  lists: Record<string, SeedReportList>;
  reportFiles: string[];
}

export interface SeedFilesDiff {
  compared: number;
  identical: number;
  changed: SeedReportList;
  onlyInNew: SeedReportList;
  onlyInOld: SeedReportList;
}

export interface SeedDiskExportResult {
  kind: string;
  seedRoot: string;
  files: number;
  diff: SeedFilesDiff;
}

export interface SeedRoundTripResult {
  kind: string;
  files: number;
  secondExportIdentical: boolean;
  secondExport: SeedFilesDiff;
  matchesDisk: boolean;
  disk: SeedFilesDiff;
}

export interface SeedRebaseCounts {
  words: number;
  morphology: number;
  mappings: number;
}

export interface SeedRebaseResult {
  refused?: string;
  before?: SeedRebaseCounts;
  after?: SeedRebaseCounts;
  revisionRealigned: number;
  revisionMissing: number;
  steps: SeedStepReport[];
}

export interface SeedJobResult {
  export?: SeedDiskExportResult;
  steps: SeedStepReport[];
  rebase?: SeedRebaseResult;
  roundTrip?: SeedRoundTripResult;
}

export interface SeedJob {
  id: string;
  operation: SeedOperation;
  kind: SeedKind | "notes" | "tsk";
  status: SeedJobState;
  startedAt: string;
  finishedAt?: string;
  log: string[];
  error?: string;
  result?: SeedJobResult;
}

export interface SeedImportRow {
  id: number;
  kind: string;
  seedVersion: string;
  exportedAt?: string;
  importedAt: string;
  rows: number;
  exact: number;
  shifted: number;
  modified: number;
  orphaned: number;
  versesUpdated?: number;
  versesKeptLocal?: number;
}

export interface SeedStatus {
  job?: SeedJob;
  assetsProblem?: string;
  imports: SeedImportRow[];
}

export interface StartSeedJobRequest {
  operation: SeedOperation;
  kind: SeedKind;
  confirmBackup?: boolean;
}

const POLL_MS = 1000;

export const seedKeys = { status: ["maintainer", "seedStatus"] as const };

/** Status of the running or last seed job; polls once a second while a job runs. */
export function useSeedStatus(enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: seedKeys.status,
      queryFn: ({ signal }) => api.get<SeedStatus>("/maintainer/seed/status", signal),
      enabled,
      refetchInterval: (query) => (query.state.data?.job?.status === "running" ? POLL_MS : false),
    }),
  );
}

export function useStartSeedJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: StartSeedJobRequest) => api.post<SeedJob>("/maintainer/seed/jobs", body),
    onSuccess: (job) => {
      queryClient.setQueryData<SeedStatus>(seedKeys.status, (previous) => ({
        imports: previous?.imports ?? [],
        assetsProblem: previous?.assetsProblem,
        job,
      }));
      return queryClient.invalidateQueries({ queryKey: seedKeys.status });
    },
  });
}
