import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { StatTile } from "@/components/StatTile";
import { StatusBadge, type StatusTone } from "@/components/StatusBadge";
import { Switch } from "@/components/Switch";
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
import { vocabEditorKeys } from "@/lib/vocab/editorApi";
import {
  estimateVocabFormRun,
  startVocabFormRun,
  type VocabFormRunEstimate,
} from "@/lib/vocab/formRunApi";
import {
  estimateVocabRootRun,
  getVocabRootRunReport,
  startVocabRootRun,
  type VocabRootRunItemKind,
  type VocabRootRunRange,
} from "@/lib/vocab/rootRunApi";

/** The two agent runs of the vocab jobs section: dictionary forms (roadmap 0022.13) and roots (roadmap 0030.4). */
export type VocabAgentRunKind = "form" | "root";

/** Course of a run: the NT or the AT (a run always has one course). */
type RunScope = "nt" | "at";
const RUN_SCOPES: RunScope[] = ["nt", "at"];
const RUN_COURSE: Record<RunScope, string> = { nt: "nt-greek", at: "ot-hebrew" };
/** Default last rank of a run: NT ranks 1 to 200, as in the roadmap example. */
const RUN_TO_RANK = 200;
/** Entries per model call: the API default 25, at most 200. */
const DEFAULT_BATCH_SIZE = 25;
const MAX_BATCH_SIZE = 200;

const KIND_CONFIG = {
  form: {
    prefix: "vocab.jobs.forms",
    mode: "vocab-form",
    estimatePath: ["maintainer", "vocab", "form-runs", "estimate"],
    estimate: (body: VocabRootRunRange, signal?: AbortSignal) => estimateVocabFormRun(body, signal),
    start: (body: VocabRootRunRange) => startVocabFormRun(body),
  },
  root: {
    prefix: "vocab.jobs.rootRun",
    mode: "vocab-root",
    estimatePath: ["maintainer", "vocab", "root-runs", "estimate"],
    estimate: (body: VocabRootRunRange, signal?: AbortSignal) => estimateVocabRootRun(body, signal),
    start: (body: VocabRootRunRange) => startVocabRootRun(body),
  },
} as const;

/** A rank typed into a field: a whole number from 1, otherwise null. */
function parseRank(text: string): number | null {
  const rank = Number(text);
  return text.trim() !== "" && Number.isInteger(rank) && rank > 0 ? rank : null;
}

/** A batch size typed into the field: a whole number from 1 to 200, otherwise null. */
function parseBatchSize(text: string): number | null {
  const size = parseRank(text);
  return size !== null && size <= MAX_BATCH_SIZE ? size : null;
}

/** A run that is still going is info, a finished one ok, a failed one an error. */
function runTone(status: string): StatusTone {
  if (isRunActive(status)) return "info";
  if (status === "completed") return "ok";
  return status === "failed" ? "error" : "warning";
}

const ITEM_TONE: Record<VocabRootRunItemKind, StatusTone> = {
  rejected: "error",
  flagged: "warning",
  replaced: "info",
};

/** Entries the root run did not simply write: rejected, flagged and replaced prefill rows, linked to the editor. */
function RootRunReport({ runId, status }: { runId: number; status: string }) {
  const { t, i18n } = useTranslation();
  const report = useQuery({
    queryKey: ["maintainer", "vocab", "root-runs", runId, "report", status],
    queryFn: ({ signal }) => getVocabRootRunReport(runId, signal),
    staleTime: 0,
  });

  if (report.isError) return <FormError message={t("vocab.jobs.rootRun.report.failed")} />;
  const data = report.data;
  if (!data) return null;
  if (data.items.length === 0) {
    return (
      <span className="text-fg-muted">
        {isRunActive(status) ? null : t("vocab.jobs.rootRun.report.empty")}
      </span>
    );
  }

  return (
    <div className="grid gap-2">
      <span className="font-medium">
        {t("vocab.jobs.rootRun.report.title", {
          rejected: data.rejected.toLocaleString(i18n.language),
          flagged: data.flagged.toLocaleString(i18n.language),
          replaced: data.replaced.toLocaleString(i18n.language),
        })}
      </span>
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-border">
              <th className="py-1 pr-4 font-medium">{t("vocab.jobs.rootRun.report.entry")}</th>
              <th className="py-1 pr-4 font-medium">{t("vocab.jobs.rootRun.report.kind")}</th>
              <th className="py-1 pr-4 font-medium">{t("vocab.jobs.rootRun.report.answer")}</th>
              <th className="py-1 font-medium">{t("vocab.jobs.rootRun.report.reason")}</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((item) => {
              const hebrew = item.strongsKey.startsWith("H");
              return (
                <tr key={item.entryId} className="border-b border-border/50 align-top">
                  <td className="py-2 pr-4">
                    <Link
                      to={`/edit/vocab/${item.entryId}`}
                      lang={hebrew ? "he" : "grc"}
                      dir={hebrew ? "rtl" : undefined}
                      className="font-original text-base text-accent underline"
                    >
                      {item.lemma}
                    </Link>{" "}
                    <span className="text-fg-muted">{item.strongsKey}</span>
                  </td>
                  <td className="py-2 pr-4">
                    <StatusBadge tone={ITEM_TONE[item.kind]}>
                      {t(`vocab.jobs.rootRun.report.kinds.${item.kind}`)}
                    </StatusBadge>
                  </td>
                  <td className="py-2 pr-4">{item.answer ?? "–"}</td>
                  <td className="py-2 text-fg-muted">{item.reason}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/**
 * One agent run of the vocab jobs section: course, rank range, profile (and for roots the option "Vorbefüllung prüfen"), a dry-run estimate
 * the start depends on, and the newest run of its mode with cancel/resume. The root run adds the run report with links to the editor.
 */
export function VocabAgentRunCard({ kind }: { kind: VocabAgentRunKind }) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const config = KIND_CONFIG[kind];
  const number = (value: number) => value.toLocaleString(i18n.language);

  const [scope, setScope] = useState<RunScope>("nt");
  const [profile, setProfile] = useState<string>();
  const [from, setFrom] = useState("1");
  const [to, setTo] = useState(String(RUN_TO_RANK));
  const [batch, setBatch] = useState(String(DEFAULT_BATCH_SIZE));
  const [checkPrefill, setCheckPrefill] = useState(false);
  /** The run started in this view; before that the newest run of this mode is shown. */
  const [startedRunId, setStartedRunId] = useState<number>();

  const profiles = useAgentProfiles();
  const profileList = profiles.data ?? [];
  const profileName = profile ?? profileList.find((p) => p.usable)?.name ?? "";
  const profileUsable = profileList.some((p) => p.name === profileName && p.usable);
  const fromRank = parseRank(from);
  const toRank = parseRank(to);
  const batchSize = parseBatchSize(batch);
  const request: VocabRootRunRange | null =
    profileUsable &&
    fromRank !== null &&
    toRank !== null &&
    toRank >= fromRank &&
    batchSize !== null
      ? {
          course: RUN_COURSE[scope],
          fromRank,
          toRank,
          batchSize,
          profile: profileName,
          ...(kind === "root" && checkPrefill ? { checkPrefill: true } : {}),
        }
      : null;

  /** Dry run of the range (no model, nothing written); the start is only enabled for the estimate shown. */
  const estimate = useQuery<VocabFormRunEstimate>({
    queryKey: [...config.estimatePath, request],
    queryFn: ({ signal }) => config.estimate(request as VocabRootRunRange, signal),
    enabled: request !== null,
    retry: false,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });

  const agentRuns = useAgentRuns();
  const latestRunId = agentRuns.data?.find((run) => run.mode === config.mode)?.id;
  const runId = startedRunId ?? latestRunId ?? null;
  const run = useAgentRun(runId);
  const runData = run.data;
  const runStatus = runData?.status;
  const runActive = runStatus !== undefined && isRunActive(runStatus);
  const cancelRun = useCancelAgentRun();
  const resumeRun = useResumeAgentRun();

  const startRun = useMutation({
    mutationFn: (body: VocabRootRunRange) => config.start(body),
    onSuccess: (started: AgentRun) => {
      queryClient.setQueryData<AgentRun>(agentKeys.run(started.id), started);
      setStartedRunId(started.id);
      return queryClient.invalidateQueries({ queryKey: agentKeys.runs });
    },
  });

  useEffect(() => {
    // A finished run has written forms or roots: the editor lists (filters "von KI", "ohne Wurzel") must read them again.
    if (runId !== null && runStatus !== undefined && !isRunActive(runStatus)) {
      void queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all });
    }
  }, [runId, runStatus, queryClient]);

  const busy = runActive || startRun.isPending;
  const estimateData = estimate.isPlaceholderData ? undefined : estimate.data;
  const ready = request !== null && estimateData !== undefined && !busy;
  const noEntries = estimate.error instanceof ApiError && estimate.error.status === 404;
  const start = () => {
    if (request !== null && ready) startRun.mutate(request);
  };

  return (
    <li className="grid gap-3 py-3" data-run-kind={kind}>
      <div className="grid gap-1">
        <span className="font-medium">{t(`${config.prefix}.title`)}</span>
        <span className="text-sm text-fg-muted">{t(`${config.prefix}.intro`)}</span>
      </div>
      <div className="flex flex-wrap items-end gap-3">
        <Select
          label={t("vocab.jobs.forms.course")}
          value={scope}
          onChange={(value) => setScope(value as RunScope)}
          disabled={busy}
          options={RUN_SCOPES.map((value) => ({
            value,
            label: t(`vocab.jobs.meanings.scopes.${value}`),
          }))}
        />
        <TextField
          type="number"
          size="sm"
          label={t("vocab.editor.rankFrom")}
          min={1}
          value={from}
          onChange={setFrom}
        />
        <TextField
          type="number"
          size="sm"
          label={t("vocab.editor.rankTo")}
          min={1}
          value={to}
          onChange={setTo}
        />
        <TextField
          type="number"
          size="sm"
          label={t("vocab.jobs.batchSize")}
          title={t("vocab.jobs.batchSizeHint", { max: MAX_BATCH_SIZE })}
          min={1}
          max={MAX_BATCH_SIZE}
          value={batch}
          onChange={setBatch}
          invalid={batchSize === null}
          disabled={busy}
        />
        <Select
          label={t("vocab.jobs.forms.profile")}
          value={profileName}
          onChange={setProfile}
          disabled={profileList.length === 0 || busy}
          options={profileList.map((p) => ({
            value: p.name,
            label: p.usable
              ? `${p.name} · ${p.model}`
              : `${p.name} · ${t("agent.dialog.unusable", { reason: p.unusableReason ?? t("agent.dialog.noReason") })}`,
            disabled: !p.usable,
            title: p.unusableReason,
          }))}
        />
        {kind === "root" ? (
          <Switch
            checked={checkPrefill}
            onCheckedChange={setCheckPrefill}
            disabled={busy}
            title={t("vocab.jobs.rootRun.checkPrefillHint")}
          >
            {t("vocab.jobs.rootRun.checkPrefill")}
          </Switch>
        ) : null}
        <Button variant="primary" size="sm" disabled={!ready} onClick={start}>
          {startRun.isPending ? t("agent.dialog.starting") : t(`${config.prefix}.start`)}
        </Button>
      </div>
      {batchSize === null ? (
        <span className="text-sm text-fg-muted">
          {t("vocab.jobs.batchSizeInvalid", { max: MAX_BATCH_SIZE })}
        </span>
      ) : request === null ? (
        <span className="text-sm text-fg-muted">{t("agent.dialog.estimate.needRange")}</span>
      ) : noEntries ? (
        <span className="text-sm text-fg-muted">{t(`${config.prefix}.noEntries`)}</span>
      ) : estimate.isError ? (
        <FormError message={t("agent.dialog.estimate.failed")} />
      ) : estimateData ? (
        <div className="grid gap-3">
          <span className="text-sm text-fg-muted">
            {t("vocab.jobs.forms.ranks", {
              from: number(estimateData.fromRank),
              to: number(estimateData.toRank),
            })}
          </span>
          <div className="grid grid-cols-2 gap-2 md:grid-cols-3">
            <StatTile title={t("vocab.jobs.forms.entries")} value={number(estimateData.entries)} />
            <StatTile
              title={t("agent.dialog.estimate.packages")}
              value={number(estimateData.batches)}
            />
            <StatTile
              title={t("agent.dialog.estimate.input")}
              value={number(estimateData.estimatedInputTokens)}
            />
            <StatTile
              title={t("agent.dialog.estimate.output")}
              value={number(estimateData.estimatedOutputTokens)}
            />
            <StatTile
              title={t("agent.dialog.estimate.cost")}
              value={formatCost(estimateData.estimatedCostUsd, i18n.language)}
            />
          </div>
          {estimateData.warnings.map((warning) => (
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
      {startRun.error ? <FormError message={t(`${config.prefix}.startFailed`)} /> : null}
      {cancelRun.isError ? <FormError message={t("agent.runs.cancelFailed")} /> : null}
      {resumeRun.isError ? <FormError message={t("agent.runs.resumeFailed")} /> : null}
      {runData ? (
        <div className="grid gap-2 rounded-md border border-border p-3 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <StatusBadge tone={runTone(runData.status)}>
              {t(`agent.status.${runData.status}`)}
            </StatusBadge>
            <span className="tabular-nums text-fg-muted">
              {t("agent.runs.tokensCell", {
                input: number(runData.inputTokens),
                output: number(runData.outputTokens),
              })}{" "}
              · {formatCost(runData.costUsd, i18n.language)}
            </span>
          </div>
          {runData.statusNote ? <span className="text-fg-muted">{runData.statusNote}</span> : null}
          <div className="flex flex-wrap gap-2">
            {runActive ? (
              <Button
                variant="secondary"
                size="sm"
                disabled={cancelRun.isPending}
                onClick={() => cancelRun.mutate(runData.id)}
              >
                {t("agent.runs.cancel")}
              </Button>
            ) : null}
            {canResumeRun(runData.status) ? (
              <Button
                variant="secondary"
                size="sm"
                disabled={resumeRun.isPending}
                onClick={() => resumeRun.mutate({ id: runData.id })}
              >
                {t("agent.runs.resume")}
              </Button>
            ) : null}
          </div>
          {kind === "root" ? <RootRunReport runId={runData.id} status={runData.status} /> : null}
        </div>
      ) : null}
    </li>
  );
}
