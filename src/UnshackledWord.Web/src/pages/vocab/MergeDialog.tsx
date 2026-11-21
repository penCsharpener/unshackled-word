import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { describeApiError } from "@/lib/api";
import { vocabLang } from "@/lib/vocab/api";
import { mergeVocabEntry, type VocabEditorListItem, vocabEditorKeys } from "@/lib/vocab/editorApi";
import { useEntrySearch } from "./useEntrySearch";

export interface MergeDialogProps {
  /** The entry that is merged away and deleted. */
  entryId: number;
  lemma: string;
  keyCount: number;
  learners: number;
  /** Course code, so the target search stays in the track. */
  course?: string;
  onClose: () => void;
}

/** "μέσος zusammenführen": this entry is merged into the chosen one, which stays (roadmap 0022.4f). */
export function MergeDialog({
  entryId,
  lemma,
  keyCount,
  learners,
  course,
  onClose,
}: MergeDialogProps) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [text, setText] = useState("");
  const [target, setTarget] = useState<VocabEditorListItem | null>(null);
  const search = useEntrySearch(course, text, entryId, true);

  const merge = useMutation({
    mutationFn: () => {
      if (!target) throw new Error("No merge target chosen.");
      return mergeVocabEntry(entryId, target.id);
    },
    onSuccess: () => {
      if (target) navigate(`/edit/vocab/${target.id}`);
      void queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all });
      onClose();
    },
  });

  const stats = (keys: number, learnerCount: number) =>
    t("vocab.editor.merge.stats", {
      keys,
      learners: learnerCount.toLocaleString(i18n.language),
    });

  return (
    <Dialog
      open
      onClose={onClose}
      title={t("vocab.editor.merge.title", { lemma })}
      dismissible={!merge.isPending}
      size="md"
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="ghost" size="sm" onClick={onClose} disabled={merge.isPending}>
            {t("vocab.editor.cancel")}
          </Button>
          <Button
            variant="primary"
            size="sm"
            disabled={target === null || merge.isPending}
            onClick={() => merge.mutate()}
          >
            {t("vocab.editor.merge.submit")}
          </Button>
        </div>
      }
    >
      <div className="grid gap-4">
        <TextField
          required={false}
          size="sm"
          label={t("vocab.editor.merge.target")}
          placeholder={t("vocab.editor.merge.targetPlaceholder")}
          value={text}
          onChange={setText}
          lang="grc"
          className="font-original"
        />
        {search.items.length > 0 ? (
          <ul className="grid gap-1" aria-label={t("vocab.editor.merge.target")}>
            {search.items.map((item) => (
              <li key={item.id}>
                <Button
                  variant="secondary"
                  size="sm"
                  className="w-full justify-between"
                  aria-pressed={target?.id === item.id}
                  onClick={() => setTarget(item)}
                >
                  <span lang={vocabLang(item.track)} className="font-original">
                    {item.lemma}
                  </span>
                  <span className="text-fg-muted">{item.strongsKeys.join(", ")}</span>
                </Button>
              </li>
            ))}
          </ul>
        ) : search.term ? (
          <p className="text-sm text-fg-muted">{t("vocab.editor.noMatches")}</p>
        ) : null}

        <dl className="grid grid-cols-2 gap-2 text-sm">
          <div>
            <dt className="text-fg-muted">{t("vocab.editor.merge.source", { lemma })}</dt>
            <dd className="tabular-nums">{stats(keyCount, learners)}</dd>
          </div>
          <div>
            <dt className="text-fg-muted">
              {target
                ? t("vocab.editor.merge.kept", { lemma: target.lemma })
                : t("vocab.editor.merge.keptNone")}
            </dt>
            <dd className="tabular-nums">
              {target ? stats(target.strongsKeys.length, target.learners) : "–"}
            </dd>
          </div>
        </dl>
        <p className="text-sm text-fg-muted">{t("vocab.editor.merge.note")}</p>
        <FormError
          message={merge.error ? describeApiError(merge.error, t("vocab.editor.actionFailed")) : ""}
        />
      </div>
    </Dialog>
  );
}
