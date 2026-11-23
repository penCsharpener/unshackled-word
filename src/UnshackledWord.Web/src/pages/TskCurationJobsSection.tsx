import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { ApiError, describeApiError } from "@/lib/api";
import { invalidateTskViews } from "@/lib/bible/tskCuration";
import { useSeedStatus } from "@/lib/maintainer/seedJobs";
import { fetchTskImportPreview, useStartTskJob } from "@/lib/maintainer/tskSeedJobs";

/**
 * Card "TSK-Kuratierung" on the imports page (roadmap 0024.7f): imports `assets/seed/tsk/curations.csv` (newer wins, keys only in the file are added).
 * With "Exakt wie Datei" the preview's count of rows that would be deleted is confirmed first. The report shows in the page's job panel.
 */
export function TskCurationJobsSection({ blocked }: { blocked: boolean }) {
  const { t } = useTranslation();
  const start = useStartTskJob();
  const queryClient = useQueryClient();
  const job = useSeedStatus(true).data?.job;
  const [mirror, setMirror] = useState(false);
  const [checking, setChecking] = useState(false);
  const [previewError, setPreviewError] = useState<unknown>(null);
  const [confirmCount, setConfirmCount] = useState<number | null>(null);

  // The chapter lists, hover texts and word boxes are cached without expiry: a finished import must refetch them.
  const importDone = job?.operation === "tskImport" && job.status === "succeeded" ? job.id : null;
  useEffect(() => {
    if (importDone) void invalidateTskViews(queryClient);
  }, [importDone, queryClient]);

  async function run() {
    setPreviewError(null);
    if (!mirror) {
      start.mutate({ operation: "import", mirror: false });
      return;
    }
    setChecking(true);
    try {
      const preview = await fetchTskImportPreview();
      setConfirmCount(preview.wouldDelete);
    } catch (error) {
      setPreviewError(error);
    } finally {
      setChecking(false);
    }
  }

  function confirm() {
    setConfirmCount(null);
    start.mutate({ operation: "import", mirror: true });
  }

  const error = start.error ?? previewError;
  const message = !error
    ? ""
    : error instanceof ApiError && error.status === 409
      ? t("maintainer.seedJobs.busy")
      : describeApiError(error, t("imports.tsk.startFailed"));

  return (
    <section
      aria-labelledby="tsk-card-title"
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4"
    >
      <h2 id="tsk-card-title" className="text-lg font-semibold">
        {t("imports.tsk.title")}
      </h2>
      <p>{t("imports.tsk.what")}</p>
      <p className="text-sm text-fg-muted">{t("imports.tsk.rules")}</p>
      <label className="flex items-center gap-2">
        <input
          type="checkbox"
          checked={mirror}
          disabled={blocked || checking}
          onChange={(event) => setMirror(event.target.checked)}
        />
        {t("imports.tsk.mirror")}
      </label>
      {mirror ? (
        <p role="note" className="text-sm font-medium text-accent">
          {t("imports.tsk.mirrorHint")}
        </p>
      ) : null}
      <FormError message={message} />
      <div className="flex flex-wrap gap-2 pt-1">
        <Button variant="primary" disabled={blocked || checking} onClick={() => void run()}>
          {t("imports.tsk.import")}
        </Button>
      </div>

      <Dialog
        open={confirmCount !== null}
        onClose={() => setConfirmCount(null)}
        title={t("imports.tsk.confirmTitle")}
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setConfirmCount(null)}>
              {t("imports.tsk.cancel")}
            </Button>
            <Button variant="primary" onClick={confirm}>
              {t("imports.tsk.confirmRun")}
            </Button>
          </div>
        }
      >
        <p>{t("imports.tsk.wouldDelete", { count: confirmCount ?? 0 })}</p>
      </Dialog>
    </section>
  );
}
