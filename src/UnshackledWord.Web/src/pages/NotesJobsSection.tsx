import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { Select } from "@/components/Select";
import { ApiError, describeApiError } from "@/lib/api";
import { notesFileKeys, useNotesFiles, useStartNotesJob } from "@/lib/maintainer/notesJobs";
import type { SeedJob } from "@/lib/maintainer/seedJobs";

/** Card "Notizen" on the imports page: export of all notes to temp/notes and import of one export file. */
export function NotesJobsSection({ job, blocked }: { job?: SeedJob; blocked: boolean }) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const files = useNotesFiles(true);
  const start = useStartNotesJob();
  const [chosen, setChosen] = useState("");

  // A finished export adds a file: reload the list so the new (newest) file shows up.
  const exported = job?.operation === "notesExport" && job.status === "succeeded" ? job.id : "";
  useEffect(() => {
    if (exported) void queryClient.invalidateQueries({ queryKey: notesFileKeys.files });
  }, [exported, queryClient]);

  const list = files.data ?? [];
  const selected = list.some((file) => file.name === chosen) ? chosen : (list[0]?.name ?? "");
  const kb = new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 });
  const options = list.map((file) => ({
    value: file.name,
    label: t("imports.notes.fileOption", {
      name: file.name,
      size: kb.format(file.sizeBytes / 1024),
      date: new Date(file.modifiedAt).toLocaleString(i18n.language),
    }),
  }));

  const error = start.error;
  const message = !error
    ? ""
    : error instanceof ApiError && error.status === 409
      ? t("maintainer.seedJobs.busy")
      : describeApiError(error, t("imports.notes.startFailed"));

  return (
    <section
      aria-labelledby="notes-card-title"
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4"
    >
      <h2 id="notes-card-title" className="text-lg font-semibold">
        {t("imports.notes.title")}
      </h2>
      <p>{t("imports.notes.what")}</p>
      <p className="text-sm text-fg-muted">{t("imports.notes.where")}</p>
      <p role="note" className="text-sm font-medium text-accent">
        {t("imports.notes.noDelete")}
      </p>
      <FormError message={message} />
      <div className="flex flex-wrap gap-2 pt-1">
        <Button
          variant="secondary"
          disabled={blocked}
          onClick={() => start.mutate({ operation: "export" })}
        >
          {t("imports.notes.export")}
        </Button>
      </div>
      {files.isError ? (
        <p role="alert" className="text-sm text-fg-muted">
          {t("imports.notes.filesFailed")}
        </p>
      ) : list.length === 0 ? (
        <p className="text-sm text-fg-muted">{t("imports.notes.noFiles")}</p>
      ) : (
        <div className="flex flex-wrap items-end gap-2">
          <Select
            label={t("imports.notes.file")}
            value={selected}
            onChange={setChosen}
            options={options}
          />
          <Button
            variant="primary"
            disabled={blocked || !selected}
            onClick={() => start.mutate({ operation: "import", fileName: selected })}
          >
            {t("imports.notes.import")}
          </Button>
        </div>
      )}
    </section>
  );
}
