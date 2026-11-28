import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { setupFake } from "./setupFake";
import type { RunSetupRequest, SetupJob, SetupStatus } from "./types";

export const SETUP_POLL_MS = 2000;
export const setupKeys = { status: ["admin", "setup"] as const };

/**
 * Setup status: the steps and counts come from `GET /admin/setup` (0031.5f); polls every 2 s while a job runs.
 * Runs are still simulated by the fake until 0031.6f, so its job (log, running step) is merged in when the server has none.
 */
export function useSetupStatus(enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: setupKeys.status,
      queryFn: async ({ signal }) => {
        const real = await api.get<SetupStatus>("/admin/setup", signal);
        const fakeJob = (await setupFake().status()).job;
        return { ...real, job: real.job ?? fakeJob };
      },
      enabled,
      refetchInterval: (query) =>
        query.state.data?.job?.status === "running" ? SETUP_POLL_MS : false,
    }),
  );
}

/** Starts a run; writes the job into the status cache and refetches (still the fake until 0031.6f: `api.post<SetupJob>("/admin/setup/run")`). */
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
