import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { type SeedJob, type SeedStatus, seedKeys } from "./seedJobs";

/** Dry run of the TSK curation import with "Exakt wie Datei" (roadmap 0024.7). `fileRows` is missing when there is no file. */
export interface TskImportPreview {
  wouldDelete: number;
  fileRows?: number;
}

export function fetchTskImportPreview(signal?: AbortSignal) {
  return api.get<TskImportPreview>("/maintainer/tsk/import/preview", signal);
}

export type TskJobRequest = { operation: "export" } | { operation: "import"; mirror: boolean };

/** Starts the TSK curation export or import job (`assets/seed/tsk/curations.csv`); the job is then polled through `useSeedStatus`. */
export function useStartTskJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TskJobRequest) =>
      request.operation === "export"
        ? api.post<SeedJob>("/maintainer/tsk/export", undefined)
        : api.post<SeedJob>("/maintainer/tsk/import", { mirror: request.mirror }),
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
