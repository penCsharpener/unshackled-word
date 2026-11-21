import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { StatTile } from "@/components/StatTile";
import { StatusBadge } from "@/components/StatusBadge";
import { ApiError } from "@/lib/api";
import { formatStamp } from "@/lib/format";
import {
  type DeriveVocabMeaningsRequest,
  getMeaningsStatus,
  getRenderingsStatus,
  getVocabGenerateStatus,
  startMeaningsDerive,
  startRenderingsRefresh,
  startVocabGenerate,
  type VocabGenerateStatus,
  type VocabJob,
  type VocabMeaningsStatus,
  type VocabRenderingsStatus,
} from "@/lib/vocab/api";

const POLL_MS = 1000;
const renderingsKey = ["maintainer", "vocab", "renderings"] as const;
const generateKey = ["maintainer", "vocab", "generate"] as const;
const meaningsKey = ["maintainer", "vocab", "meanings"] as const;

/** Scope of a meaning derivation: all entries, or one course. */
type MeaningsScope = "all" | "nt" | "at";
const MEANINGS_SCOPES: MeaningsScope[] = ["all", "nt", "at"];
const COURSE_BY_SCOPE: Record<MeaningsScope, string | undefined> = {
  all: undefined,
  nt: "nt-greek",
  at: "ot-hebrew",
};

/** One entry whose first meaning changed, parsed from a report line `"{id} {lemma}: {before} -> {after}"` (`-` = none). */
interface TopChange {
  id: number;
  lemma: string;
  before: string;
  after: string;
}

function parseTopChange(line: string): TopChange | null {
  const match = /^(\d+) (.+?): (.*) -> (.*)$/u.exec(line);
  if (!match) return null;
  const [, id = "", lemma = "", before = "", after = ""] = match;
  return { id: Number(id), lemma, before, after };
}

/** Hebrew and Aramaic lemmas get `lang="he"` and `dir="rtl"`, Greek ones `lang="grc"`. */
const isHebrew = (text: string) => /[֐-׿]/u.test(text);

/** The report writes `-` for "no first meaning"; the table shows the en dash used elsewhere. */
const orDash = (value: string) => (value === "-" ? "–" : value);

/** Entries and new entries of a finished lexicon generation, summed over the tracks. */
function generateSummary(job: VocabJob) {
  const counts = job.result?.steps.flatMap((step) => Object.entries(step.counts)) ?? [];
  const entries = counts
    .filter(([key]) => key.endsWith(".entries"))
    .reduce((sum, [, value]) => sum + value, 0);
  const created = counts.find(([key]) => key === "entriesCreated")?.[1] ?? 0;
  return { entries, created };
}

/** Maintainer card "Vokabeln" (screen 7): the lexicon generation, the renderings refresh and the meaning derivation (dry run, then apply) as background jobs with their time. */
export function VocabJobsSection() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const number = (value: number) => value.toLocaleString(i18n.language);
  const [scope, setScope] = useState<MeaningsScope>("all");
  /** Scope of the last dry run started in this session; apply needs a finished dry run for the selected scope. */
  const [dryRunScope, setDryRunScope] = useState<MeaningsScope | null>(null);

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

  const meanings = useQuery({
    queryKey: meaningsKey,
    queryFn: ({ signal }) => getMeaningsStatus(signal),
    refetchInterval: (query) => (query.state.data?.job?.status === "running" ? POLL_MS : false),
  });
  const startMeanings = useMutation({
    mutationFn: ({ dryRun, scope: selected }: { dryRun: boolean; scope: MeaningsScope }) => {
      const request: DeriveVocabMeaningsRequest = { course: COURSE_BY_SCOPE[selected], dryRun };
      return startMeaningsDerive(request);
    },
    onSuccess: (job, { dryRun, scope: selected }) => {
      queryClient.setQueryData<VocabMeaningsStatus>(meaningsKey, { job });
      setDryRunScope(dryRun ? selected : null);
      return queryClient.invalidateQueries({ queryKey: meaningsKey });
    },
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
  const meaningsJob = meanings.data?.job;
  const meaningsRunning = meaningsJob?.status === "running" || startMeanings.isPending;
  const meaningsReport =
    meaningsJob?.status === "succeeded" ? meaningsJob.result?.steps[0] : undefined;
  const meaningCount = (key: string) => meaningsReport?.counts[key] ?? 0;
  const topList = meaningsReport?.lists.topMeaningChanged;
  const topChanges = (topList?.items ?? [])
    .map(parseTopChange)
    .filter((change): change is TopChange => change !== null);
  const canApply = !meaningsRunning && meaningsJob?.status === "succeeded" && dryRunScope === scope;

  const startError = (error: unknown) =>
    error instanceof ApiError && error.status === 409
      ? t("vocab.jobs.conflict")
      : t("vocab.jobs.startFailed");

  const messages = [
    renderings.isError || generate.isError || meanings.isError ? t("vocab.jobs.loadFailed") : null,
    startRefresh.error ? startError(startRefresh.error) : null,
    startGenerate.error ? startError(startGenerate.error) : null,
    startMeanings.error ? startError(startMeanings.error) : null,
    refreshJob?.status === "failed" ? `${t("vocab.jobs.failed")} ${refreshJob.error ?? ""}` : null,
    generateJob?.status === "failed"
      ? `${t("vocab.jobs.failed")} ${generateJob.error ?? ""}`
      : null,
    meaningsJob?.status === "failed"
      ? `${t("vocab.jobs.failed")} ${meaningsJob.error ?? ""}`
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
        <li className="grid gap-3 py-3">
          <div className="grid gap-1">
            <span className="font-medium">{t("vocab.jobs.meanings.title")}</span>
            <span className="text-sm text-fg-muted">{t("vocab.jobs.meanings.intro")}</span>
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <Select
              label={t("vocab.jobs.meanings.scope")}
              value={scope}
              onChange={(value) => setScope(value as MeaningsScope)}
              disabled={meaningsRunning}
              options={MEANINGS_SCOPES.map((value) => ({
                value,
                label: t(`vocab.jobs.meanings.scopes.${value}`),
              }))}
            />
            {meaningsRunning ? (
              <StatusBadge tone="info">{t("vocab.jobs.running")}</StatusBadge>
            ) : null}
            <Button
              variant="secondary"
              size="sm"
              disabled={meaningsRunning}
              onClick={() => startMeanings.mutate({ dryRun: true, scope })}
            >
              {t("vocab.jobs.meanings.dryRun")}
            </Button>
            <Button
              variant="primary"
              size="sm"
              disabled={!canApply}
              onClick={() => startMeanings.mutate({ dryRun: false, scope })}
            >
              {t("vocab.jobs.meanings.apply")}
            </Button>
          </div>
          {!canApply && !meaningsRunning ? (
            <span className="text-sm text-fg-muted">
              {t("vocab.jobs.meanings.applyNeedsDryRun")}
            </span>
          ) : null}
          {meaningsReport ? (
            <div className="grid gap-3">
              <span className="text-sm text-fg-muted">
                {meaningsReport.counts.dryRun === 1
                  ? t("vocab.jobs.meanings.reportDryRun")
                  : t("vocab.jobs.meanings.reportApplied")}
              </span>
              <div className="grid grid-cols-2 gap-2 md:grid-cols-3">
                <StatTile
                  title={t("vocab.jobs.meanings.entries")}
                  value={number(meaningCount("entriesProcessed"))}
                />
                <StatTile
                  title={t("vocab.jobs.meanings.entriesChanged")}
                  value={number(meaningCount("entriesChanged"))}
                />
                <StatTile
                  title={t("vocab.jobs.meanings.topChanged")}
                  value={number(meaningCount("topMeaningChanged"))}
                />
              </div>
              {topChanges.length > 0 ? (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm">
                    <caption className="pb-2 text-left font-medium">
                      {t("vocab.jobs.meanings.topChangedCaption")}
                    </caption>
                    <thead className="text-fg-muted">
                      <tr className="border-b border-border">
                        <th className="py-2 pr-4 font-medium">
                          {t("vocab.jobs.meanings.columns.lemma")}
                        </th>
                        <th className="py-2 pr-4 font-medium">
                          {t("vocab.jobs.meanings.columns.before")}
                        </th>
                        <th className="py-2 pr-4 font-medium">
                          {t("vocab.jobs.meanings.columns.after")}
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-border">
                      {topChanges.map((change) => {
                        const he = isHebrew(change.lemma);
                        return (
                          <tr key={change.id}>
                            <td className="py-2 pr-4">
                              <Link
                                to={`/vocab/${change.id}`}
                                lang={he ? "he" : "grc"}
                                dir={he ? "rtl" : undefined}
                                className="font-original text-base text-accent underline"
                              >
                                {change.lemma}
                              </Link>
                            </td>
                            <td className="py-2 pr-4">{orDash(change.before)}</td>
                            <td className="py-2 pr-4">{orDash(change.after)}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              ) : (
                <span className="text-sm text-fg-muted">
                  {t("vocab.jobs.meanings.noTopChange")}
                </span>
              )}
              {topList && topList.total > topList.items.length ? (
                <span className="text-sm text-fg-muted tabular-nums">
                  {t("vocab.jobs.meanings.truncated", {
                    shown: number(topList.items.length),
                    total: number(topList.total),
                  })}
                </span>
              ) : null}
            </div>
          ) : null}
        </li>
      </ul>
    </div>
  );
}
