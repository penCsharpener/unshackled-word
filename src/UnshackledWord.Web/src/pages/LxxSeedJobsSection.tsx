import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { ApiError, describeApiError } from "@/lib/api";
import { invalidateReaderLxx } from "@/lib/maintainer/lxx";
import { fetchLxxSeedImportPreview, useStartLxxSeedJob } from "@/lib/maintainer/lxxSeedJobs";
import { useSeedStatus } from "@/lib/maintainer/seedJobs";

/**
 * Card "LXX-Tags" on the imports page (roadmap 0028.8): imports `assets/seed/lxx/tags.csv` and `links.csv` (confirmed tags and Hebrew links, merge by default).
 * With "Exakt wie Datei" the preview's counts of confirmed rows that would be deleted are confirmed first; proposals are never touched. The report shows in the
 * page's job panel.
 */
export function LxxSeedJobsSection({ blocked }: { blocked: boolean }) {
  const { t } = useTranslation();
  const start = useStartLxxSeedJob();
  const queryClient = useQueryClient();
  const job = useSeedStatus(true).data?.job;
  const [mirror, setMirror] = useState(false);
  const [checking, setChecking] = useState(false);
  const [previewError, setPreviewError] = useState<unknown>(null);
  const [confirmCount, setConfirmCount] = useState<{ tags: number; links: number } | null>(null);

  // The reader's LXX chapter and word card are cached: a finished import must refetch them.
  const importDone =
    job?.operation === "lxxSeedImport" && job.status === "succeeded" ? job.id : null;
  useEffect(() => {
    if (importDone) void invalidateReaderLxx(queryClient);
  }, [importDone, queryClient]);

  async function run() {
    setPreviewError(null);
    if (!mirror) {
      start.mutate({ operation: "import", mirror: false });
      return;
    }
    setChecking(true);
    try {
      const preview = await fetchLxxSeedImportPreview();
      setConfirmCount({ tags: preview.wouldDeleteTags, links: preview.wouldDeleteLinks });
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
      : describeApiError(error, t("imports.lxx.startFailed"));

  return (
    <section
      aria-labelledby="lxx-seed-card-title"
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4"
    >
      <h2 id="lxx-seed-card-title" className="text-lg font-semibold">
        {t("imports.lxx.title")}
      </h2>
      <p>{t("imports.lxx.what")}</p>
      <p className="text-sm text-fg-muted">{t("imports.lxx.rules")}</p>
      <label className="flex items-center gap-2">
        <input
          type="checkbox"
          checked={mirror}
          disabled={blocked || checking}
          onChange={(event) => setMirror(event.target.checked)}
        />
        {t("imports.lxx.mirror")}
      </label>
      {mirror ? (
        <p role="note" className="text-sm font-medium text-accent">
          {t("imports.lxx.mirrorHint")}
        </p>
      ) : null}
      <FormError message={message} />
      <div className="flex flex-wrap gap-2 pt-1">
        <Button variant="primary" disabled={blocked || checking} onClick={() => void run()}>
          {t("imports.lxx.import")}
        </Button>
      </div>

      <Dialog
        open={confirmCount !== null}
        onClose={() => setConfirmCount(null)}
        title={t("imports.lxx.confirmTitle")}
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setConfirmCount(null)}>
              {t("imports.lxx.cancel")}
            </Button>
            <Button variant="primary" onClick={confirm}>
              {t("imports.lxx.confirmRun")}
            </Button>
          </div>
        }
      >
        <p>
          {t("imports.lxx.wouldDelete", {
            tags: confirmCount?.tags ?? 0,
            links: confirmCount?.links ?? 0,
          })}
        </p>
      </Dialog>
    </section>
  );
}
