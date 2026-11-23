import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate } from "react-router";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { EmptyState } from "@/components/EmptyState";
import { FormError } from "@/components/FormError";
import { StatusBadge } from "@/components/StatusBadge";
import { TextField } from "@/components/TextField";
import { ApiError, describeApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth/AuthContext";
import { hasWriteRole } from "@/lib/auth/roles";
import { bookName, chapterPath } from "@/lib/bible/books";
import { useBibleStructure } from "@/lib/bible/queries";
import {
  useAcceptTskProposal,
  useRejectTskProposal,
  useTskReviewProposals,
} from "@/lib/bible/tskCuration";
import type { BibleStructureBook, TskProposedState, TskReviewProposal } from "@/lib/bible/types";
import { formatStamp } from "@/lib/format";
import { wordDiff } from "@/lib/revision/wordDiff";

/** Reasons are limited like the API (`RejectTskProposalRequestValidator`). */
const REASON_MAX_LENGTH = 500;

/** `BBCCCVVV` → book, chapter, verse. */
const splitHebRefId = (id: number) => ({
  bookId: Math.floor(id / 1_000_000),
  chapter: Math.floor(id / 1_000) % 1_000,
  verse: id % 1_000,
});

function verseLabel(
  books: BibleStructureBook[] | undefined,
  language: string,
  start: number,
  end?: number,
) {
  const { bookId, chapter, verse } = splitHebRefId(start);
  const book = books?.find((b) => b.id === bookId);
  const name = book ? bookName(book, language) : String(bookId);
  const verses = end && end !== start ? `${verse}–${splitHebRefId(end).verse}` : verse;
  return `${name} ${chapter},${verses}`;
}

const verseHref = (id: number) => {
  const { bookId, chapter, verse } = splitHebRefId(id);
  return `${chapterPath({ bookId, chapter })}#v${verse}`;
};

/** The comment of the proposed state against the current one: new words in `<ins>`, removed ones struck through. */
function CommentDiff({ before, after }: { before: string; after: string }) {
  const { t } = useTranslation();
  if (before === after) {
    return <p className="whitespace-pre-wrap">{after || t("tskReview.noComment")}</p>;
  }
  return (
    <p data-testid="tsk-comment-diff">
      {wordDiff(before, after).map((part, index) => {
        const key = `${index}-${part.kind}`;
        if (part.kind === "added") {
          return (
            <ins key={key} className="bg-mark-same no-underline">
              {part.text}{" "}
            </ins>
          );
        }
        if (part.kind === "removed") {
          return (
            <del key={key} className="text-fg-muted line-through">
              {part.text}{" "}
            </del>
          );
        }
        return <span key={key}>{part.text} </span>;
      })}
    </p>
  );
}

/** One side of a row: rating, comment (diffed on the proposed side), word quote; or "kein Querverweis" / delete. */
function StateColumn({
  heading,
  state,
  exists,
  diffAgainst,
}: {
  heading: string;
  state: Omit<TskProposedState, "origin"> | undefined;
  /** False: the link does not exist on this side (a new link's current side). */
  exists: boolean;
  /** Proposed side: the current comment the proposed one is compared to. */
  diffAgainst?: string;
}) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-col gap-1 rounded-md bg-surface-muted p-2">
      <p className="text-xs font-medium text-fg-muted">{heading}</p>
      {!exists ? (
        <p className="text-fg-muted">{t("tskReview.none")}</p>
      ) : state?.delete ? (
        <p className="font-medium">{t("tskReview.delete")}</p>
      ) : (
        <>
          <p>
            {t("tskReview.rating", {
              rating: t(`bible.tskCuration.choice.${state?.rating ?? "neutral"}`),
            })}
          </p>
          {diffAgainst !== undefined ? (
            <CommentDiff before={diffAgainst} after={state?.comment ?? ""} />
          ) : (
            <p className="whitespace-pre-wrap">{state?.comment || t("tskReview.noComment")}</p>
          )}
          <p className="text-fg-muted">
            {state?.word
              ? t("tskReview.word", { quote: state.word.quotedText })
              : t("tskReview.noWord")}
          </p>
        </>
      )}
    </div>
  );
}

/** Pending proposals grouped by source verse, in the API's order. */
function groupBySource(proposals: TskReviewProposal[]) {
  const groups: { source: number; items: TskReviewProposal[] }[] = [];
  for (const proposal of proposals) {
    const last = groups[groups.length - 1];
    if (last?.source === proposal.sourceHebRefId) last.items.push(proposal);
    else groups.push({ source: proposal.sourceHebRefId, items: [proposal] });
  }
  return groups;
}

/**
 * "TSK-Vorschläge" (`/edit/tsk`, Admin/Maintainer, roadmap 0024.6f): pending suggestions of users grouped by source
 * verse with current against proposed state (rating, comment as word diff, word quote); Annehmen (a stale one asks
 * first and then forces) and Ablehnen with an optional reason.
 */
export function TskReviewPage() {
  const { t, i18n } = useTranslation();
  const { status, user } = useAuth();
  const canReview = hasWriteRole(user);
  const review = useTskReviewProposals(canReview);
  const structure = useBibleStructure();
  const accept = useAcceptTskProposal();
  const reject = useRejectTskProposal();
  const [rejecting, setRejecting] = useState<number>();
  const [confirmStale, setConfirmStale] = useState<number>();
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
  const busy = accept.isPending || reject.isPending;

  function runAccept(id: number, force: boolean) {
    setDone(undefined);
    reject.reset();
    accept.mutate(
      { id, force },
      {
        onSuccess: () => {
          setDone(t("tskReview.done.accept"));
          setConfirmStale(undefined);
        },
        onError: () => {
          setConfirmStale(undefined);
          // a 400 means the link changed meanwhile (or the suggestion is decided): show the fresh state
          void review.refetch();
        },
      },
    );
  }

  function runReject() {
    if (rejecting === undefined) return;
    setDone(undefined);
    accept.reset();
    const trimmed = reason.trim();
    reject.mutate(
      { id: rejecting, reason: trimmed === "" ? undefined : trimmed },
      {
        onSuccess: () => {
          setDone(t("tskReview.done.reject"));
          setRejecting(undefined);
          setReason("");
        },
      },
    );
  }

  const proposals = review.data?.proposals ?? [];
  const acceptError = accept.isError
    ? accept.error instanceof ApiError && accept.error.status === 400
      ? t("tskReview.staleError")
      : describeApiError(accept.error, t("tskReview.actionError"))
    : "";

  return (
    <section className="mx-auto flex w-full max-w-4xl flex-col gap-4 p-4">
      <title>{t("tskReview.title")}</title>
      <h1 className="text-2xl font-semibold">{t("tskReview.title")}</h1>
      <p className="text-sm text-fg-muted">{t("tskReview.intro")}</p>
      {review.isError && <FormError message={t("tskReview.loadError")} />}
      <FormError message={acceptError} />
      {reject.isError && rejecting === undefined && (
        <FormError message={describeApiError(reject.error, t("tskReview.actionError"))} />
      )}
      {done && (
        <p role="status" className="text-sm text-fg-muted">
          {done}
        </p>
      )}

      {review.data &&
        (proposals.length === 0 ? (
          <EmptyState>{t("tskReview.empty")}</EmptyState>
        ) : (
          <>
            <p className="text-sm text-fg-muted">
              {t("tskReview.count", { count: proposals.length })}
            </p>
            {groupBySource(proposals).map((group) => (
              <section
                key={group.source}
                className="flex flex-col gap-2"
                data-source={group.source}
                aria-label={verseLabel(books, i18n.language, group.source)}
              >
                <h2 className="text-lg font-semibold">
                  <Link to={verseHref(group.source)} className="text-accent underline">
                    {verseLabel(books, i18n.language, group.source)}
                  </Link>
                </h2>
                <ul className="grid gap-3">
                  {group.items.map((item) => {
                    const target = verseLabel(
                      books,
                      i18n.language,
                      item.targetStartHebRefId,
                      item.targetEndHebRefId,
                    );
                    const exists = item.inTsk || item.current !== undefined;
                    return (
                      <li
                        key={item.id}
                        data-proposal-id={item.id}
                        className="grid gap-2 rounded-md border border-border bg-surface p-3 text-sm"
                      >
                        <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
                          <span className="text-fg-muted">{t("tskReview.target")}</span>
                          <Link
                            to={verseHref(item.targetStartHebRefId)}
                            className="font-medium text-accent underline"
                          >
                            {target}
                          </Link>
                          {!exists ? (
                            <StatusBadge tone="info">{t("tskReview.newLink")}</StatusBadge>
                          ) : (
                            !item.inTsk && (
                              <span className="text-xs text-fg-muted">{t("tskReview.added")}</span>
                            )
                          )}
                          <span className="text-fg-muted">
                            {t("tskReview.by", { author: item.authorDisplayName })}
                          </span>
                          <span className="text-xs text-fg-muted">
                            {formatStamp(item.createdAt, i18n.language)}
                          </span>
                        </div>
                        {item.stale && (
                          <p className="flex flex-wrap items-center gap-2" data-testid="tsk-stale">
                            <StatusBadge tone="warning">{t("tskReview.stale")}</StatusBadge>
                          </p>
                        )}
                        <div className="grid gap-2 md:grid-cols-2">
                          <StateColumn
                            heading={t("tskReview.current")}
                            state={item.current}
                            exists={exists}
                          />
                          <StateColumn
                            heading={t("tskReview.proposed")}
                            state={item.proposed}
                            exists
                            diffAgainst={
                              item.proposed.delete ? undefined : (item.current?.comment ?? "")
                            }
                          />
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <Button
                            variant="primary"
                            size="sm"
                            disabled={busy}
                            onClick={() =>
                              item.stale ? setConfirmStale(item.id) : runAccept(item.id, false)
                            }
                          >
                            {t("tskReview.accept")}
                          </Button>
                          <Button
                            variant="secondary"
                            size="sm"
                            disabled={busy}
                            onClick={() => {
                              reject.reset();
                              setReason("");
                              setRejecting(item.id);
                            }}
                          >
                            {t("tskReview.reject")}
                          </Button>
                        </div>
                      </li>
                    );
                  })}
                </ul>
              </section>
            ))}
          </>
        ))}

      <Dialog
        open={confirmStale !== undefined}
        onClose={() => setConfirmStale(undefined)}
        dismissible={!accept.isPending}
        title={t("tskReview.staleTitle")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirmStale(undefined)}>
              {t("tskReview.cancel")}
            </Button>
            <Button
              variant="primary"
              disabled={accept.isPending}
              onClick={() => confirmStale !== undefined && runAccept(confirmStale, true)}
            >
              {t("tskReview.acceptAnyway")}
            </Button>
          </>
        }
      >
        <p>{t("tskReview.staleText")}</p>
      </Dialog>

      <Dialog
        open={rejecting !== undefined}
        onClose={() => setRejecting(undefined)}
        dismissible={!reject.isPending}
        title={t("tskReview.rejectTitle")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setRejecting(undefined)}>
              {t("tskReview.cancel")}
            </Button>
            <Button variant="primary" disabled={reject.isPending} onClick={runReject}>
              {t("tskReview.reject")}
            </Button>
          </>
        }
      >
        <div className="grid gap-2">
          <TextField
            label={t("tskReview.reasonLabel")}
            value={reason}
            required={false}
            maxLength={REASON_MAX_LENGTH}
            onChange={(value) => setReason(value.slice(0, REASON_MAX_LENGTH))}
            multiline
            rows={3}
          />
          <p className="text-end text-xs text-fg-muted">
            {t("tskReview.counter", { length: reason.length, max: REASON_MAX_LENGTH })}
          </p>
          {reject.isError && (
            <FormError message={describeApiError(reject.error, t("tskReview.actionError"))} />
          )}
        </div>
      </Dialog>
    </section>
  );
}
