import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { StatusBadge, type StatusTone } from "@/components/StatusBadge";
import { TextField } from "@/components/TextField";
import { ApiError, describeApiError } from "@/lib/api";
import {
  invalidateReaderSimilarVerses,
  useStartVerseSimilarityRebuild,
  useVerseSimilarityStatus,
  VERSE_SIMILARITY_BOUNDS,
  type VerseSimilarityParameters,
  type VerseSimilarityRun,
  type VerseSimilarityRunStatus,
} from "@/lib/maintainer/verseSimilarity";

type ParameterKey = keyof VerseSimilarityParameters;

const KEYS: ParameterKey[] = ["stopDfShare", "topK", "minScore", "phraseWeight"];
const TONES: Record<VerseSimilarityRunStatus, StatusTone> = {
  running: "info",
  succeeded: "ok",
  failed: "error",
};

/** The field text as a number; a decimal comma is accepted, an empty or unparsable text is NaN. */
function parse(text: string): number {
  const trimmed = text.trim().replace(",", ".");
  return trimmed === "" ? Number.NaN : Number(trimmed);
}

function inBounds(key: ParameterKey, value: number): boolean {
  const b = VERSE_SIMILARITY_BOUNDS[key];
  if (!Number.isFinite(value)) return false;
  if (key === "topK" && !Number.isInteger(value)) return false;
  const aboveMin = b.minExclusive ? value > b.min : value >= b.min;
  const belowMax = b.maxExclusive ? value < b.max : value <= b.max;
  return aboveMin && belowMax;
}

function RunStatus({ run }: { run: VerseSimilarityRun }) {
  const { t } = useTranslation();
  return (
    <StatusBadge tone={TONES[run.status]}>
      {t(`maintainer.verseSimilarity.status.${run.status}`)}
    </StatusBadge>
  );
}

/** Placeholder for the evaluation display of roadmap 0027.6f; shows nothing until a run carries an evaluation. */
function EvaluationPlaceholder() {
  return null;
}

/**
 * "Ähnliche Verse" card of the maintenance page (roadmap 0027.5f): the last run, the rebuild parameters prefilled with the configured
 * defaults, "Neu berechnen" (background job, polled), and the last five runs for comparison. A succeeded rebuild makes the reader's
 * cached similar verses stale.
 */
export function VerseSimilaritySection() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const status = useVerseSimilarityStatus(true);
  const start = useStartVerseSimilarityRebuild();
  const [edits, setEdits] = useState<Partial<Record<ParameterKey, string>>>({});

  const data = status.data;
  const runs = data?.runs ?? [];
  const last = runs[0];
  const running = Boolean(data?.running) || start.isPending;
  const job = data?.job;

  // A rebuild that just finished: the reader shows the new ranking after a refetch.
  const wasRunning = useRef(false);
  useEffect(() => {
    if (wasRunning.current && data && !data.running) {
      void invalidateReaderSimilarVerses(queryClient);
    }
    wasRunning.current = Boolean(data?.running);
  }, [data, queryClient]);

  const date = new Intl.DateTimeFormat(i18n.language, { dateStyle: "short", timeStyle: "short" });
  const number = new Intl.NumberFormat(i18n.language);
  const seconds = (ms?: number) =>
    ms === undefined
      ? "–"
      : t("maintainer.verseSimilarity.seconds", { value: (ms / 1000).toFixed(1) });
  const who = (run: VerseSimilarityRun) =>
    run.startedByName ?? t("maintainer.verseSimilarity.bySeed");

  const field = (key: ParameterKey) => edits[key] ?? (data ? String(data.defaults[key]) : "");
  const invalid = (key: ParameterKey) => !inBounds(key, parse(field(key)));
  const anyInvalid = KEYS.some(invalid);

  function submit() {
    start.mutate({
      stopDfShare: parse(field("stopDfShare")),
      topK: parse(field("topK")),
      minScore: parse(field("minScore")),
      phraseWeight: parse(field("phraseWeight")),
    });
  }

  const startMessage = !start.error
    ? ""
    : start.error instanceof ApiError && start.error.status === 409
      ? t("maintainer.verseSimilarity.busy")
      : describeApiError(start.error, t("maintainer.verseSimilarity.startFailed"));
  const loadMessage = status.error
    ? describeApiError(status.error, t("maintainer.verseSimilarity.loadFailed"))
    : "";

  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t("maintainer.verseSimilarity.title")}</h2>
      <p className="text-fg-muted">{t("maintainer.verseSimilarity.intro")}</p>
      <FormError message={loadMessage} />

      {last ? (
        <dl className="flex flex-wrap gap-x-6 gap-y-1 text-sm">
          <div className="flex gap-2">
            <dt className="text-fg-muted">{t("maintainer.verseSimilarity.lastRun")}</dt>
            <dd>
              <RunStatus run={last} /> {date.format(new Date(last.startedAt))}
            </dd>
          </div>
          <div className="flex gap-2">
            <dt className="text-fg-muted">{t("maintainer.verseSimilarity.by")}</dt>
            <dd>{who(last)}</dd>
          </div>
          <div className="flex gap-2">
            <dt className="text-fg-muted">{t("maintainer.verseSimilarity.duration")}</dt>
            <dd className="tabular-nums">{seconds(last.durationMs)}</dd>
          </div>
          <div className="flex gap-2">
            <dt className="text-fg-muted">{t("maintainer.verseSimilarity.pairs")}</dt>
            <dd className="tabular-nums">
              {last.pairCount === undefined ? "–" : number.format(last.pairCount)}
            </dd>
          </div>
          <div className="flex gap-2">
            <dt className="text-fg-muted">{t("maintainer.verseSimilarity.stopWords")}</dt>
            <dd className="tabular-nums">{last.stopWordCount ?? "–"}</dd>
          </div>
        </dl>
      ) : status.isSuccess ? (
        <p className="text-sm text-fg-muted">{t("maintainer.verseSimilarity.noRun")}</p>
      ) : null}
      {last?.error ? (
        <p role="alert" className="text-sm font-medium text-accent">
          {t("maintainer.verseSimilarity.failed", { error: last.error })}
        </p>
      ) : null}
      <EvaluationPlaceholder />

      <fieldset className="flex flex-wrap items-end gap-3" disabled={running || !data}>
        <legend className="sr-only">{t("maintainer.verseSimilarity.parameters")}</legend>
        {KEYS.map((key) => (
          <TextField
            key={key}
            inputMode="decimal"
            size="sm"
            label={t(`maintainer.verseSimilarity.params.${key}`)}
            title={t(`maintainer.verseSimilarity.hints.${key}`)}
            value={field(key)}
            invalid={Boolean(data) && invalid(key)}
            onChange={(value) => setEdits((previous) => ({ ...previous, [key]: value }))}
          />
        ))}
        <Button variant="primary" disabled={running || !data || anyInvalid} onClick={submit}>
          {running
            ? t("maintainer.verseSimilarity.running")
            : t("maintainer.verseSimilarity.rebuild")}
        </Button>
        <Button
          variant="ghost"
          disabled={running || Object.keys(edits).length === 0}
          onClick={() => setEdits({})}
        >
          {t("maintainer.verseSimilarity.reset")}
        </Button>
      </fieldset>
      {data && anyInvalid ? <FormError message={t("maintainer.verseSimilarity.invalid")} /> : null}
      <FormError message={startMessage} />

      {running && job ? (
        <ol className="max-h-32 overflow-y-auto font-mono text-sm text-fg-muted" aria-live="polite">
          {job.log.map((line, index) => (
            // biome-ignore lint/suspicious/noArrayIndexKey: log lines are append-only and may repeat
            <li key={index}>{line}</li>
          ))}
        </ol>
      ) : null}
      {running ? (
        <p role="status" className="text-sm text-fg-muted">
          {t("maintainer.verseSimilarity.progress")}
        </p>
      ) : null}

      {runs.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <caption className="sr-only">{t("maintainer.verseSimilarity.history")}</caption>
            <thead className="text-fg-muted">
              <tr>
                {[
                  "run",
                  "startedAt",
                  "by",
                  "status",
                  "stopDfShare",
                  "topK",
                  "minScore",
                  "phraseWeight",
                  "pairs",
                  "stopWords",
                  "duration",
                ].map((column) => (
                  <th key={column} scope="col" className="px-2 py-1 font-medium">
                    {t(`maintainer.verseSimilarity.columns.${column}`)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {runs.map((run) => (
                <tr key={run.id}>
                  <td className="px-2 py-1 tabular-nums">{run.id}</td>
                  <td className="px-2 py-1">{date.format(new Date(run.startedAt))}</td>
                  <td className="px-2 py-1">{who(run)}</td>
                  <td className="px-2 py-1">
                    <RunStatus run={run} />
                  </td>
                  <td className="px-2 py-1 tabular-nums">{run.parameters.stopDfShare}</td>
                  <td className="px-2 py-1 tabular-nums">{run.parameters.topK}</td>
                  <td className="px-2 py-1 tabular-nums">{run.parameters.minScore}</td>
                  <td className="px-2 py-1 tabular-nums">{run.parameters.phraseWeight}</td>
                  <td className="px-2 py-1 tabular-nums">
                    {run.pairCount === undefined ? "–" : number.format(run.pairCount)}
                  </td>
                  <td className="px-2 py-1 tabular-nums">{run.stopWordCount ?? "–"}</td>
                  <td className="px-2 py-1 tabular-nums">{seconds(run.durationMs)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}
