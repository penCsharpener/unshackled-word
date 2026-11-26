import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { StatusBadge, type StatusTone } from "@/components/StatusBadge";
import { ApiError, describeApiError } from "@/lib/api";
import {
  invalidateReaderLxx,
  type LxxTagSource,
  type LxxTagStatus,
  sumTags,
  useLxxStatus,
  useStartLxxPrefill,
} from "@/lib/maintainer/lxx";
import type { SeedJobState } from "@/lib/maintainer/seedJobs";

const STATUSES: LxxTagStatus[] = ["proposed", "confirmed"];
const SOURCES: LxxTagSource[] = ["nt-form", "ai", "human"];
const TONES: Record<SeedJobState, StatusTone> = {
  running: "info",
  succeeded: "ok",
  failed: "error",
};

/**
 * "LXX" card of the maintenance page (roadmap 0028.6f): text and tag counts, the coverage share and "Vorbelegen" (background job on
 * the shared seed job runner, polled). A finished job makes the reader's cached LXX chapters and word cards stale.
 */
export function LxxSection() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const status = useLxxStatus(true);
  const start = useStartLxxPrefill();

  const data = status.data;
  const running = Boolean(data?.running) || start.isPending;
  const job = data?.job;

  const wasRunning = useRef(false);
  useEffect(() => {
    if (wasRunning.current && data && !data.running) {
      void invalidateReaderLxx(queryClient);
    }
    wasRunning.current = Boolean(data?.running);
  }, [data, queryClient]);

  const number = new Intl.NumberFormat(i18n.language);
  const percent = new Intl.NumberFormat(i18n.language, {
    style: "percent",
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  });
  const date = new Intl.DateTimeFormat(i18n.language, { dateStyle: "short", timeStyle: "short" });

  const tags = data?.tags ?? [];
  const tagged = sumTags(tags);
  const coverage = data && data.words > 0 ? tagged / data.words : 0;

  const startMessage = !start.error
    ? ""
    : start.error instanceof ApiError && start.error.status === 409
      ? t("maintainer.lxx.busy")
      : describeApiError(start.error, t("maintainer.lxx.startFailed"));
  const loadMessage = status.error
    ? describeApiError(status.error, t("maintainer.lxx.loadFailed"))
    : "";

  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t("maintainer.lxx.title")}</h2>
      <p className="text-fg-muted">{t("maintainer.lxx.intro")}</p>
      <FormError message={loadMessage} />

      {data ? (
        <>
          <dl className="flex flex-wrap gap-x-6 gap-y-1 text-sm">
            <div className="flex gap-2">
              <dt className="text-fg-muted">{t("maintainer.lxx.verses")}</dt>
              <dd className="tabular-nums">{number.format(data.verses)}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-fg-muted">{t("maintainer.lxx.words")}</dt>
              <dd className="tabular-nums">{number.format(data.words)}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-fg-muted">{t("maintainer.lxx.tags")}</dt>
              <dd className="tabular-nums">{number.format(tagged)}</dd>
            </div>
            <div className="flex gap-2">
              <dt className="text-fg-muted">{t("maintainer.lxx.coverage")}</dt>
              <dd className="tabular-nums">{percent.format(coverage)}</dd>
            </div>
          </dl>
          <div className="flex flex-wrap gap-x-8 gap-y-2 text-sm">
            <dl className="flex flex-col gap-1">
              <dt className="font-medium">{t("maintainer.lxx.byStatus")}</dt>
              {STATUSES.map((value) => (
                <dd key={value} className="flex gap-2">
                  <span className="text-fg-muted">{t(`maintainer.lxx.status.${value}`)}</span>
                  <span className="tabular-nums">
                    {number.format(sumTags(tags, { status: value }))}
                  </span>
                </dd>
              ))}
            </dl>
            <dl className="flex flex-col gap-1">
              <dt className="font-medium">{t("maintainer.lxx.bySource")}</dt>
              {SOURCES.map((value) => (
                <dd key={value} className="flex gap-2">
                  <span className="text-fg-muted">{t(`maintainer.lxx.source.${value}`)}</span>
                  <span className="tabular-nums">
                    {number.format(sumTags(tags, { source: value }))}
                  </span>
                </dd>
              ))}
            </dl>
          </div>
        </>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Button variant="primary" disabled={running || !data} onClick={() => start.mutate()}>
          {running ? t("maintainer.lxx.running") : t("maintainer.lxx.prefill")}
        </Button>
        {job ? (
          <span className="flex items-center gap-2 text-sm">
            <span className="text-fg-muted">{t("maintainer.lxx.lastJob")}</span>
            <StatusBadge tone={TONES[job.status]}>
              {t(`maintainer.lxx.jobStatus.${job.status}`)}
            </StatusBadge>
            {date.format(new Date(job.startedAt))}
          </span>
        ) : null}
      </div>
      <FormError message={startMessage} />
      {job?.error ? (
        <p role="alert" className="text-sm font-medium text-accent">
          {t("maintainer.lxx.jobFailed", { error: job.error })}
        </p>
      ) : null}
      {job && job.log.length > 0 ? (
        <ol className="max-h-32 overflow-y-auto font-mono text-sm text-fg-muted" aria-live="polite">
          {job.log.map((line, index) => (
            // biome-ignore lint/suspicious/noArrayIndexKey: log lines are append-only and may repeat
            <li key={index}>{line}</li>
          ))}
        </ol>
      ) : null}
      {running ? (
        <p role="status" className="text-sm text-fg-muted">
          {t("maintainer.lxx.progress")}
        </p>
      ) : null}
    </div>
  );
}
