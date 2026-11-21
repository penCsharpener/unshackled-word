import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { StatusBadge } from "@/components/StatusBadge";
import { ApiError } from "@/lib/api";
import { formatStamp } from "@/lib/format";
import {
  getRenderingsStatus,
  getVocabGenerateStatus,
  startRenderingsRefresh,
  startVocabGenerate,
  type VocabGenerateStatus,
  type VocabJob,
  type VocabRenderingsStatus,
} from "@/lib/vocab/api";

const POLL_MS = 1000;
const renderingsKey = ["maintainer", "vocab", "renderings"] as const;
const generateKey = ["maintainer", "vocab", "generate"] as const;

/** Entries and new entries of a finished lexicon generation, summed over the tracks. */
function generateSummary(job: VocabJob) {
  const counts = job.result?.steps.flatMap((step) => Object.entries(step.counts)) ?? [];
  const entries = counts
    .filter(([key]) => key.endsWith(".entries"))
    .reduce((sum, [, value]) => sum + value, 0);
  const created = counts.find(([key]) => key === "entriesCreated")?.[1] ?? 0;
  return { entries, created };
}

/** Maintainer card "Vokabeln" (screen 7): the lexicon generation and the renderings refresh as background jobs with their time. */
export function VocabJobsSection() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const number = (value: number) => value.toLocaleString(i18n.language);

  const renderings = useQuery({
    queryKey: renderingsKey,
    queryFn: ({ signal }) => getRenderingsStatus(signal),
    refetchInterval: (query) => (query.state.data?.job?.status === "running" ? POLL_MS : false),
  });
  const generate = useQuery({
    queryKey: generateKey,
    queryFn: ({ signal }) => getVocabGenerateStatus(signal),
    refetchInterval: (query) => (query.state.data?.job?.status === "running" ? POLL_MS : false),
  });

  const startRefresh = useMutation({
    mutationFn: startRenderingsRefresh,
    onSuccess: (job) => {
      queryClient.setQueryData<VocabRenderingsStatus>(renderingsKey, (previous) => ({
        ...previous,
        job,
      }));
      return queryClient.invalidateQueries({ queryKey: renderingsKey });
    },
  });
  const startGenerate = useMutation({
    mutationFn: startVocabGenerate,
    onSuccess: (job) => {
      queryClient.setQueryData<VocabGenerateStatus>(generateKey, { job });
      return queryClient.invalidateQueries({ queryKey: generateKey });
    },
  });

  const refreshJob = renderings.data?.job;
  const refreshRunning = refreshJob?.status === "running" || startRefresh.isPending;
  const generateJob = generate.data?.job;
  const generateRunning = generateJob?.status === "running" || startGenerate.isPending;

  const startError = (error: unknown) =>
    error instanceof ApiError && error.status === 409
      ? t("vocab.jobs.conflict")
      : t("vocab.jobs.startFailed");

  const messages = [
    renderings.isError || generate.isError ? t("vocab.jobs.loadFailed") : null,
    startRefresh.error ? startError(startRefresh.error) : null,
    startGenerate.error ? startError(startGenerate.error) : null,
    refreshJob?.status === "failed" ? `${t("vocab.jobs.failed")} ${refreshJob.error ?? ""}` : null,
    generateJob?.status === "failed"
      ? `${t("vocab.jobs.failed")} ${generateJob.error ?? ""}`
      : null,
  ].filter((message): message is string => message !== null);

  const generateSucceeded = generateJob?.status === "succeeded" && generateJob.finishedAt;
  const generateInfo = generateSucceeded ? generateSummary(generateJob) : null;

  return (
    <div className="grid gap-4 rounded-lg border border-border bg-surface p-4">
      <h2 className="text-lg font-semibold">{t("vocab.jobs.title")}</h2>
      <FormError message={messages.join(" ")} />
      <ul className="divide-y divide-border">
        <li className="flex flex-wrap items-center justify-between gap-3 py-3">
          <div className="grid gap-1">
            <span className="font-medium">{t("vocab.jobs.generate.title")}</span>
            <span className="text-sm text-fg-muted tabular-nums">
              {generateInfo && generateJob?.finishedAt
                ? t("vocab.jobs.generate.last", {
                    time: formatStamp(generateJob.finishedAt, i18n.language),
                    entries: number(generateInfo.entries),
                    created: number(generateInfo.created),
                  })
                : t("vocab.jobs.generate.never")}
            </span>
          </div>
          <div className="flex items-center gap-2">
            {generateRunning ? (
              <StatusBadge tone="info">{t("vocab.jobs.running")}</StatusBadge>
            ) : null}
            <Button
              variant="secondary"
              size="sm"
              disabled={generateRunning}
              onClick={() => startGenerate.mutate()}
            >
              {t("vocab.jobs.generate.start")}
            </Button>
          </div>
        </li>
        <li className="flex flex-wrap items-center justify-between gap-3 py-3">
          <div className="grid gap-1">
            <span className="font-medium">{t("vocab.jobs.renderings.title")}</span>
            <span className="text-sm text-fg-muted tabular-nums">
              {renderings.data?.refreshedAt
                ? t("vocab.jobs.renderings.asOf", {
                    time: formatStamp(renderings.data.refreshedAt, i18n.language),
                  })
                : t("vocab.jobs.renderings.never")}
            </span>
          </div>
          <div className="flex items-center gap-2">
            {refreshRunning ? (
              <StatusBadge tone="info">{t("vocab.jobs.running")}</StatusBadge>
            ) : null}
            <Button
              variant="secondary"
              size="sm"
              disabled={refreshRunning}
              onClick={() => startRefresh.mutate()}
            >
              {t("vocab.jobs.renderings.start")}
            </Button>
          </div>
        </li>
      </ul>
    </div>
  );
}
