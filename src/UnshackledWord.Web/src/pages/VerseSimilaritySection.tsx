import { useQueryClient } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { StatusBadge, type StatusTone } from "@/components/StatusBadge";
import { TextField } from "@/components/TextField";
import { ApiError, describeApiError } from "@/lib/api";
import {
  invalidateReaderSimilarVerses,
  testamentRecall,
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

function usePercent() {
  const { i18n } = useTranslation();
  return new Intl.NumberFormat(i18n.language, {
    style: "percent",
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  });
}

/** Rank text: the number, or "fehlt" when the pair is not among the stored hits. */
function rankText(t: TFunction, rank: number | undefined): string {
  return rank === undefined ? t("maintainer.verseSimilarity.evaluation.missing") : String(rank);
}

/**
 * Evaluation of a run (roadmap 0027.6f): recall@30 and @10 against the raw TSK links per testament, and the golden pairs with their rank in
 * both directions ("fehlt" when missing).
 */
function RunEvaluation({ run }: { run: VerseSimilarityRun }) {
  const { t, i18n } = useTranslation();
  const percent = usePercent();
  const number = new Intl.NumberFormat(i18n.language);
  const evaluation = run.evaluationResult;
  if (run.status !== "succeeded") return null;

  return (
    <section
      aria-labelledby="verse-similarity-evaluation"
      className="flex flex-col gap-2 rounded-md border border-border p-3 text-sm"
    >
      <h3 id="verse-similarity-evaluation" className="font-semibold">
        {t("maintainer.verseSimilarity.evaluation.title")}
      </h3>
      {!evaluation ? (
        <p className="text-fg-muted">{t("maintainer.verseSimilarity.evaluation.none")}</p>
      ) : evaluation.error ? (
        <p role="alert" className="font-medium text-accent">
          {t("maintainer.verseSimilarity.evaluation.failed", { error: evaluation.error })}
        </p>
      ) : (
        <>
          <p className="text-fg-muted">{t("maintainer.verseSimilarity.evaluation.intro")}</p>
          <dl className="flex flex-wrap gap-x-6 gap-y-1">
            {evaluation.testaments.map((entry) => (
              <div key={entry.testament} className="flex flex-col">
                <dt className="text-fg-muted">
                  {t(`maintainer.verseSimilarity.evaluation.testament.${entry.testament}`)}
                </dt>
                <dd className="tabular-nums">
                  {t("maintainer.verseSimilarity.evaluation.recall", {
                    at30: percent.format(entry.recallAt30),
                    at10: percent.format(entry.recallAt10),
                  })}{" "}
                  <span className="text-fg-muted">
                    (
                    {t("maintainer.verseSimilarity.evaluation.links", {
                      value: number.format(entry.links),
                    })}
                    )
                  </span>
                </dd>
              </div>
            ))}
          </dl>
          <h4 className="text-fg-muted">
            {t("maintainer.verseSimilarity.evaluation.goldenPairs")}
          </h4>
          <ul className="flex flex-col gap-1">
            {evaluation.goldenPairs.map((pair) => (
              <li
                key={`${pair.sourceVerseId}-${pair.targetVerseId}`}
                className="flex flex-wrap gap-x-3"
              >
                <span>
                  {pair.source} → {pair.target}
                </span>
                <span className={pair.rank === undefined ? "text-fg-muted" : "tabular-nums"}>
                  {pair.rank === undefined
                    ? t("maintainer.verseSimilarity.evaluation.missing")
                    : t("maintainer.verseSimilarity.evaluation.rank", { rank: pair.rank })}
                </span>
                <span className="text-fg-muted tabular-nums">
                  {t("maintainer.verseSimilarity.evaluation.reverse", {
                    rank: rankText(t, pair.reverseRank),
                  })}
                </span>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}

/** Golden pair ranks of a run as a compact row cell ("– · 13 · 6 · 1 · 4"), each with the pair as accessible name. */
function GoldenRanksCell({ run }: { run: VerseSimilarityRun }) {
  const { t } = useTranslation();
  const pairs = run.evaluationResult?.goldenPairs ?? [];
  if (pairs.length === 0) return <>–</>;
  return (
    <span className="flex gap-1 tabular-nums">
      {pairs.map((pair, index) => {
        const label = t("maintainer.verseSimilarity.evaluation.pairRank", {
          source: pair.source,
          target: pair.target,
          rank: rankText(t, pair.rank),
        });
        return (
          <span key={`${pair.sourceVerseId}-${pair.targetVerseId}`} className="flex gap-1">
            {index > 0 ? <span aria-hidden="true">·</span> : null}
            <span title={label}>
              <span aria-hidden="true">{pair.rank ?? "–"}</span>
              <span className="sr-only">{label}</span>
            </span>
          </span>
        );
      })}
    </span>
  );
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
  const percent = usePercent();
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
      {last ? <RunEvaluation run={last} /> : null}

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
                  "recallOt",
                  "recallNt",
                  "golden",
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
                  {(["old", "new"] as const).map((testament) => {
                    const recall = testamentRecall(run.evaluationResult, testament);
                    return (
                      <td key={testament} className="px-2 py-1 tabular-nums">
                        {recall ? percent.format(recall.recallAt30) : "–"}
                      </td>
                    );
                  })}
                  <td className="px-2 py-1">
                    <GoldenRanksCell run={run} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}
