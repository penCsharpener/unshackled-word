import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";

export interface SafeConfirmCounts {
  /** Words that will be sent (AI `high`, unconfirmed, unchanged). */
  words: number;
  units: number;
  /** AI-`low` words on screen that stay open. */
  low: number;
  /** Units left out because of unsaved edits or a conflict. */
  dirty: number;
}

export interface SafeConfirmDialogProps {
  open: boolean;
  counts: SafeConfirmCounts;
  pending: boolean;
  error: string;
  onConfirm: () => void;
  onClose: () => void;
}

/** Confirmation before "Kapitel bestätigen (sichere)" (06a). */
export function SafeConfirmDialog({
  open,
  counts,
  pending,
  error,
  onConfirm,
  onClose,
}: SafeConfirmDialogProps) {
  const { t } = useTranslation();
  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!pending}
      title={t("lxxReview.safe.title")}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="secondary" disabled={pending} onClick={onClose}>
            {t("lxxReview.safe.cancel")}
          </Button>
          <Button variant="primary" disabled={pending || counts.words === 0} onClick={onConfirm}>
            {t("lxxReview.safe.confirm")}
          </Button>
        </div>
      }
    >
      <p>{t("lxxReview.safe.text", { words: counts.words, units: counts.units })}</p>
      <p className="text-fg-muted">{t("lxxReview.safe.low", { count: counts.low })}</p>
      {counts.dirty > 0 ? (
        <p className="text-fg-muted">{t("lxxReview.safe.dirty", { count: counts.dirty })}</p>
      ) : null}
      <FormError message={error} />
    </Dialog>
  );
}
