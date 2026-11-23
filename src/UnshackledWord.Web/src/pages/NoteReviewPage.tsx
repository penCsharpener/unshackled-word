import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate } from "react-router";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { ApiError, describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { useBibleStructure } from "@/lib/bible/queries";
import { formatStamp } from "@/lib/format";
import { noteHref, type ReviewAction, type ReviewNote } from "@/lib/notes/api";
import { noteReference } from "@/lib/notes/display";
import { useReviewAction, useReviewNotes } from "@/lib/notes/queries";
import { sentenceDiff } from "@/lib/notes/textDiff";

/** Reasons are limited like the API (`NoteReviewActionRequest.Reason`). */
const REASON_MAX_LENGTH = 500;

/** Approved against current text: changed sentences in a `<mark>`, new words in `<ins>`, removed words struck through. */
function ReviewDiff({ approved, current }: { approved: string; current: string }) {
  const { t } = useTranslation();
  const segments = sentenceDiff(approved, current);
  if (segments.every((segment) => segment.kind === "same")) {
    return (
      <>
        <p className="text-xs text-fg-muted">{t("notes.review.unchanged")}</p>
        <p className="whitespace-pre-wrap">{current}</p>
      </>
    );
  }
  return (
    <p data-testid="review-diff">
      {segments.map((segment, index) => {
        const key = `${index}-${segment.kind}`;
        if (segment.kind === "same") return <span key={key}>{segment.text} </span>;
        return (
          <mark key={key} data-changed="" className="rounded-sm bg-mark-linked text-fg">
            {segment.parts.map((part, k) => {
              const partKey = `${k}-${part.kind}`;
              if (part.kind === "added") {
                return (
                  <ins key={partKey} className="bg-mark-same no-underline">
                    {part.text}{" "}
                  </ins>
                );
              }
              if (part.kind === "removed") {
                return (
                  <del key={partKey} className="text-fg-muted line-through">
                    {part.text}{" "}
                  </del>
                );
              }
              return <span key={partKey}>{part.text} </span>;
            })}
          </mark>
        );
      })}
    </p>
  );
}

/**
 * "Notizen prüfen" (`/edit/notes`, Admin/Maintainer): public notes waiting for approval with a diff of the approved
 * against the current text, approve / reject (reason) / hide (reason), and the hidden notes with "Wieder einblenden".
 */
export function NoteReviewPage() {
  const { t, i18n } = useTranslation();
  const { status, user } = useAuth();
  const canReview = hasWriteRole(user);
  const review = useReviewNotes(canReview);
  const structure = useBibleStructure();
  const act = useReviewAction();
  const [dialog, setDialog] = useState<{ id: string; action: "reject" | "hide" }>();
  const [reason, setReason] = useState("");
  const [done, setDone] = useState<string>();

  if (status === "loading") return null;
  if (status === "anonymous") return <Navigate to="/login" replace />;
  if (!canReview) {
    return (
      <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
        <p role="alert" className="text-fg-muted">
          {t("admin.forbidden")}
        </p>
      </section>
    );
  }

  const books = structure.data;
  const stamp = (value: string | undefined) => (value ? formatStamp(value, i18n.language) : "");

  function run(id: string, action: ReviewAction, why?: string, reviewedVersion?: string) {
    setDone(undefined);
    act.mutate(
      { id, action, reason: why?.trim() || undefined, reviewedVersion },
      {
        onSuccess: () => {
          setDone(t(`notes.review.done.${action}`));
          setDialog(undefined);
          setReason("");
        },
      },
    );
  }

  const header = (item: ReviewNote, date: string) => (
    <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
      <Link to={noteHref(item.note)} className="font-medium text-accent underline">
        {noteReference(item.note, books, i18n.language)}
      </Link>
      <span className="text-fg-muted">
        {t("notes.review.by", { author: item.note.authorDisplayName })}
      </span>
      <span className="text-xs text-fg-muted">{date}</span>
    </div>
  );

  const data = review.data;
  const pending = data?.pending ?? [];
  const hidden = data?.hidden ?? [];

  return (
    <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
      <title>{t("notes.review.title")}</title>
      <h1 className="text-2xl font-semibold">{t("notes.review.title")}</h1>
      <p className="text-sm text-fg-muted">{t("notes.review.intro")}</p>
      {review.isError && <FormError message={t("notes.review.loadError")} />}
      {act.isError && !dialog && (
        <FormError
          message={
            act.error instanceof ApiError && act.error.status === 409
              ? t("notes.review.changedSinceReview")
              : describeApiError(act.error, t("notes.review.actionError"))
          }
        />
      )}
      {done && (
        <p role="status" className="text-sm text-fg-muted">
          {done}
        </p>
      )}

      {data && (
        <>
          <h2 className="text-lg font-semibold">
            {t("notes.review.pending")} ({pending.length})
          </h2>
          {pending.length === 0 ? (
            <EmptyState>{t("notes.review.pendingEmpty")}</EmptyState>
          ) : (
            <ul className="grid gap-3">
              {pending.map((item) => (
                <li
                  key={item.note.id}
                  data-review-id={item.note.id}
                  className="grid gap-2 rounded-md border border-border bg-surface p-3 text-sm"
                >
                  {header(
                    item,
                    t("notes.review.changedAt", {
                      date: stamp(item.note.updatedAt ?? item.note.createdAt),
                    }),
                  )}
                  {item.approvedBodyMarkdown !== undefined ? (
                    <>
                      <p className="text-xs text-fg-muted">
                        {t("notes.review.approvedAt", { date: stamp(item.approvedAt) })}
                      </p>
                      <ReviewDiff
                        approved={item.approvedBodyMarkdown}
                        current={item.note.bodyMarkdown}
                      />
                    </>
                  ) : (
                    <>
                      <p className="text-xs text-fg-muted">{t("notes.review.firstRequest")}</p>
                      <p className="whitespace-pre-wrap">{item.note.bodyMarkdown}</p>
                    </>
                  )}
                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="primary"
                      size="sm"
                      disabled={act.isPending}
                      onClick={() =>
                        run(
                          item.note.id,
                          "approve",
                          undefined,
                          item.note.updatedAt ?? item.note.createdAt,
                        )
                      }
                    >
                      {t("notes.review.approve")}
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      disabled={act.isPending}
                      onClick={() => {
                        act.reset();
                        setReason("");
                        setDialog({ id: item.note.id, action: "reject" });
                      }}
                    >
                      {t("notes.review.reject")}
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={act.isPending}
                      onClick={() => {
                        act.reset();
                        setReason("");
                        setDialog({ id: item.note.id, action: "hide" });
                      }}
                    >
                      {t("notes.review.hide")}
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}

          <h2 className="text-lg font-semibold">
            {t("notes.review.hidden")} ({hidden.length})
          </h2>
          {hidden.length === 0 ? (
            <EmptyState>{t("notes.review.hiddenEmpty")}</EmptyState>
          ) : (
            <ul className="grid gap-3">
              {hidden.map((item) => (
                <li
                  key={item.note.id}
                  data-hidden-id={item.note.id}
                  className="grid gap-2 rounded-md border border-border bg-surface p-3 text-sm"
                >
                  {header(item, t("notes.review.hiddenAt", { date: stamp(item.hiddenAt) }))}
                  {item.reviewNote && (
                    <p className="text-fg-muted">
                      {t("notes.review.reason", { reason: item.reviewNote })}
                    </p>
                  )}
                  <p className="whitespace-pre-wrap">{item.note.bodyMarkdown}</p>
                  <div>
                    <Button
                      variant="secondary"
                      size="sm"
                      disabled={act.isPending}
                      onClick={() => run(item.note.id, "unhide")}
                    >
                      {t("notes.review.unhide")}
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </>
      )}

      <Dialog
        open={dialog !== undefined}
        onClose={() => setDialog(undefined)}
        dismissible={!act.isPending}
        title={t(dialog?.action === "hide" ? "notes.review.hideTitle" : "notes.review.rejectTitle")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setDialog(undefined)}>
              {t("notes.review.cancel")}
            </Button>
            <Button
              variant="primary"
              disabled={act.isPending}
              onClick={() => dialog && run(dialog.id, dialog.action, reason)}
            >
              {t(dialog?.action === "hide" ? "notes.review.hide" : "notes.review.reject")}
            </Button>
          </>
        }
      >
        <div className="grid gap-2">
          <TextField
            label={t("notes.review.reasonLabel")}
            value={reason}
            onChange={(value) => setReason(value.slice(0, REASON_MAX_LENGTH))}
            multiline
            rows={3}
          />
          {act.isError && (
            <FormError message={describeApiError(act.error, t("notes.review.actionError"))} />
          )}
        </div>
      </Dialog>
    </section>
  );
}
