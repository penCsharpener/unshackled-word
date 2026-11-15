import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { describeApiError } from "@/lib/api";
import { useSaveRevisionLevel } from "@/lib/revision/queries";
import type { RevisionLevelDetail } from "@/lib/revision/types";

export interface RevisionLevelDialogProps {
  open: boolean;
  onClose: () => void;
  /** The preset to edit; without it the dialog adds a new one. */
  initial?: RevisionLevelDetail;
  /** Called with the saved level value after the preset was stored. */
  onSaved?: (level: number) => void;
}

/** Add or edit a literalness preset (level 1-1000, code, German and English name). */
export function RevisionLevelDialog({ open, onClose, initial, onSaved }: RevisionLevelDialogProps) {
  const { t } = useTranslation();
  const save = useSaveRevisionLevel();
  const [level, setLevel] = useState(initial ? String(initial.level) : "");
  const [code, setCode] = useState(initial?.code ?? "");
  const [nameDe, setNameDe] = useState(initial?.nameDe ?? "");
  const [nameEn, setNameEn] = useState(initial?.nameEn ?? "");
  const levelNumber = Number(level);
  const valid =
    Number.isInteger(levelNumber) &&
    levelNumber >= 1 &&
    levelNumber <= 1000 &&
    /^[a-z0-9][a-z0-9-]{0,29}$/.test(code) &&
    nameDe.trim() !== "" &&
    nameEn.trim() !== "";

  function submit() {
    if (!valid) return;
    save.mutate(
      {
        currentLevel: initial?.level,
        body: {
          level: levelNumber,
          code,
          nameDe: nameDe.trim(),
          nameEn: nameEn.trim(),
          descriptionDe: initial?.descriptionDe,
          descriptionEn: initial?.descriptionEn,
          isDefault: initial?.isDefault ?? false,
        },
      },
      {
        onSuccess: () => {
          onSaved?.(levelNumber);
          onClose();
        },
      },
    );
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t(initial ? "revisionLevels.editTitle" : "revisionLevels.newTitle")}
      dismissible={!save.isPending}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={save.isPending}>
            {t("revisionLevels.cancel")}
          </Button>
          <Button variant="primary" onClick={submit} disabled={!valid || save.isPending}>
            {t("revisionLevels.save")}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-3">
        <TextField
          label={t("revisionLevels.level")}
          type="number"
          min={1}
          max={1000}
          value={level}
          onChange={setLevel}
        />
        <TextField
          label={t("revisionLevels.code")}
          value={code}
          maxLength={30}
          onChange={setCode}
        />
        <TextField
          label={t("revisionLevels.nameDe")}
          value={nameDe}
          maxLength={100}
          onChange={setNameDe}
        />
        <TextField
          label={t("revisionLevels.nameEn")}
          value={nameEn}
          maxLength={100}
          onChange={setNameEn}
        />
        {save.isError ? (
          <FormError message={describeApiError(save.error, t("revisionLevels.failed"))} />
        ) : null}
      </div>
    </Dialog>
  );
}
