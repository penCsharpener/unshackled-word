import type { TFunction } from "i18next";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { RequireRole } from "@/components/RequireRole";
import { StatusBadge, type StatusTone } from "@/components/StatusBadge";
import { ApiError, describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { isAdmin } from "@/lib/auth/roles";
import { formatNumber } from "@/lib/chart";
import { disabledReason, runnableKeys, summarize } from "@/lib/setup/catalog";
import {
  type RunSetupRequest,
  SETUP_GROUPS,
  type SetupGroup,
  type SetupJob,
  type SetupStep,
  type SetupStepKey,
  type SetupStepStatus,
} from "@/lib/setup/types";
import { useRunSetup, useSetupStatus } from "@/lib/setup/useSetup";

const TONES: Record<SetupStepStatus | "running", StatusTone> = {
  green: "ok",
  amber: "warning",
  red: "error",
  grey: "neutral",
  running: "info",
};

/** Ersteinrichtung (roadmap 0031): seed steps with their state from row counts; admin only. */
export function SetupPage() {
  return (
    <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
      <RequireRole requires="admin">
        <SetupView />
      </RequireRole>
    </section>
  );
}

function SetupView() {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const status = useSetupStatus(isAdmin(user));
  const run = useRunSetup();

  const labels = (key: SetupStepKey) => t(`setup.step.${key}`);
  const steps = status.data?.steps ?? [];
  const job = status.data?.job;
  const jobRunning = job?.status === "running" || run.isPending;
  const { done, total } = summarize(steps);
  const allDone = total > 0 && done === total;
  const canRunAll = runnableKeys(steps, { all: true }).length > 0;
  const onRun = (request: RunSetupRequest) => run.mutate(request);

  const runError = run.error;
  const runMessage = !runError
    ? ""
    : runError instanceof ApiError && runError.status === 409
      ? t("setup.disabled.jobRunning")
      : describeApiError(runError, runError.message);

  const heading = <h1 className="text-2xl font-semibold">{t("setup.title")}</h1>;

  if (status.isPending) {
    return (
      <>
        {heading}
        <p role="status" className="text-fg-muted">
          {t("setup.loading")}
        </p>
      </>
    );
  }
  if (status.isError) {
    return (
      <>
        {heading}
        <FormError message={describeApiError(status.error, t("setup.loadFailed"))} />
        <div>
          <Button variant="secondary" onClick={() => void status.refetch()}>
            {t("setup.retry")}
          </Button>
        </div>
      </>
    );
  }

  const checked = new Date(status.data.checkedAt).toLocaleString(i18n.language, {
    dateStyle: "short",
    timeStyle: "short",
  });
  const runningStep = job?.status === "running" ? job.currentStep : undefined;

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-2">
        {heading}
        <Button
          variant="primary"
          disabled={jobRunning || allDone || !canRunAll}
          onClick={() => onRun({ all: true })}
        >
          {allDone ? t("setup.allDone") : t("setup.runAll")}
        </Button>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p role="status" className="text-sm text-fg-muted">
          {t("setup.stand", { time: checked, done, total })}
          {runningStep ? ` · ${t("setup.runningNow", { step: labels(runningStep) })}` : ""}
        </p>
        <Button
          variant="ghost"
          size="sm"
          aria-label={t("setup.refresh")}
          disabled={status.isFetching}
          onClick={() => void status.refetch()}
        >
          <svg
            aria-hidden="true"
            viewBox="0 0 24 24"
            className="size-4"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M21 12a9 9 0 1 1-3-6.7" />
            <path d="M21 3v6h-6" />
          </svg>
        </Button>
      </div>
      <p className="text-fg-muted">{t("setup.intro")}</p>
      <p role="note" className="text-sm text-fg-muted">
        {t("setup.fakeNote")}
      </p>
      <FormError message={runMessage} />

      {steps.length === 0 ? (
        <EmptyState title={t("setup.emptyCatalog")}>{t("setup.emptyCatalog")}</EmptyState>
      ) : (
        <div className="flex flex-col gap-4">
          {SETUP_GROUPS.map((group) => (
            <SetupGroupCard
              key={group}
              group={group}
              steps={steps.filter((s) => s.group === group)}
              allSteps={steps}
              jobRunning={jobRunning}
              runningKey={runningStep}
              job={job}
              labels={labels}
              onRun={onRun}
            />
          ))}
        </div>
      )}

      {job ? <SetupJobLog job={job} labels={labels} /> : null}
    </>
  );
}

function SetupGroupCard({
  group,
  steps,
  allSteps,
  jobRunning,
  runningKey,
  job,
  labels,
  onRun,
}: {
  group: SetupGroup;
  steps: SetupStep[];
  allSteps: SetupStep[];
  jobRunning: boolean;
  runningKey?: SetupStepKey;
  job?: SetupJob;
  labels: (key: SetupStepKey) => string;
  onRun: (request: RunSetupRequest) => void;
}) {
  const { t } = useTranslation();
  if (steps.length === 0) return null;
  const nothingToRun = runnableKeys(allSteps, { group }).length === 0;
  const sources = new Set(steps.map((s) => s.source));
  const uniform = sources.size === 1 ? steps[0] : undefined;
  const headingId = `setup-group-${group}`;

  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-baseline gap-x-3">
          <h2 id={headingId} className="text-lg font-semibold">
            {t(`setup.group.${group}`)}
          </h2>
          {uniform ? <span className="text-sm text-fg-muted">{sourceHint(t, uniform)}</span> : null}
        </div>
        <Button
          variant="secondary"
          size="sm"
          disabled={jobRunning || nothingToRun}
          title={nothingToRun ? t("setup.groupNothingToRun") : undefined}
          onClick={() => onRun({ group })}
        >
          {t("setup.runGroup")}
        </Button>
      </div>
      <ul className="flex flex-col divide-y divide-border">
        {steps.map((step) => (
          <SetupStepRow
            key={step.key}
            step={step}
            label={labels(step.key)}
            labels={labels}
            showSource={!uniform}
            jobRunning={jobRunning}
            running={runningKey === step.key}
            failedLines={job?.failedStep === step.key ? job.log.slice(-3) : undefined}
            onRun={(key) => onRun({ keys: [key] })}
          />
        ))}
      </ul>
    </section>
  );
}

function sourceHint(t: TFunction, step: SetupStep) {
  return t(`setup.source.${step.source}`, { host: step.sourceHost ?? "" });
}

function SetupStepRow({
  step,
  label,
  labels,
  showSource,
  jobRunning,
  running,
  failedLines,
  onRun,
}: {
  step: SetupStep;
  label: string;
  labels: (key: SetupStepKey) => string;
  showSource: boolean;
  jobRunning: boolean;
  running: boolean;
  failedLines?: string[];
  onRun: (key: SetupStepKey) => void;
}) {
  const { t, i18n } = useTranslation();
  const reason = disabledReason(step, jobRunning);
  const shown: SetupStepStatus | "running" = running ? "running" : step.status;
  const word = step.failed && !running ? t("setup.status.failed") : t(`setup.status.${shown}`);
  const detailId = `setup-row-${step.key}`;
  const reasonText = !reason
    ? undefined
    : reason === "missing"
      ? t("setup.disabled.missing", { steps: step.missing.map(labels).join(", ") })
      : reason === "readOnly"
        ? undefined
        : t(`setup.disabled.${reason}`);

  return (
    <li className="flex flex-wrap items-start gap-x-3 gap-y-1 py-2">
      <span aria-hidden="true" className="mt-1.5">
        <StatusBadge
          variant="dot"
          tone={step.failed && !running ? "warning" : TONES[shown]}
          busy={running}
        >
          {t(`setup.dot.${shown}`)}
        </StatusBadge>
      </span>
      <span className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-2">
        <span className="font-medium">{label}</span>
        {showSource ? <span className="text-sm text-fg-muted">{sourceHint(t, step)}</span> : null}
      </span>
      {step.runnable ? (
        <Button
          variant="secondary"
          size="sm"
          className="ml-auto"
          disabled={reason !== null}
          aria-describedby={reason ? detailId : undefined}
          title={reasonText}
          onClick={() => onRun(step.key)}
        >
          {reason === "notWired" ? t("setup.disabled.notWired") : t("setup.run")}
        </Button>
      ) : null}
      <div id={detailId} className="basis-full pl-6 text-sm text-fg-muted">
        <p>
          <span>{word}</span>
          {step.status === "grey" && step.missing.length > 0
            ? ` · ${t("setup.requires", { steps: step.missing.map(labels).join(", ") })}`
            : ""}
          {step.note ? ` · ${t(`setup.${step.note}`)}` : ""}
          {reason === "notWired" ? ` · ${t("setup.notWired")}` : ""}
        </p>
        <p>
          {step.tables.map((table, index) => (
            <span key={table.name}>
              {index > 0 ? " · " : ""}
              <span className="break-all font-mono">{table.name}</span>{" "}
              {table.populated === undefined
                ? formatNumber(table.rows, i18n.language)
                : table.populated
                  ? `${t("setup.populated")} (${formatNumber(table.rows, i18n.language)})`
                  : t("setup.notPopulated")}
            </span>
          ))}
        </p>
        {failedLines?.length ? (
          <ol className="mt-1 font-mono">
            {failedLines.map((line, index) => (
              // biome-ignore lint/suspicious/noArrayIndexKey: log lines are append-only and may repeat
              <li key={index}>{line}</li>
            ))}
          </ol>
        ) : null}
        {reasonText && reason !== "done" && reason !== "missing" ? (
          <p className="sr-only">{reasonText}</p>
        ) : null}
      </div>
    </li>
  );
}

function elapsedText(job: SetupJob): string {
  const end = job.finishedAt ? new Date(job.finishedAt).getTime() : Date.now();
  const seconds = Math.max(0, Math.round((end - new Date(job.startedAt).getTime()) / 1000));
  const mm = String(Math.floor(seconds / 60)).padStart(2, "0");
  const ss = String(seconds % 60).padStart(2, "0");
  return `${mm}:${ss}`;
}

function SetupJobLog({ job, labels }: { job: SetupJob; labels: (key: SetupStepKey) => string }) {
  const { t } = useTranslation();
  const scope =
    job.kind === "all"
      ? t("setup.job.all")
      : job.kind.startsWith("group:")
        ? t("setup.job.group", { group: t(`setup.group.${job.kind.slice("group:".length)}`) })
        : labels(job.kind as SetupStepKey);

  return (
    <div className="flex flex-col gap-3 rounded-md border border-border p-3">
      <h2 className="font-medium">
        {t("setup.job.title", { scope, elapsed: elapsedText(job) })}
        {" – "}
        <span role="status">{t(`setup.job.status.${job.status}`)}</span>
      </h2>
      {job.error ? (
        <p role="alert" className="text-sm font-medium text-accent">
          {t("setup.job.failed", { error: job.error })}
        </p>
      ) : null}
      {job.failedStep ? (
        <p className="text-sm text-fg-muted">
          {t("setup.stoppedAt", { step: labels(job.failedStep) })}
        </p>
      ) : null}
      <details open={job.status !== "succeeded"}>
        <summary className="cursor-pointer text-sm font-medium">{t("setup.job.log")}</summary>
        <ol
          className="mt-2 max-h-64 overflow-auto font-mono text-sm whitespace-pre text-fg-muted"
          aria-live="polite"
        >
          {job.log.map((line, index) => (
            // biome-ignore lint/suspicious/noArrayIndexKey: log lines are append-only and may repeat
            <li key={index}>{line}</li>
          ))}
        </ol>
      </details>
    </div>
  );
}
