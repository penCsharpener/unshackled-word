import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { type SeedJob, type SeedStatus, seedKeys } from "./seedJobs";

/** One notes export file in `temp/notes` (roadmap 0023.6), newest first. */
export interface NotesFile {
  name: string;
  sizeBytes: number;
  modifiedAt: string;
}

export const notesFileKeys = { files: ["maintainer", "notesFiles"] as const };

export function useNotesFiles(enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: notesFileKeys.files,
      queryFn: ({ signal }) => api.get<NotesFile[]>("/maintainer/notes/files", signal),
      enabled,
    }),
  );
}

export type NotesJobRequest = { operation: "export" } | { operation: "import"; fileName?: string };

/** Starts the notes export or import job; the job is then polled through `useSeedStatus`. */
export function useStartNotesJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: NotesJobRequest) =>
      request.operation === "export"
        ? api.post<SeedJob>("/maintainer/notes/export", undefined)
        : api.post<SeedJob>("/maintainer/notes/import", { fileName: request.fileName }),
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
