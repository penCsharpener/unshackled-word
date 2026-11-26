import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";

export interface DiscardDialogProps {
  open: boolean;
  /** Ref of the unit, e.g. `Rut.1:1`. */
  unitRef: string;
  pending: boolean;
  error: string;
  onConfirm: () => void;
  onClose: () => void;
}

/** "KI-Vorschlag verwerfen?" (06a): the nt-form prefills stay. */
export function DiscardDialog({
  open,
  unitRef,
  pending,
  error,
  onConfirm,
  onClose,
}: DiscardDialogProps) {
  const { t } = useTranslation();
  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!pending}
      title={t("lxxReview.discard.title", { ref: unitRef })}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="secondary" disabled={pending} onClick={onClose}>
            {t("lxxReview.discard.cancel")}
          </Button>
          <Button variant="primary" disabled={pending} onClick={onConfirm}>
            {t("lxxReview.discard.confirm")}
          </Button>
        </div>
      }
    >
      <p>{t("lxxReview.discard.text")}</p>
      <FormError message={error} />
    </Dialog>
  );
}
