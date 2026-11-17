import { Fragment, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { AgentRunScorePanel } from "@/components/AgentRunScorePanel";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { ProgressBar } from "@/components/ProgressBar";
import { StatusBadge } from "@/components/StatusBadge";
import { TextField } from "@/components/TextField";
import { formatCost, formatRunRange, formatTokens } from "@/lib/agent/format";
import { useAgentRuns, useCancelAgentRun, useResumeAgentRun } from "@/lib/agent/queries";
import { type AgentRun, canResumeRun, isRunActive, runTone } from "@/lib/agent/types";
import { describeApiErrors } from "@/lib/api";
import { bookName } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import { formatStamp } from "@/lib/format";

const positive = (value: string): number | undefined =>
  Number(value) > 0 ? Number(value) : undefined;

/** Cap mini-form of a budget-stopped run: raise one or both caps (empty keeps the stored cap), then continue. */
function ResumeForm({ run, onDone }: { run: AgentRun; onDone: () => void }) {
  const { t, i18n } = useTranslation();
  const resume = useResumeAgentRun();
  const [maxTokens, setMaxTokens] = useState("");
  const [maxCost, setMaxCost] = useState("");
  const errors = resume.isError ? describeApiErrors(resume.error) : [];
  return (
    <form
      className="flex flex-col gap-2 rounded-md border border-border p-3"
      onSubmit={(event) => {
        event.preventDefault();
        resume.mutate(
          { id: run.id, maxTokens: positive(maxTokens), maxCostUsd: positive(maxCost) },
          { onSuccess: onDone },
        );
      }}
    >
      <p className="text-sm text-fg-muted">
        {t("agent.runs.resumeHint", {
          tokens: formatTokens(run.inputTokens + run.outputTokens, i18n.language),
          cost: formatCost(run.costUsd, i18n.language),
        })}
      </p>
      <div className="flex flex-wrap items-end gap-3">
        <TextField
          label={t("agent.runs.newMaxTokens")}
          type="number"
          min={1}
          size="sm"
          required={false}
          placeholder={run.maxTokens ? String(run.maxTokens) : undefined}
          value={maxTokens}
          onChange={setMaxTokens}
        />
        <TextField
          label={t("agent.runs.newMaxCost")}
          type="number"
          min={0}
          step="any"
          size="sm"
          required={false}
          placeholder={run.maxCostUsd ? String(run.maxCostUsd) : undefined}
          value={maxCost}
          onChange={setMaxCost}
        />
        <Button type="submit" size="sm" variant="primary" disabled={resume.isPending}>
          {resume.isPending ? t("agent.runs.resuming") : t("agent.runs.resumeConfirm")}
        </Button>
        <Button size="sm" variant="ghost" onClick={onDone}>
          {t("agent.runs.cancelForm")}
        </Button>
      </div>
      {errors.map((message) => (
        <FormError key={message} message={message} />
      ))}
      {resume.isError && errors.length === 0 ? (
        <FormError message={t("agent.runs.resumeFailed")} />
      ) : null}
    </form>
  );
}

function RunActions({
  run,
  scoring,
  onToggleScore,
}: {
  run: AgentRun;
  scoring: boolean;
  onToggleScore: () => void;
}) {
  const { t } = useTranslation();
  const cancel = useCancelAgentRun();
  const resume = useResumeAgentRun();
  const [confirming, setConfirming] = useState(false);
  const [capForm, setCapForm] = useState(false);
  const error = cancel.isError
    ? describeApiErrors(cancel.error)[0] || t("agent.runs.cancelFailed")
    : resume.isError
      ? describeApiErrors(resume.error)[0] || t("agent.runs.resumeFailed")
      : "";
  return (
    <div className="flex flex-col items-start gap-2">
      <div className="flex flex-wrap gap-2">
        {isRunActive(run.status) ? (
          confirming ? (
            <>
              <Button
                size="sm"
                variant="primary"
                disabled={cancel.isPending}
                onClick={() => cancel.mutate(run.id, { onSettled: () => setConfirming(false) })}
              >
                {t("agent.runs.cancelConfirm")}
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setConfirming(false)}>
                {t("agent.runs.cancelKeep")}
              </Button>
            </>
          ) : (
            <Button size="sm" onClick={() => setConfirming(true)}>
              {t("agent.runs.cancel")}
            </Button>
          )
        ) : null}
        {canResumeRun(run.status) && !capForm ? (
          <Button
            size="sm"
            disabled={resume.isPending}
            onClick={() =>
              run.status === "budget-exceeded" ? setCapForm(true) : resume.mutate({ id: run.id })
            }
          >
            {t("agent.runs.resume")}
          </Button>
        ) : null}
        <Button size="sm" aria-pressed={scoring} onClick={onToggleScore}>
          {t("agent.score.toggle")}
        </Button>
        {run.proposedVerses > 0 ? (
          <Link
            to={`/edit/proposals/${run.id}`}
            className="text-accent underline underline-offset-2"
          >
            {t("agent.runs.review", { count: run.openProposals })}
          </Link>
        ) : null}
      </div>
      {capForm ? <ResumeForm run={run} onDone={() => setCapForm(false)} /> : null}
      <FormError message={error} />
    </div>
  );
}

/** "KI-Läufe" section of the maintenance page: the agent runs with progress, tokens, cost and open proposals; polls while one runs. */
export function AgentRunsSection() {
  const { t, i18n } = useTranslation();
  const runs = useAgentRuns();
  const structure = useBibleStructure();
  const books = structure.data;
  const name = (id: number) => {
    const book = books?.find((b) => b.id === id);
    return book ? bookName(book, i18n.language) : `#${id}`;
  };
  const [scoringId, setScoringId] = useState<number | null>(null);
  const columns = [
    "run",
    "range",
    "model",
    "status",
    "progress",
    "tokens",
    "cost",
    "proposals",
    "actions",
  ];
  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t("agent.runs.title")}</h2>
      <p className="text-fg-muted">{t("agent.runs.intro")}</p>
      {runs.isError ? <FormError message={t("agent.runs.loadFailed")} /> : null}
      {runs.isSuccess && runs.data.length === 0 ? (
        <p className="text-sm text-fg-muted">{t("agent.runs.empty")}</p>
      ) : null}
      {runs.data && runs.data.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <caption className="sr-only">{t("agent.runs.title")}</caption>
            <thead className="text-fg-muted">
              <tr className="border-b border-border">
                {columns.map((column) => (
                  <th key={column} scope="col" className="px-2 py-1 font-medium">
                    {t(`agent.runs.columns.${column}`)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {runs.data.map((run) => (
                <Fragment key={run.id}>
                  <tr className="border-b border-border align-top">
                    <td className="whitespace-nowrap px-2 py-2 tabular-nums">
                      #{run.id}
                      <p className="text-xs text-fg-muted">
                        {formatStamp(run.createdAt, i18n.language)}
                      </p>
                    </td>
                    <td className="whitespace-nowrap px-2 py-2">
                      {formatRunRange(run, name(run.book))}
                      <p className="text-xs text-fg-muted">
                        {t(`agent.mode.${run.mode}`, { defaultValue: run.mode })}
                      </p>
                    </td>
                    <td className="px-2 py-2">
                      {run.profile}
                      <p className="text-xs text-fg-muted">{run.model}</p>
                    </td>
                    <td className="px-2 py-2">
                      <StatusBadge tone={runTone(run.status)}>
                        {t(`agent.status.${run.status}`)}
                      </StatusBadge>
                      {run.statusNote ? (
                        <p className="mt-1 max-w-64 text-xs text-fg-muted">{run.statusNote}</p>
                      ) : null}
                    </td>
                    <td className="min-w-32 px-2 py-2">
                      <ProgressBar
                        value={run.packagesCompleted + run.packagesFailed}
                        max={run.packages}
                        label={t("agent.runs.packages")}
                        tone={
                          run.packagesFailed > 0
                            ? "warning"
                            : run.status === "completed"
                              ? "ok"
                              : "info"
                        }
                      />
                    </td>
                    <td className="whitespace-nowrap px-2 py-2 tabular-nums">
                      {t("agent.runs.tokensCell", {
                        input: formatTokens(run.inputTokens, i18n.language),
                        output: formatTokens(run.outputTokens, i18n.language),
                      })}
                      <p className="text-xs text-fg-muted">
                        {t("agent.runs.cacheRate", { rate: Math.round(run.cacheRate * 100) })}
                      </p>
                    </td>
                    <td className="whitespace-nowrap px-2 py-2 tabular-nums">
                      {formatCost(run.costUsd, i18n.language)}
                      {run.maxCostUsd ? (
                        <p className="text-xs text-fg-muted">
                          {t("agent.runs.of", { cap: formatCost(run.maxCostUsd, i18n.language) })}
                        </p>
                      ) : null}
                    </td>
                    <td className="whitespace-nowrap px-2 py-2 tabular-nums">
                      {t("agent.runs.proposalsCell", {
                        open: run.openProposals,
                        total: run.proposedVerses,
                      })}
                      {run.flaggedVerses > 0 ? (
                        <p className="text-xs text-fg-muted">
                          {t("agent.runs.flagged", { count: run.flaggedVerses })}
                        </p>
                      ) : null}
                    </td>
                    <td className="px-2 py-2">
                      <RunActions
                        run={run}
                        scoring={scoringId === run.id}
                        onToggleScore={() => setScoringId(scoringId === run.id ? null : run.id)}
                      />
                    </td>
                  </tr>
                  {scoringId === run.id ? (
                    <tr className="border-b border-border">
                      <td colSpan={columns.length} className="px-2 py-3">
                        <AgentRunScorePanel runId={run.id} />
                      </td>
                    </tr>
                  ) : null}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}
