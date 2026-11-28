import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { RunSetupRequest, SetupJob, SetupStatus } from "./types";

export const SETUP_POLL_MS = 2000;
export const setupKeys = { status: ["admin", "setup"] as const };

/**
 * Setup status from `GET /admin/setup`: steps with counts and the running or last setup job. The server computes the counts on every
 * read, so the poll that sees a finished job also carries the final counts. Polls every 2 s while a job runs.
 */
export function useSetupStatus(enabled: boolean) {
  return useQuery(
    queryOptions({
      queryKey: setupKeys.status,
      queryFn: async ({ signal }) =>
        withJobDetails(await api.get<SetupStatus>("/admin/setup", signal)),
      enabled,
      refetchInterval: (query) =>
        query.state.data?.job?.status === "running" || query.state.data?.otherJobRunning
          ? SETUP_POLL_MS
          : false,
    }),
  );
}

/** Adds what the page needs from the job: the running step (server) and the step that stopped a failed job (its `failed` flag). */
function withJobDetails(status: SetupStatus): SetupStatus {
  const job = status.job;
  if (!job) return status;
  const failedStep =
    job.failedStep ??
    (job.status === "failed" ? status.steps.find((step) => step.failed)?.key : undefined);
  return {
    ...status,
    job: {
      ...job,
      currentStep: job.status === "running" ? (status.currentStep ?? job.currentStep) : undefined,
      failedStep,
    },
  };
}

/** Starts a run (`POST /admin/setup/run`, 202 with the job); the status poll takes over from there. */
export function useRunSetup() {
  const queryClient = useQueryClient();
  return useMutation<SetupJob, Error, RunSetupRequest>({
    mutationFn: (request) => api.post<SetupJob>("/admin/setup/run", request),
    onSuccess: (job) => {
      queryClient.setQueryData<SetupStatus>(setupKeys.status, (old) =>
        old ? { ...old, job } : old,
      );
      void queryClient.invalidateQueries({ queryKey: setupKeys.status });
    },
  });
}
