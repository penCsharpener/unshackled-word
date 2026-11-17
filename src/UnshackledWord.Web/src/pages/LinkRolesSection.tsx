import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { StatusBadge } from "@/components/StatusBadge";
import { ApiError, describeApiError } from "@/lib/api";
import {
  LINK_ROLE_RULES,
  type LinkRoleCounts,
  type LinkRoleJob,
  type LinkRoleMode,
  type LinkRoleReport,
  useLinkRoleStatus,
  useStartLinkRoleJob,
} from "@/lib/maintainer/linkRoles";

const LANGUAGES = ["greek", "hebrew", "german"] as const;

const sample = (hebRefId: number) => ({
  book: Math.floor(hebRefId / 1_000_000),
  chapter: Math.floor(hebRefId / 1000) % 1000,
  verse: hebRefId % 1000,
});

function RuleTable({ report }: { report: LinkRoleReport }) {
  const { t, i18n } = useTranslation();
  const number = new Intl.NumberFormat(i18n.language);
  const cell = (counts: LinkRoleCounts, language: string, rule: string) =>
    counts[language]?.[rule] ?? 0;
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <caption className="sr-only">{t("maintainer.linkRoles.rules.title")}</caption>
        <thead className="text-fg-muted">
          <tr>
            <th scope="col" className="px-2 py-1 font-medium">
              {t("maintainer.linkRoles.rules.rule")}
            </th>
            {LANGUAGES.map((language) => (
              <th key={language} scope="col" className="px-2 py-1 font-medium">
                {t(`maintainer.linkRoles.languages.${language}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {LINK_ROLE_RULES.map((rule) => (
            <tr key={rule}>
              <th scope="row" className="px-2 py-1 font-normal">
                <span className="font-mono">{rule}</span>
                {" – "}
                {t(`maintainer.linkRoles.rules.labels.${rule}`)}
              </th>
              {LANGUAGES.map((language) => (
                <td key={language} className="px-2 py-1 tabular-nums">
                  {number.format(cell(report.changed, language, rule))}
                  <span className="text-fg-muted">
                    {" / "}
                    {number.format(cell(report.decided, language, rule))}
                  </span>
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      <p className="mt-1 text-xs text-fg-muted">{t("maintainer.linkRoles.rules.legend")}</p>
    </div>
  );
}

function UndecidedTable({ report }: { report: LinkRoleReport }) {
  const { t, i18n } = useTranslation();
  const number = new Intl.NumberFormat(i18n.language);
  const rows = Object.entries(report.undecidedByReason).flatMap(([language, reasons]) =>
    Object.entries(reasons).map(([reason, count]) => ({ language, reason, count })),
  );
  rows.sort((a, b) => b.count - a.count);
  return (
    <div className="flex flex-col gap-1">
      <h4 className="font-medium">
        {t("maintainer.linkRoles.undecided.title", { count: number.format(report.undecided) })}
      </h4>
      {rows.length === 0 ? (
        <p className="text-sm text-fg-muted">{t("maintainer.linkRoles.undecided.none")}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <caption className="sr-only">{t("maintainer.linkRoles.undecided.caption")}</caption>
            <thead className="text-fg-muted">
              <tr>
                <th scope="col" className="px-2 py-1 font-medium">
                  {t("maintainer.linkRoles.undecided.language")}
                </th>
                <th scope="col" className="px-2 py-1 font-medium">
                  {t("maintainer.linkRoles.undecided.reason")}
                </th>
                <th scope="col" className="px-2 py-1 font-medium">
                  {t("maintainer.linkRoles.undecided.count")}
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {rows.map((row) => (
                <tr key={`${row.language}|${row.reason}`}>
                  <td className="px-2 py-1">
                    {t(`maintainer.linkRoles.languages.${row.language}`, {
                      defaultValue: row.language,
                    })}
                  </td>
                  <td className="px-2 py-1">{row.reason}</td>
                  <td className="px-2 py-1 tabular-nums">{number.format(row.count)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function Samples({ report }: { report: LinkRoleReport }) {
  const { t } = useTranslation();
  const rules = LINK_ROLE_RULES.filter((rule) => (report.samples[rule]?.length ?? 0) > 0);
  if (rules.length === 0) return null;
  return (
    <div className="flex flex-col gap-1">
      <h4 className="font-medium">{t("maintainer.linkRoles.samples.title")}</h4>
      {rules.map((rule) => (
        <details key={rule} className="rounded-md border border-border px-3 py-2">
          <summary className="cursor-pointer text-sm font-medium">
            {t("maintainer.linkRoles.samples.rule", {
              rule,
              label: t(`maintainer.linkRoles.rules.labels.${rule}`),
              count: report.samples[rule]?.length ?? 0,
            })}
          </summary>
          <ul className="mt-2 flex flex-col gap-2 text-sm">
            {(report.samples[rule] ?? []).map((item) => {
              const ref = sample(item.hebRefId);
              return (
                <li key={`${item.hebRefId}-${item.language}`}>
                  <Link
                    to={`/bible/${ref.book}/${ref.chapter}#v${ref.verse}`}
                    className="text-accent underline"
                  >
                    {item.reference}
                  </Link>
                  <span className="text-fg-muted"> · {item.text}</span>
                  <ul className="pl-4 text-fg-muted">
                    {item.changes.map((change) => (
                      <li key={`${change.word}|${change.before}|${change.after}`}>
                        <span className="text-fg">{change.word}</span>: {change.before} →{" "}
                        {change.after}
                      </li>
                    ))}
                  </ul>
                </li>
              );
            })}
          </ul>
        </details>
      ))}
    </div>
  );
}

const tone = (status: LinkRoleJob["status"]) =>
  status === "succeeded" ? "ok" : status === "failed" ? "error" : "info";

/**
 * "Link-Rollen" section of the maintenance page (roadmap 0018.4f): the deterministic link-role backfill as a background job.
 * Dry run shows the report (counts per rule and language, undecided links, sample verses); apply writes one change set after
 * a confirmation; undo restores the latest change set.
 */
export function LinkRolesSection() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const status = useLinkRoleStatus(true);
  const start = useStartLinkRoleJob();
  const [confirm, setConfirm] = useState<"apply" | "undo" | null>(null);
  const job = status.data?.job;
  const applied = status.data?.applied;
  const appliedAt = status.data?.linkRolesAppliedAt;
  const running = job?.status === "running" || start.isPending;
  const report = job?.status === "succeeded" ? job.result?.report : undefined;

  // A finished job rewrote links or lemmas: refresh what the other sections show.
  const previousStatus = useRef(job?.status);
  useEffect(() => {
    if (previousStatus.current === "running" && job?.status !== "running") {
      void queryClient.invalidateQueries({ queryKey: ["revision"] });
    }
    previousStatus.current = job?.status;
  }, [job?.status, queryClient]);

  const run = (mode: LinkRoleMode) => {
    setConfirm(null);
    start.mutate(mode);
  };

  const date = new Intl.DateTimeFormat(i18n.language, { dateStyle: "short", timeStyle: "short" });
  const number = new Intl.NumberFormat(i18n.language);
  const startError = start.error;
  const startMessage = !startError
    ? ""
    : startError instanceof ApiError && startError.status === 409
      ? t("maintainer.linkRoles.busy", {
          detail: describeApiError(startError, t("maintainer.linkRoles.busyFallback")),
        })
      : describeApiError(startError, t("maintainer.linkRoles.startFailed"));

  const applyRows = report ? report.mappingRows + report.lemmaRows : 0;
  const applyHint = report ? "" : t("maintainer.linkRoles.hints.applyNeedsDryRun");

  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t("maintainer.linkRoles.title")}</h2>
      <p className="text-fg-muted">{t("maintainer.linkRoles.intro")}</p>
      <p role="status" className="text-sm text-fg-muted">
        {appliedAt
          ? t("maintainer.linkRoles.appliedAt", { date: date.format(new Date(appliedAt)) })
          : t("maintainer.linkRoles.notApplied")}
        {applied
          ? ` ${t("maintainer.linkRoles.changeSet", {
              id: applied.id,
              mappings: number.format(applied.mappingRows),
              lemmas: number.format(applied.lemmaRows),
            })}`
          : ""}
      </p>
      <div className="flex flex-wrap items-center gap-3">
        <Button
          variant="primary"
          disabled={running}
          title={t("maintainer.linkRoles.hints.dryRun")}
          onClick={() => run("dryRun")}
        >
          {t("maintainer.linkRoles.modes.dryRun")}
        </Button>
        <Button
          variant="secondary"
          disabled={running || !report}
          title={applyHint || t("maintainer.linkRoles.hints.apply")}
          onClick={() => setConfirm("apply")}
        >
          {t("maintainer.linkRoles.modes.apply")}
        </Button>
        <Button
          variant="ghost"
          disabled={running || !applied}
          title={
            applied
              ? t("maintainer.linkRoles.hints.undo")
              : t("maintainer.linkRoles.hints.undoNone")
          }
          onClick={() => setConfirm("undo")}
        >
          {t("maintainer.linkRoles.modes.undo")}
        </Button>
      </div>
      <FormError message={startMessage} />

      {job ? (
        <div className="flex flex-col gap-3 rounded-md border border-border p-3">
          <h3 className="flex flex-wrap items-center gap-2 font-medium">
            {t(`maintainer.linkRoles.modes.${job.mode}`)}
            <StatusBadge tone={tone(job.status)}>
              {t(`maintainer.linkRoles.status.${job.status}`)}
            </StatusBadge>
          </h3>
          {job.error ? (
            <p role="alert" className="text-sm font-medium text-accent">
              {t("maintainer.linkRoles.failed", { error: job.error })}
            </p>
          ) : null}
          {job.status === "succeeded" && job.mode !== "dryRun" && job.result ? (
            <p role="status" className="text-sm">
              {job.mode === "apply"
                ? t("maintainer.linkRoles.result.apply", {
                    id: job.result.changeSetId,
                    mappings: number.format(job.result.mappingRows),
                    lemmas: number.format(job.result.lemmaRows),
                  })
                : t("maintainer.linkRoles.result.undo", {
                    id: job.result.undoChangeSetId,
                    mappings: number.format(job.result.mappingRows),
                    lemmas: number.format(job.result.lemmaRows),
                  })}
            </p>
          ) : null}
          {report ? (
            <>
              <p className="text-sm text-fg-muted">
                {t("maintainer.linkRoles.report.summary", {
                  verses: number.format(report.verses),
                  links: number.format(report.links),
                  mappings: number.format(report.mappingRows),
                  lemmas: number.format(report.lemmaRows),
                })}
              </p>
              <RuleTable report={report} />
              <UndecidedTable report={report} />
              <Samples report={report} />
            </>
          ) : null}
          <details open={job.status !== "succeeded"}>
            <summary className="cursor-pointer text-sm font-medium">
              {t("maintainer.linkRoles.log")}
            </summary>
            <ol
              className="mt-2 max-h-64 overflow-y-auto font-mono text-sm text-fg-muted"
              aria-live="polite"
            >
              {job.log.map((line, index) => (
                // biome-ignore lint/suspicious/noArrayIndexKey: log lines are append-only and may repeat
                <li key={index}>{line}</li>
              ))}
            </ol>
          </details>
        </div>
      ) : (
        <p className="text-sm text-fg-muted">{t("maintainer.linkRoles.noJob")}</p>
      )}

      <Dialog
        open={confirm === "apply"}
        onClose={() => setConfirm(null)}
        title={t("maintainer.linkRoles.applyDialog.title")}
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setConfirm(null)}>
              {t("maintainer.linkRoles.dialog.cancel")}
            </Button>
            <Button variant="primary" onClick={() => run("apply")}>
              {t("maintainer.linkRoles.applyDialog.start")}
            </Button>
          </div>
        }
      >
        <p>
          {t("maintainer.linkRoles.applyDialog.body", {
            rows: number.format(applyRows),
            mappings: number.format(report?.mappingRows ?? 0),
            lemmas: number.format(report?.lemmaRows ?? 0),
          })}
        </p>
      </Dialog>
      <Dialog
        open={confirm === "undo"}
        onClose={() => setConfirm(null)}
        title={t("maintainer.linkRoles.undoDialog.title")}
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setConfirm(null)}>
              {t("maintainer.linkRoles.dialog.cancel")}
            </Button>
            <Button variant="primary" onClick={() => run("undo")}>
              {t("maintainer.linkRoles.undoDialog.start")}
            </Button>
          </div>
        }
      >
        <p>
          {t("maintainer.linkRoles.undoDialog.body", {
            id: applied?.id,
            rows: number.format((applied?.mappingRows ?? 0) + (applied?.lemmaRows ?? 0)),
          })}
        </p>
      </Dialog>
    </div>
  );
}
