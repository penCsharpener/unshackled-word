import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { StatusBadge } from "@/components/StatusBadge";
import { formatCost } from "@/lib/agent/format";
import { useAgentScore, useStoredAgentScore } from "@/lib/agent/queries";
import type {
  AgentScore,
  AgentScoreError,
  AgentScoreRatio,
  AgentScoreVerse,
} from "@/lib/agent/types";
import { ApiError, describeApiErrors } from "@/lib/api";
import { chapterPath } from "@/lib/bible/books";
import { formatStamp } from "@/lib/format";

export interface AgentRunScorePanelProps {
  runId: number;
}

const percent = (value: number | undefined, language: string): string =>
  value === undefined
    ? "–"
    : new Intl.NumberFormat(language, { style: "percent", maximumFractionDigits: 1 }).format(value);

const number = (value: number | undefined, language: string): string =>
  value === undefined
    ? "–"
    : new Intl.NumberFormat(language, { maximumFractionDigits: 1 }).format(value);

function Ratio({ label, ratio }: { label: string; ratio: AgentScoreRatio }) {
  const { i18n } = useTranslation();
  return (
    <div className="flex flex-col rounded-md border border-border p-2">
      <dt className="text-xs text-fg-muted">{label}</dt>
      <dd className="text-base font-semibold tabular-nums">
        {percent(ratio.accuracy, i18n.language)}
        <span className="ml-2 text-xs font-normal text-fg-muted">
          {ratio.correct} / {ratio.total}
        </span>
      </dd>
    </div>
  );
}

function ErrorList({ title, errors }: { title: string; errors: AgentScoreError[] }) {
  if (errors.length === 0) return null;
  return (
    <div>
      <p className="font-medium">{title}</p>
      <ul className="list-disc pl-5">
        {errors.map((error) => (
          <li key={`${error.item}|${error.expected}|${error.actual}`}>
            {error.text ? `${error.text} (${error.item})` : error.item}
            {error.expected || error.actual
              ? `: ${error.expected ?? "–"} → ${error.actual ?? "–"}`
              : null}
          </li>
        ))}
      </ul>
    </div>
  );
}

const errorCount = (verse: AgentScoreVerse) =>
  verse.lemmaErrors.length +
  verse.roleErrors.length +
  verse.missingLinks.length +
  verse.extraLinks.length;

function VerseRow({ verse, runId }: { verse: AgentScoreVerse; runId: number }) {
  const { t } = useTranslation();
  const count = errorCount(verse);
  const failed = verse.status === "no-proposal";
  return (
    <details className="rounded-md border border-border px-3 py-2">
      <summary className="cursor-pointer text-sm">
        <span className="font-medium">{verse.ref}</span>{" "}
        <span className="text-fg-muted">
          {t(`agent.score.verseStatus.${verse.status}`, { defaultValue: verse.status })}
          {verse.status === "proposal"
            ? ` · ${t("agent.score.errorCount", { count })} · ${t("agent.score.flagCount", { count: verse.flags.length })}`
            : null}
        </span>
      </summary>
      <div className="mt-2 flex flex-col gap-2 text-sm">
        {failed ? <FormError message={t("agent.score.hardRejection")} /> : null}
        <ErrorList title={t("agent.score.lemmaErrors")} errors={verse.lemmaErrors} />
        <ErrorList title={t("agent.score.roleErrors")} errors={verse.roleErrors} />
        <ErrorList title={t("agent.score.missingLinks")} errors={verse.missingLinks} />
        <ErrorList title={t("agent.score.extraLinks")} errors={verse.extraLinks} />
        {verse.flags.length > 0 ? (
          <p>
            <span className="font-medium">{t("agent.score.flags")}</span>{" "}
            {verse.flags.map((flag) => t(`agent.flag.${flag}`, { defaultValue: flag })).join(", ")}
          </p>
        ) : null}
        <p className="flex flex-wrap gap-3">
          <Link
            to={chapterPath({ bookId: verse.bibleBookId, chapter: verse.chapter })}
            className="text-accent underline underline-offset-2"
          >
            {t("agent.score.openVerse")}
          </Link>
          {verse.status === "proposal" ? (
            <Link
              to={`/edit/proposals/${runId}`}
              className="text-accent underline underline-offset-2"
            >
              {t("agent.score.openProposals")}
            </Link>
          ) : null}
        </p>
      </div>
    </details>
  );
}

function ScoreResult({ score }: { score: AgentScore }) {
  const { t, i18n } = useTranslation();
  const lang = i18n.language;
  const { bulkMerge, verses, links, cost, flags } = score;
  const problem = (verse: AgentScoreVerse) =>
    verse.status === "no-proposal" || errorCount(verse) > 0;
  const shown = [...score.perVerse].sort((a, b) => Number(problem(b)) - Number(problem(a)));
  return (
    <div className="flex flex-col gap-3">
      <div
        className="flex flex-col gap-2 rounded-md border border-border p-3"
        data-testid="bulk-merge-bar"
      >
        <StatusBadge tone={bulkMerge.meets ? "ok" : "error"}>
          {bulkMerge.meets ? t("agent.score.barPass") : t("agent.score.barFail")}
        </StatusBadge>
        <ul className="text-sm">
          {bulkMerge.checks.map((check) => (
            <li key={check.code}>
              {check.ok ? "✓" : "✕"}{" "}
              {t(`agent.score.check.${check.code}`, { defaultValue: check.code })}
              {": "}
              {check.code === "lemmas" || check.code === "role-morphemes"
                ? `${percent(check.value, lang)} (${t("agent.score.min", { value: percent(check.required, lang) })})`
                : check.code === "hard-rejections"
                  ? `${number(check.value, lang)} (${t("agent.score.max", { value: number(check.required, lang) })})`
                  : `${number(check.value, lang)} (${t("agent.score.min", { value: number(check.required, lang) })})`}
            </li>
          ))}
        </ul>
        {bulkMerge.reasons.length > 0 ? (
          <ul className="list-disc pl-5 text-sm text-fg-muted">
            {bulkMerge.reasons.map((reason) => (
              <li key={reason}>{reason}</li>
            ))}
          </ul>
        ) : null}
      </div>
      <p className="text-xs text-fg-muted">
        {t("agent.score.meta", {
          reference: score.referenceVersion,
          stamp: formatStamp(score.computedAt, lang),
          scored: verses.scored,
          total: verses.reference,
          silent: verses.silent,
          notInRun: verses.notInRun,
        })}
      </p>
      <dl className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
        <Ratio label={t("agent.score.lemmas")} ratio={score.lemmas} />
        <Ratio label={t("agent.score.stts")} ratio={score.stts} />
        <Ratio label={t("agent.score.partLemmas")} ratio={score.partLemmas} />
        <Ratio label={t("agent.score.roles")} ratio={score.roles} />
        <Ratio label={t("agent.score.morphemes")} ratio={score.morphemes} />
        <Ratio label={t("agent.score.roleMorphemes")} ratio={score.roleMorphemes} />
      </dl>
      <p className="text-sm">
        {t("agent.score.links", {
          precision: percent(links.precision, lang),
          recall: percent(links.recall, lang),
          matched: links.matched,
          expected: links.expected,
          actual: links.actual,
        })}
      </p>
      <p className="text-sm">
        {t("agent.score.hardRejections", { count: verses.hardRejections })}
        {" · "}
        {t("agent.score.flagsPerVerse", {
          rate: number(flags.perVerse, lang),
          total: flags.total,
        })}
      </p>
      <p className="text-sm">
        {t("agent.score.costPerVerse", {
          input: number(cost.inputTokensPerVerse, lang),
          output: number(cost.outputTokensPerVerse, lang),
          cost: cost.costUsdPerVerse === undefined ? "–" : formatCost(cost.costUsdPerVerse, lang),
          verses: cost.verses,
        })}
      </p>
      {shown.length > 0 ? (
        <div className="flex flex-col gap-2">
          <h4 className="text-sm font-semibold">{t("agent.score.perVerse")}</h4>
          {shown.map((verse) => (
            <VerseRow key={verse.ref} verse={verse} runId={score.runId} />
          ))}
        </div>
      ) : null}
    </div>
  );
}

/** "Bewertung" of one agent run (roadmap 0018.10f): scores it against the frozen reference file, shows accuracies, the bulk-merge bar and the errors per verse. */
export function AgentRunScorePanel({ runId }: AgentRunScorePanelProps) {
  const { t } = useTranslation();
  const query = useAgentScore(runId);
  const stored = useStoredAgentScore(runId);
  const score = query.data ?? stored.data;
  const noReference = query.error instanceof ApiError && query.error.status === 409;
  const message = query.isError && !noReference ? describeApiErrors(query.error)[0] : "";
  return (
    <section
      aria-label={t("agent.score.title")}
      className="flex flex-col gap-3 rounded-md border border-border bg-surface p-3"
    >
      <div className="flex flex-wrap items-center gap-3">
        <h3 className="text-base font-semibold">{t("agent.score.title")}</h3>
        <Button
          size="sm"
          variant="primary"
          disabled={query.isFetching}
          onClick={() => void query.refetch()}
        >
          {query.isFetching
            ? t("agent.score.scoring")
            : score
              ? t("agent.score.rescore")
              : t("agent.score.score")}
        </Button>
      </div>
      <p className="text-sm text-fg-muted">{t("agent.score.beforeMerge")}</p>
      {noReference ? (
        <p role="alert" className="text-sm">
          {t("agent.score.noReference")}
        </p>
      ) : null}
      {query.isError && !noReference ? (
        <FormError message={message || t("agent.score.failed")} />
      ) : null}
      {score ? <ScoreResult score={score} /> : null}
    </section>
  );
}
