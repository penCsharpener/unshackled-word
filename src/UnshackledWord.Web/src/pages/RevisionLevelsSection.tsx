import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import { describeApiError } from "@/lib/api";
import { useRevisionLevelDetails, useSaveRevisionLevel } from "@/lib/revision/queries";
import type { RevisionLevelDetail } from "@/lib/revision/types";
import { RevisionLevelDialog } from "./RevisionLevelDialog";

/** "Stufen" section of the maintenance page: rename the literalness presets, add one, choose the default. */
export function RevisionLevelsSection() {
  const { t, i18n } = useTranslation();
  const levels = useRevisionLevelDetails();
  const save = useSaveRevisionLevel();
  /** `null` = the add dialog, a preset = the edit dialog, `undefined` = closed. */
  const [editing, setEditing] = useState<RevisionLevelDetail | null>();
  const name = (l: RevisionLevelDetail) => (i18n.language.startsWith("de") ? l.nameDe : l.nameEn);

  function makeDefault(l: RevisionLevelDetail) {
    save.mutate({
      currentLevel: l.level,
      body: {
        level: l.level,
        code: l.code,
        nameDe: l.nameDe,
        nameEn: l.nameEn,
        descriptionDe: l.descriptionDe,
        descriptionEn: l.descriptionEn,
        isDefault: true,
      },
    });
  }

  return (
    <div className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold">{t("revisionLevels.title")}</h2>
      <p className="text-fg-muted">{t("revisionLevels.intro")}</p>
      {levels.isError ? <FormError message={t("revisionLevels.loadFailed")} /> : null}
      <ul className="flex flex-col divide-y divide-border rounded-md border border-border">
        {(levels.data ?? []).map((l) => (
          <li key={l.level} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2">
            <span className="font-medium">{name(l)}</span>
            <span className="text-sm text-fg-muted">
              {t("revisionLevels.meta", { level: l.level, code: l.code, count: l.variantCount })}
            </span>
            {l.isDefault ? (
              <span className="rounded-md bg-surface px-2 text-sm text-fg-muted">
                {t("revisionLevels.default")}
              </span>
            ) : null}
            {l.isBase ? null : (
              <span className="ml-auto flex gap-2">
                {l.isDefault ? null : (
                  <Button
                    size="sm"
                    variant="ghost"
                    disabled={save.isPending}
                    onClick={() => makeDefault(l)}
                  >
                    {t("revisionLevels.makeDefault")}
                  </Button>
                )}
                <Button size="sm" variant="secondary" onClick={() => setEditing(l)}>
                  {t("revisionLevels.edit")}
                </Button>
              </span>
            )}
          </li>
        ))}
      </ul>
      {save.isError ? (
        <FormError message={describeApiError(save.error, t("revisionLevels.failed"))} />
      ) : null}
      <div>
        <Button variant="secondary" onClick={() => setEditing(null)}>
          {t("revisionLevels.add")}
        </Button>
      </div>
      {editing !== undefined ? (
        <RevisionLevelDialog
          key={editing?.level ?? "new"}
          open
          initial={editing ?? undefined}
          onClose={() => setEditing(undefined)}
        />
      ) : null}
    </div>
  );
}
