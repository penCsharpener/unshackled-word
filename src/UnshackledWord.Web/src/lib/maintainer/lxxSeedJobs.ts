import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { type SeedJob, type SeedStatus, seedKeys } from "./seedJobs";

/** Dry run of the LXX seed import with "Exakt wie Datei" (roadmap 0028.8). The file row counts are missing when there are no files. */
export interface LxxSeedImportPreview {
  wouldDeleteTags: number;
  wouldDeleteLinks: number;
  fileTagRows?: number;
  fileLinkRows?: number;
}

export function fetchLxxSeedImportPreview(signal?: AbortSignal) {
  return api.get<LxxSeedImportPreview>("/maintainer/lxx/seed/import/preview", signal);
}

export type LxxSeedJobRequest = { operation: "export" } | { operation: "import"; mirror: boolean };

/** Starts the LXX seed export or import job (`assets/seed/lxx/tags.csv`, `links.csv`); the job is then polled through `useSeedStatus`. */
export function useStartLxxSeedJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: LxxSeedJobRequest) =>
      request.operation === "export"
        ? api.post<SeedJob>("/maintainer/lxx/seed/export", undefined)
        : api.post<SeedJob>("/maintainer/lxx/seed/import", { mirror: request.mirror }),
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
