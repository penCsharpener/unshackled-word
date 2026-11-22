import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { ChangeReviewList } from "@/components/ChangeReviewList";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { getVocabChanges, keepAllVocabChanges, postVocabChangeAction } from "@/lib/vocab/changeApi";
import { toChangeReviewItems } from "@/lib/vocab/changeReview";

/**
 * The "Geändert" notice (roadmap 0022.8bf) on `/learn` and at the start of a session. It opens the
 * review list in a dialog. It never blocks a session; it disappears once no flagged card is left.
 */
export function ChangedWordsNotice({ changedCount }: { changedCount: number }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const changes = useQuery({
    queryKey: ["vocab", "changes"],
    queryFn: ({ signal }) => getVocabChanges(signal),
    retry: false,
  });

  // Once the list is loaded it is the source of truth: keeping or resetting the last word hides the notice.
  const count = changes.data?.count ?? changedCount;
  const items = toChangeReviewItems(changes.data?.items ?? [], (key, options) =>
    options ? t(key, options) : t(key),
  );
  const keepable = items.some((item) => !item.removed);

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["vocab", "changes"] }),
      queryClient.invalidateQueries({ queryKey: ["vocab", "courses"] }),
    ]);
  const action = useMutation({
    mutationFn: ({ entryId, keep }: { entryId: number; keep: boolean }) =>
      postVocabChangeAction(entryId, keep ? "keep" : "reset"),
    onSuccess: refresh,
  });
  const keepAll = useMutation({
    mutationFn: keepAllVocabChanges,
    onSuccess: refresh,
  });
  const busy = action.isPending || keepAll.isPending;
  const failed = action.isError || keepAll.isError;

  return (
    <>
      {count > 0 ? (
        <div
          role="status"
          className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-sev-info bg-surface p-3"
        >
          <p className="text-sm">
            <span aria-hidden="true">ℹ </span>
            {t("learn.changes.notice", { count })}
          </p>
          <Button variant="secondary" size="sm" onClick={() => setOpen(true)}>
            {t("learn.changes.review")}
          </Button>
        </div>
      ) : null}
      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        title={t("learn.changes.title")}
        size="lg"
        dismissible={!busy}
        footer={
          <Button
            variant="primary"
            size="sm"
            disabled={busy || !keepable}
            onClick={() => keepAll.mutate()}
          >
            {t("learn.changes.keepAll")}
          </Button>
        }
      >
        <div className="grid gap-4">
          <p className="text-sm text-fg-muted">{t("learn.changes.intro")}</p>
          {failed ? <FormError message={t("learn.changes.failed")} /> : null}
          <ChangeReviewList
            items={items}
            disabled={busy}
            onKeep={(entryId) => action.mutate({ entryId, keep: true })}
            onReset={(entryId) => action.mutate({ entryId, keep: false })}
          />
        </div>
      </Dialog>
    </>
  );
}
