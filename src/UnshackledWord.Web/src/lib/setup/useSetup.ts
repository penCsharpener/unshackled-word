import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { setupFake } from "./setupFake";
import type { RunSetupRequest, SetupJob, SetupStatus } from "./types";

export const SETUP_POLL_MS = 2000;
export const setupKeys = { status: ["admin", "setup"] as const };

/** Setup status; polls every 2 s while a job runs (0031.5f: `api.get<SetupStatus>("/admin/setup")`). */
export function useSetupStatus(enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: setupKeys.status,
      queryFn: () => setupFake().status(),
      enabled,
      refetchInterval: (query) =>
        query.state.data?.job?.status === "running" ? SETUP_POLL_MS : false,
    }),
  );
}

/** Starts a run; writes the job into the status cache and refetches (0031.5f: `api.post<SetupJob>("/admin/setup/run")`). */
export function useRunSetup() {
  const queryClient = useQueryClient();
  return useMutation<SetupJob, Error, RunSetupRequest>({
    mutationFn: (request) => setupFake().run(request),
    onSuccess: (job) => {
      queryClient.setQueryData<SetupStatus>(setupKeys.status, (old) =>
        old ? { ...old, job } : old,
      );
      void queryClient.invalidateQueries({ queryKey: setupKeys.status });
    },
  });
}
