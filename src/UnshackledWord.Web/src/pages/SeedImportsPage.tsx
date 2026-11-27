import { Trans, useTranslation } from "react-i18next";
import { Link, Navigate } from "react-router";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { ApiError, describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole, isAdmin } from "@/lib/auth/roles";
import type { SeedKind } from "@/lib/maintainer/api";
import {
  useSeedStatus,
  useStartSeedJob,
  useVocabSeedImportPreview,
} from "@/lib/maintainer/seedJobs";
import { LxxSeedJobsSection } from "./LxxSeedJobsSection";
import { NotesJobsSection } from "./NotesJobsSection";
import { SeedJobResultView } from "./SeedJobResultView";
import { TskCurationJobsSection } from "./TskCurationJobsSection";

type ImportKind = Exclude<SeedKind, "all">;

interface ImportSpec {
  kind: ImportKind;
  /** Tables the import writes to. */
  tables: string[];
  /** Whether a separate, gentler update exists next to the import. */
  update: boolean;
  /** Whether the import can overwrite maintainer edits (shown as a warning). */
  overwrites: boolean;
}

const IMPORTS: ImportSpec[] = [
  {
    kind: "vocab",
    tables: [
      "VocabEntries",
      "VocabStrongs",
      "VocabMeanings",
      "VocabEntryChanges",
      "VocabRoots",
      "VocabStrongsRoots",
    ],
    update: false,
    overwrites: true,
  },
  {
    kind: "revision",
    tables: ["RevisionVerses", "RevisionWords", "RevisionMapping", "SeedVerseState", "SeedImports"],
    update: true,
    overwrites: false,
  },
  {
    kind: "mappings",
    tables: ["Elb1871GreekMapping", "Elb1871HebrewMapping", "SeedVerseState", "SeedImports"],
    update: true,
    overwrites: false,
  },
  {
    kind: "morphology",
    tables: ["Elb1871Morphology", "SeedImports"],
    update: false,
    overwrites: false,
  },
];

/** Roadmap 0030.7f: what the root files would do, shown on the vocab card before the maintainer presses the import button. */
function VocabRootsPreview({ refreshKey }: { refreshKey: number | string | null }) {
  const { t } = useTranslation();
  const preview = useVocabSeedImportPreview(true, refreshKey);
  if (preview.isError) {
    return <p className="text-sm text-fg-muted">{t("imports.rootsPreviewFailed")}</p>;
  }
  const data = preview.data;
  if (!data) return null;
  return (
    <div role="status" className="flex flex-col gap-1 text-sm">
      <p className="font-medium">
        {t("imports.rootsPreview", {
          added: data.rootsNew,
          changed: data.rootsChanged,
          kept: data.manualKept,
        })}
      </p>
      <p className="text-fg-muted">
        {t("imports.rootsPreviewFiles", { roots: data.fileRoots, words: data.fileWordRoots })}
        {data.skipped > 0 ? ` ${t("imports.rootsPreviewSkipped", { count: data.skipped })}` : ""}
      </p>
    </div>
  );
}

/** "Importe": every seed import as its own explained button; nothing is imported automatically. */
export function SeedImportsPage() {
  const { t } = useTranslation();
  const { status, user } = useAuth();
  const seedStatus = useSeedStatus(hasWriteRole(user));
  const start = useStartSeedJob();

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  if (!hasWriteRole(user)) {
    return (
      <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
        <p role="alert" className="text-fg-muted">
          {t("admin.forbidden")}
        </p>
      </section>
    );
  }

  const maintenanceLink = <Link to="/edit/maintainer" className="text-accent underline" />;
  const job = seedStatus.data?.job;
  const running = job?.status === "running" || start.isPending;
  const assetsProblem = seedStatus.data?.assetsProblem;
  const blocked = running || Boolean(assetsProblem);
  const error = start.error;
  const message = !error
    ? ""
    : error instanceof ApiError && error.status === 409
      ? t("maintainer.seedJobs.busy")
      : describeApiError(error, t("maintainer.seedJobs.startFailed"));

  return (
    <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold">{t("imports.title")}</h1>
      <p className="text-fg-muted">
        <Trans i18nKey="imports.intro" components={{ maint: maintenanceLink }} />
      </p>
      {isAdmin(user) ? (
        <p className="text-sm">
          <Link to="/edit/setup" className="text-accent underline">
            {t("setup.link")}
          </Link>
        </p>
      ) : null}
      {assetsProblem ? (
        <p role="status" className="text-sm text-fg-muted">
          {t("maintainer.seedJobs.assetsProblem", { problem: assetsProblem })}
        </p>
      ) : null}
      <FormError message={message} />

      <ul className="flex flex-col gap-4">
        {IMPORTS.map((spec) => (
          <li
            key={spec.kind}
            className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4"
          >
            <h2 className="text-lg font-semibold">{t(`imports.kinds.${spec.kind}.title`)}</h2>
            <p>{t(`imports.kinds.${spec.kind}.what`)}</p>
            <p className="text-sm text-fg-muted">
              <span className="font-medium">{t("imports.tables")}</span>{" "}
              {spec.tables.map((table, index) => (
                <span key={table}>
                  {index > 0 ? ", " : ""}
                  <code>{table}</code>
                </span>
              ))}
            </p>
            <p className="text-sm text-fg-muted">
              <span className="font-medium">{t("imports.importLabel")}</span>{" "}
              {t(`imports.kinds.${spec.kind}.import`)}
            </p>
            <p className="text-sm text-fg-muted">
              <span className="font-medium">{t("imports.updateLabel")}</span>{" "}
              {t(`imports.kinds.${spec.kind}.update`)}
            </p>
            {spec.overwrites ? (
              <p role="note" className="text-sm font-medium text-accent">
                <Trans
                  i18nKey={`imports.kinds.${spec.kind}.warning`}
                  components={{ maint: maintenanceLink }}
                />
              </p>
            ) : null}
            {spec.kind === "vocab" ? (
              <VocabRootsPreview refreshKey={job?.status === "succeeded" ? job.id : null} />
            ) : null}
            <div className="flex flex-wrap gap-2 pt-1">
              <Button
                variant="primary"
                disabled={blocked}
                onClick={() => start.mutate({ operation: "import", kind: spec.kind })}
              >
                {t("imports.run", { kind: t(`maintainer.seed.kinds.${spec.kind}`) })}
              </Button>
              {spec.update ? (
                <Button
                  variant="secondary"
                  disabled={blocked}
                  onClick={() => start.mutate({ operation: "update", kind: spec.kind })}
                >
                  {t("imports.runUpdate", { kind: t(`maintainer.seed.kinds.${spec.kind}`) })}
                </Button>
              ) : null}
            </div>
          </li>
        ))}
        <li>
          <NotesJobsSection job={job} blocked={blocked} />
        </li>
        <li>
          <TskCurationJobsSection blocked={blocked} />
        </li>
        <li>
          <LxxSeedJobsSection blocked={blocked} />
        </li>
      </ul>

      {job ? (
        <div className="flex flex-col gap-3 rounded-md border border-border p-3">
          <h2 className="font-medium">
            {t(`maintainer.seedJobs.operations.${job.operation}`)}
            {` · ${t(`maintainer.seed.kinds.${job.kind}`)}`}
            {" – "}
            <span role="status">{t(`maintainer.seedJobs.status.${job.status}`)}</span>
          </h2>
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
      ) : null}
    </section>
  );
}
