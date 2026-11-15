import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { ApiError, describeApiError } from "@/lib/api";
import type { SeedKind } from "@/lib/maintainer/api";
import {
  type SeedImportRow,
  type SeedOperation,
  useSeedStatus,
  useStartSeedJob,
} from "@/lib/maintainer/seedJobs";
import { SeedJobResultView } from "./SeedJobResultView";

const KINDS: SeedKind[] = ["revision", "mappings", "morphology", "all"];
const OPERATIONS: Exclude<SeedOperation, "rebase">[] = ["export", "import", "update", "roundTrip"];

function ImportsTable({ rows }: { rows: SeedImportRow[] }) {
  const { t, i18n } = useTranslation();
  if (rows.length === 0)
    return <p className="text-sm text-fg-muted">{t("maintainer.seedJobs.imports.none")}</p>;
  const date = new Intl.DateTimeFormat(i18n.language, { dateStyle: "short", timeStyle: "short" });
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <caption className="sr-only">{t("maintainer.seedJobs.imports.title")}</caption>
        <thead className="text-fg-muted">
          <tr>
            {["kind", "version", "importedAt", "rows", "orphaned", "updated", "kept"].map(
              (column) => (
                <th key={column} scope="col" className="px-2 py-1 font-medium">
                  {t(`maintainer.seedJobs.imports.${column}`)}
                </th>
              ),
            )}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {rows.map((row) => (
            <tr key={row.id}>
              <td className="px-2 py-1">
                {t(`maintainer.seed.kinds.${row.kind}`, { defaultValue: row.kind })}
              </td>
              <td className="px-2 py-1 font-mono">{row.seedVersion.slice(0, 12)}</td>
              <td className="px-2 py-1">{date.format(new Date(row.importedAt))}</td>
              <td className="px-2 py-1 tabular-nums">{row.rows}</td>
              <td className="px-2 py-1 tabular-nums">{row.orphaned}</td>
              <td className="px-2 py-1 tabular-nums">{row.versesUpdated ?? "–"}</td>
              <td className="px-2 py-1 tabular-nums">{row.versesKeptLocal ?? "–"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/**
 * "Seed-Rundlauf" section of the maintenance page: the seed steps the SeedDb command line offers (export to assets/seed, import,
 * update, rebase, round-trip check) as background jobs with a live log, the structured result and the latest `SeedImports` rows.
 */
export function SeedRoundTripSection() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [kind, setKind] = useState<SeedKind>("revision");
  const [rebaseOpen, setRebaseOpen] = useState(false);
  const [backupConfirmed, setBackupConfirmed] = useState(false);
  const status = useSeedStatus(true);
  const start = useStartSeedJob();
  const job = status.data?.job;
  const running = job?.status === "running" || start.isPending;
  const assetsProblem = status.data?.assetsProblem;
  const blocked = running || Boolean(assetsProblem);

  // A finished job may have changed the revision: refresh what the revision sections show.
  const previousStatus = useRef(job?.status);
  useEffect(() => {
    if (previousStatus.current === "running" && job?.status !== "running") {
      void queryClient.invalidateQueries({ queryKey: ["revision"] });
    }
    previousStatus.current = job?.status;
  }, [job?.status, queryClient]);

  function run(operation: SeedOperation, confirmBackup = false) {
    start.mutate({ operation, kind: operation === "rebase" ? "all" : kind, confirmBackup });
  }

  function closeRebase() {
    setRebaseOpen(false);
    setBackupConfirmed(false);
  }

  const startError = start.error;
  const startMessage = !startError
    ? ""
    : startError instanceof ApiError && startError.status === 409
      ? t("maintainer.seedJobs.busy")
      : describeApiError(startError, t("maintainer.seedJobs.startFailed"));

  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t("maintainer.seedJobs.title")}</h2>
      <p className="text-fg-muted">{t("maintainer.seedJobs.intro")}</p>
      {assetsProblem ? (
        <p role="status" className="text-sm text-fg-muted">
          {t("maintainer.seedJobs.assetsProblem", { problem: assetsProblem })}
        </p>
      ) : null}
      <div className="flex flex-wrap items-end gap-3">
        <Select
          label={t("maintainer.seedJobs.kind")}
          value={kind}
          onChange={(value) => setKind(value as SeedKind)}
          disabled={running}
          options={KINDS.map((value) => ({ value, label: t(`maintainer.seed.kinds.${value}`) }))}
        />
        {OPERATIONS.map((operation) => (
          <Button
            key={operation}
            variant={operation === "roundTrip" ? "secondary" : "primary"}
            disabled={blocked}
            title={t(`maintainer.seedJobs.hints.${operation}`)}
            onClick={() => run(operation)}
          >
            {t(`maintainer.seedJobs.operations.${operation}`)}
          </Button>
        ))}
        <Button
          variant="ghost"
          disabled={blocked}
          title={t("maintainer.seedJobs.hints.rebase")}
          onClick={() => setRebaseOpen(true)}
        >
          {t("maintainer.seedJobs.operations.rebase")}
        </Button>
      </div>
      <FormError message={startMessage} />

      {job ? (
        <div className="flex flex-col gap-3 rounded-md border border-border p-3">
          <h3 className="font-medium">
            {t(`maintainer.seedJobs.operations.${job.operation}`)}
            {job.operation === "rebase" ? "" : ` · ${t(`maintainer.seed.kinds.${job.kind}`)}`}
            {" – "}
            <span role="status">{t(`maintainer.seedJobs.status.${job.status}`)}</span>
          </h3>
          {job.error ? (
            <p role="alert" className="text-sm font-medium text-accent">
              {t("maintainer.seedJobs.failed", { error: job.error })}
            </p>
          ) : null}
          {job.status === "succeeded" ? <SeedJobResultView job={job} /> : null}
          <details open={job.status !== "succeeded"}>
            <summary className="cursor-pointer text-sm font-medium">
              {t("maintainer.seedJobs.log")}
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
        <p className="text-sm text-fg-muted">{t("maintainer.seedJobs.noJob")}</p>
      )}

      <h3 className="font-medium">{t("maintainer.seedJobs.imports.title")}</h3>
      <ImportsTable rows={status.data?.imports ?? []} />

      <Dialog
        open={rebaseOpen}
        onClose={closeRebase}
        title={t("maintainer.seedJobs.rebaseDialog.title")}
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={closeRebase}>
              {t("maintainer.seedJobs.rebaseDialog.cancel")}
            </Button>
            <Button
              variant="primary"
              disabled={!backupConfirmed}
              onClick={() => {
                run("rebase", true);
                closeRebase();
              }}
            >
              {t("maintainer.seedJobs.rebaseDialog.start")}
            </Button>
          </div>
        }
      >
        <div className="flex flex-col gap-3">
          <p>{t("maintainer.seedJobs.rebaseDialog.body")}</p>
          <ul className="list-disc pl-5 text-sm text-fg-muted">
            <li>{t("maintainer.seedJobs.rebaseDialog.stepExport")}</li>
            <li>{t("maintainer.seedJobs.rebaseDialog.stepClear")}</li>
            <li>{t("maintainer.seedJobs.rebaseDialog.stepImport")}</li>
            <li>{t("maintainer.seedJobs.rebaseDialog.stepRevision")}</li>
          </ul>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={backupConfirmed}
              onChange={(event) => setBackupConfirmed(event.target.checked)}
            />
            {t("maintainer.seedJobs.rebaseDialog.confirm")}
          </label>
        </div>
      </Dialog>
    </div>
  );
}
