import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { StatTile } from "@/components/StatTile";
import { StatusBadge, type StatusTone } from "@/components/StatusBadge";
import { TextField } from "@/components/TextField";
import { formatCost } from "@/lib/agent/format";
import {
  agentKeys,
  useAgentProfiles,
  useAgentRun,
  useAgentRuns,
  useCancelAgentRun,
  useResumeAgentRun,
} from "@/lib/agent/queries";
import { type AgentRun, canResumeRun, isRunActive } from "@/lib/agent/types";
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
import { vocabEditorKeys } from "@/lib/vocab/editorApi";
import {
  estimateVocabFormRun,
  startVocabFormRun,
  type VocabFormRunRange,
} from "@/lib/vocab/formRunApi";

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

/** Course of a dictionary-form run: the NT or the AT (a form run always has one course). */
type FormScope = "nt" | "at";
const FORM_SCOPES: FormScope[] = ["nt", "at"];
const FORM_COURSE: Record<FormScope, string> = { nt: "nt-greek", at: "ot-hebrew" };
/** Default last rank of a form run: NT ranks 1 to 200, as in the roadmap example. */
const FORM_TO_RANK = 200;

/** A rank typed into a field: a whole number from 1, otherwise null. */
function parseRank(text: string): number | null {
  const rank = Number(text);
  return text.trim() !== "" && Number.isInteger(rank) && rank > 0 ? rank : null;
}

/** A form run that is still going is info, a finished one ok, a failed one an error. */
function formRunTone(status: string): StatusTone {
  if (isRunActive(status)) return "info";
  if (status === "completed") return "ok";
  return status === "failed" ? "error" : "warning";
}

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

  const [formScope, setFormScope] = useState<FormScope>("nt");
  const [formProfile, setFormProfile] = useState<string>();
  const [formFrom, setFormFrom] = useState("1");
  const [formTo, setFormTo] = useState(String(FORM_TO_RANK));
  /** The form run started in this view; before that the newest run of mode `vocab-form` is shown. */
  const [startedFormRunId, setStartedFormRunId] = useState<number>();

  const profiles = useAgentProfiles();
  const profileList = profiles.data ?? [];
  const formProfileName = formProfile ?? profileList.find((p) => p.usable)?.name ?? "";
  const formProfileUsable = profileList.some((p) => p.name === formProfileName && p.usable);
  const fromRank = parseRank(formFrom);
  const toRank = parseRank(formTo);
  const formRequest: VocabFormRunRange | null =
    formProfileUsable && fromRank !== null && toRank !== null && toRank >= fromRank
      ? { course: FORM_COURSE[formScope], fromRank, toRank, profile: formProfileName }
      : null;

  /** Dry run of the range (no model, nothing written); the start is only enabled for the estimate shown. */
  const formEstimate = useQuery({
    queryKey: ["maintainer", "vocab", "form-runs", "estimate", formRequest],
    queryFn: ({ signal }) => estimateVocabFormRun(formRequest as VocabFormRunRange, signal),
    enabled: formRequest !== null,
    retry: false,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });

  const agentRuns = useAgentRuns();
  const latestFormRunId = agentRuns.data?.find((run) => run.mode === "vocab-form")?.id;
  const formRunId = startedFormRunId ?? latestFormRunId ?? null;
  const formRun = useAgentRun(formRunId);
  const formRunData = formRun.data;
  const formRunStatus = formRunData?.status;
  const formRunActive = formRunStatus !== undefined && isRunActive(formRunStatus);
  const cancelFormRun = useCancelAgentRun();
  const resumeFormRun = useResumeAgentRun();

  const startFormRun = useMutation({
    mutationFn: (body: VocabFormRunRange) => startVocabFormRun(body),
    onSuccess: (run: AgentRun) => {
      queryClient.setQueryData<AgentRun>(agentKeys.run(run.id), run);
      setStartedFormRunId(run.id);
      return queryClient.invalidateQueries({ queryKey: agentKeys.runs });
    },
  });

  useEffect(() => {
    // A finished form run has written forms: the editor lists (filter "Lernform von KI") must read them again.
    if (formRunId !== null && formRunStatus !== undefined && !isRunActive(formRunStatus)) {
      void queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all });
    }
  }, [formRunId, formRunStatus, queryClient]);

  const formRunBusy = formRunActive || startFormRun.isPending;
  const formEstimateData = formEstimate.isPlaceholderData ? undefined : formEstimate.data;
  const formReady = formRequest !== null && formEstimateData !== undefined && !formRunBusy;
  const formNoEntries = formEstimate.error instanceof ApiError && formEstimate.error.status === 404;
  const startForm = () => {
    if (formRequest !== null && formReady) startFormRun.mutate(formRequest);
  };

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
    startFormRun.error ? t("vocab.jobs.forms.startFailed") : null,
    cancelFormRun.isError ? t("agent.runs.cancelFailed") : null,
    resumeFormRun.isError ? t("agent.runs.resumeFailed") : null,
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
        <li className="grid gap-3 py-3">
          <div className="grid gap-1">
            <span className="font-medium">{t("vocab.jobs.forms.title")}</span>
            <span className="text-sm text-fg-muted">{t("vocab.jobs.forms.intro")}</span>
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <Select
              label={t("vocab.jobs.forms.course")}
              value={formScope}
              onChange={(value) => setFormScope(value as FormScope)}
              disabled={formRunBusy}
              options={FORM_SCOPES.map((value) => ({
                value,
                label: t(`vocab.jobs.meanings.scopes.${value}`),
              }))}
            />
            <TextField
              type="number"
              size="sm"
              label={t("vocab.editor.rankFrom")}
              min={1}
              value={formFrom}
              onChange={setFormFrom}
            />
            <TextField
              type="number"
              size="sm"
              label={t("vocab.editor.rankTo")}
              min={1}
              value={formTo}
              onChange={setFormTo}
            />
            <Select
              label={t("vocab.jobs.forms.profile")}
              value={formProfileName}
              onChange={setFormProfile}
              disabled={profileList.length === 0 || formRunBusy}
              options={profileList.map((p) => ({
                value: p.name,
                label: p.usable
                  ? `${p.name} · ${p.model}`
                  : `${p.name} · ${t("agent.dialog.unusable", { reason: p.unusableReason ?? t("agent.dialog.noReason") })}`,
                disabled: !p.usable,
                title: p.unusableReason,
              }))}
            />
            <Button variant="primary" size="sm" disabled={!formReady} onClick={startForm}>
              {startFormRun.isPending ? t("agent.dialog.starting") : t("vocab.jobs.forms.start")}
            </Button>
          </div>
          {formRequest === null ? (
            <span className="text-sm text-fg-muted">{t("agent.dialog.estimate.needRange")}</span>
          ) : formNoEntries ? (
            <span className="text-sm text-fg-muted">{t("vocab.jobs.forms.noEntries")}</span>
          ) : formEstimate.isError ? (
            <FormError message={t("agent.dialog.estimate.failed")} />
          ) : formEstimateData ? (
            <div className="grid gap-3">
              <span className="text-sm text-fg-muted">
                {t("vocab.jobs.forms.ranks", {
                  from: number(formEstimateData.fromRank),
                  to: number(formEstimateData.toRank),
                })}
              </span>
              <div className="grid grid-cols-2 gap-2 md:grid-cols-3">
                <StatTile
                  title={t("vocab.jobs.forms.entries")}
                  value={number(formEstimateData.entries)}
                />
                <StatTile
                  title={t("agent.dialog.estimate.packages")}
                  value={number(formEstimateData.batches)}
                />
                <StatTile
                  title={t("agent.dialog.estimate.input")}
                  value={number(formEstimateData.estimatedInputTokens)}
                />
                <StatTile
                  title={t("agent.dialog.estimate.output")}
                  value={number(formEstimateData.estimatedOutputTokens)}
                />
                <StatTile
                  title={t("agent.dialog.estimate.cost")}
                  value={formatCost(formEstimateData.estimatedCostUsd, i18n.language)}
                />
              </div>
              {formEstimateData.warnings.map((warning) => (
                <span key={warning} className="text-sm text-fg-muted">
                  {warning}
                </span>
              ))}
            </div>
          ) : (
            <span role="status" className="text-sm text-fg-muted">
              {t("agent.dialog.estimate.loading")}
            </span>
          )}
          {formRunData ? (
            <div className="grid gap-2 rounded-md border border-border p-3 text-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <StatusBadge tone={formRunTone(formRunData.status)}>
                  {t(`agent.status.${formRunData.status}`)}
                </StatusBadge>
                <span className="tabular-nums text-fg-muted">
                  {t("agent.runs.tokensCell", {
                    input: number(formRunData.inputTokens),
                    output: number(formRunData.outputTokens),
                  })}{" "}
                  · {formatCost(formRunData.costUsd, i18n.language)}
                </span>
              </div>
              {formRunData.statusNote ? (
                <span className="text-fg-muted">{formRunData.statusNote}</span>
              ) : null}
              <div className="flex flex-wrap gap-2">
                {formRunActive ? (
                  <Button
                    variant="secondary"
                    size="sm"
                    disabled={cancelFormRun.isPending}
                    onClick={() => cancelFormRun.mutate(formRunData.id)}
                  >
                    {t("agent.runs.cancel")}
                  </Button>
                ) : null}
                {canResumeRun(formRunData.status) ? (
                  <Button
                    variant="secondary"
                    size="sm"
                    disabled={resumeFormRun.isPending}
                    onClick={() => resumeFormRun.mutate({ id: formRunData.id })}
                  >
                    {t("agent.runs.resume")}
                  </Button>
                ) : null}
              </div>
            </div>
          ) : null}
        </li>
      </ul>
    </div>
  );
}
