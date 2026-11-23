import { useTranslation } from "react-i18next";
import { StatTile } from "@/components/StatTile";
import { formatNumber } from "@/lib/chart";
import type {
  SeedFilesDiff,
  SeedJob,
  SeedReportList,
  SeedRoundTripResult,
  SeedStepReport,
} from "@/lib/maintainer/seedJobs";

const COUNT_ORDER = [
  "notes",
  "variants",
  "verses",
  "rows",
  "exact",
  "shifted",
  "modified",
  "inserted",
  "updated",
  "skipped",
  "orphaned",
  "replaced",
  "new",
  "removed",
  "kept",
  "unchanged",
  "alignedChapters",
];

/** A capped list of entries; the total says how many more there are (all of them are in the report file). */
function ReportList({ label, list }: { label: string; list: SeedReportList }) {
  const { t, i18n } = useTranslation();
  if (list.total === 0) return null;
  return (
    <details className="rounded-md border border-border bg-surface px-3 py-2">
      <summary className="cursor-pointer text-sm font-medium">
        {label} ({formatNumber(list.total, i18n.language)})
      </summary>
      <ul className="mt-2 flex flex-col gap-0.5 text-sm text-fg-muted">
        {list.items.map((item) => (
          <li key={item}>{item}</li>
        ))}
        {list.total > list.items.length ? (
          <li>{t("maintainer.seedJobs.more", { count: list.total - list.items.length })}</li>
        ) : null}
      </ul>
    </details>
  );
}

function StepReport({ step }: { step: SeedStepReport }) {
  const { t, i18n } = useTranslation();
  const counts = COUNT_ORDER.filter((name) => step.counts[name] !== undefined);
  const heading = `${t(`maintainer.seed.kinds.${step.kind}`)} · ${t(`maintainer.seedJobs.actions.${step.action}`)}`;
  return (
    <section className="flex flex-col gap-2" aria-label={heading}>
      <h4 className="font-medium">{heading}</h4>
      {step.skipped ? (
        <p className="text-sm text-fg-muted">
          {t("maintainer.seedJobs.skipped", {
            reason: t(`maintainer.seedJobs.skip.${step.reason}`, { defaultValue: step.reason }),
          })}
        </p>
      ) : (
        <>
          <ul className="flex flex-wrap gap-2">
            {counts.map((name) => {
              const value = step.counts[name] ?? 0;
              const check = name === "orphaned" && value > 0;
              return (
                <li key={name}>
                  <StatTile
                    title={t(`maintainer.seedJobs.counts.${name}`)}
                    value={formatNumber(value, i18n.language)}
                    severity={check ? "warning" : undefined}
                    severityLabel={check ? t("maintainer.seedJobs.severity.check") : undefined}
                  />
                </li>
              );
            })}
          </ul>
          {Object.entries(step.lists).map(([name, list]) => (
            <ReportList key={name} label={t(`maintainer.seedJobs.lists.${name}`)} list={list} />
          ))}
          {step.reportFiles.length > 0 ? (
            <p className="text-sm text-fg-muted">
              {t("maintainer.seedJobs.reportFiles", {
                files: step.reportFiles.map((file) => `temp/seed-report/${file}`).join(", "),
              })}
            </p>
          ) : null}
        </>
      )}
    </section>
  );
}

function DiffView({ diff }: { diff: SeedFilesDiff }) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-col gap-2">
      <ReportList label={t("maintainer.seedJobs.diff.changed")} list={diff.changed} />
      <ReportList label={t("maintainer.seedJobs.diff.onlyInNew")} list={diff.onlyInNew} />
      <ReportList label={t("maintainer.seedJobs.diff.onlyInOld")} list={diff.onlyInOld} />
    </div>
  );
}

function RoundTrip({ result }: { result: SeedRoundTripResult }) {
  const { t, i18n } = useTranslation();
  const verdict = (identical: boolean, ok: string, bad: string) => (
    <p
      role="status"
      className={`flex items-center gap-2 rounded-md border border-l-4 bg-surface p-3 ${identical ? "border-l-sev-ok" : "border-l-sev-error"} border-border`}
    >
      <span aria-hidden="true">{identical ? "✓" : "✕"}</span>
      <span>{identical ? ok : bad}</span>
    </p>
  );
  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-fg-muted">
        {t("maintainer.seedJobs.roundTrip.files", {
          files: formatNumber(result.files, i18n.language),
        })}
      </p>
      {verdict(
        result.secondExportIdentical,
        t("maintainer.seedJobs.roundTrip.twiceOk"),
        t("maintainer.seedJobs.roundTrip.twiceBad"),
      )}
      {result.secondExportIdentical ? null : <DiffView diff={result.secondExport} />}
      {verdict(
        result.matchesDisk,
        t("maintainer.seedJobs.roundTrip.diskOk"),
        t("maintainer.seedJobs.roundTrip.diskBad"),
      )}
      {result.matchesDisk ? null : (
        <>
          <p className="text-sm text-fg-muted">{t("maintainer.seedJobs.roundTrip.diskHint")}</p>
          <DiffView diff={result.disk} />
        </>
      )}
    </div>
  );
}

/** The result of a finished job: export diff, per-kind import/update reports, rebase counts or the round-trip verdict. */
export function SeedJobResultView({ job }: { job: SeedJob }) {
  const { t, i18n } = useTranslation();
  const result = job.result;
  if (!result) return null;
  const number = (value: number) => formatNumber(value, i18n.language);

  return (
    <div className="flex flex-col gap-3">
      {result.export ? (
        <div className="flex flex-col gap-2">
          <p>
            {t("maintainer.seedJobs.export.summary", {
              files: number(result.export.files),
              created: number(result.export.diff.onlyInNew.total),
              changed: number(result.export.diff.changed.total),
              removed: number(result.export.diff.onlyInOld.total),
              path: result.export.seedRoot,
            })}
          </p>
          <DiffView diff={result.export.diff} />
          <p className="text-sm text-fg-muted">{t("maintainer.seedJobs.export.next")}</p>
        </div>
      ) : null}
      {result.rebase?.refused ? (
        <p role="alert" className="text-sm font-medium text-accent">
          {t("maintainer.seedJobs.rebase.refused", {
            reason: t(`maintainer.seedJobs.rebase.refusal.${result.rebase.refused}`),
          })}
        </p>
      ) : null}
      {result.rebase && !result.rebase.refused && result.rebase.before && result.rebase.after ? (
        <ul className="flex flex-wrap gap-2">
          {(["words", "morphology", "mappings"] as const).map((name) => (
            <li key={name}>
              <StatTile
                title={t(`maintainer.seedJobs.rebase.${name}`)}
                value={`${number(result.rebase?.before?.[name] ?? 0)} → ${number(result.rebase?.after?.[name] ?? 0)}`}
              />
            </li>
          ))}
          <li>
            <StatTile
              title={t("maintainer.seedJobs.rebase.realigned")}
              value={number(result.rebase.revisionRealigned)}
              sub={t("maintainer.seedJobs.rebase.missing", {
                count: result.rebase.revisionMissing,
              })}
            />
          </li>
        </ul>
      ) : null}
      {result.steps.map((step) => (
        <StepReport key={`${step.kind}-${step.action}`} step={step} />
      ))}
      {result.roundTrip ? <RoundTrip result={result.roundTrip} /> : null}
    </div>
  );
}
