import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { Dialog } from "@/components/Dialog";
import { FormError } from "@/components/FormError";
import { TextField } from "@/components/TextField";
import { describeApiError } from "@/lib/api";
import { vocabLang } from "@/lib/vocab/api";
import { moveVocabKey, type VocabEditorListItem, vocabEditorKeys } from "@/lib/vocab/editorApi";
import { useEntrySearch } from "./useEntrySearch";

export interface KeyMoveDialogProps {
  /** The entry the key belongs to. */
  entryId: number;
  lemma: string;
  /** The Strong's key to move. The dialog is mounted per opening. */
  strongsKey: string;
  /** The key is the last one of the entry; the entry is retired afterwards. */
  lastKey: boolean;
  learners: number;
  /** Course code, so the target search stays in the track. */
  course?: string;
  onClose: () => void;
}

/** "G3319 verschieben": the key goes to a new entry or to an existing one (roadmap 0022.4f). */
export function KeyMoveDialog({
  entryId,
  lemma,
  strongsKey,
  lastKey,
  learners,
  course,
  onClose,
}: KeyMoveDialogProps) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const [toExisting, setToExisting] = useState(false);
  const [text, setText] = useState("");
  const [target, setTarget] = useState<VocabEditorListItem | null>(null);
  const search = useEntrySearch(course, text, entryId, toExisting);

  const move = useMutation({
    mutationFn: () => moveVocabKey(entryId, strongsKey, toExisting ? target?.id : undefined),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: vocabEditorKeys.all });
      onClose();
    },
  });

  const blocked = toExisting && target === null;
  const notify = t("vocab.editor.notify", { count: learners.toLocaleString(i18n.language) });

  return (
    <Dialog
      open
      onClose={onClose}
      title={t("vocab.editor.keyMove.title", { key: strongsKey })}
      dismissible={!move.isPending}
      size="md"
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="ghost" size="sm" onClick={onClose} disabled={move.isPending}>
            {t("vocab.editor.cancel")}
          </Button>
          <Button
            variant="primary"
            size="sm"
            disabled={blocked || move.isPending}
            onClick={() => move.mutate()}
          >
            {t("vocab.editor.keyMove.submit")}
          </Button>
        </div>
      }
    >
      <div className="grid gap-4">
        <fieldset className="grid gap-2 text-sm">
          <legend className="sr-only">{t("vocab.editor.keyMove.target")}</legend>
          <label className="flex items-center gap-2">
            <input
              type="radio"
              name="keyMoveTarget"
              className="accent-accent"
              checked={!toExisting}
              onChange={() => setToExisting(false)}
            />
            {t("vocab.editor.keyMove.newEntry")}
          </label>
          <label className="flex items-center gap-2">
            <input
              type="radio"
              name="keyMoveTarget"
              className="accent-accent"
              checked={toExisting}
              onChange={() => setToExisting(true)}
            />
            {t("vocab.editor.keyMove.existingEntry")}
          </label>
        </fieldset>

        {toExisting ? (
          <>
            <TextField
              required={false}
              size="sm"
              label={t("vocab.editor.keyMove.search")}
              placeholder={t("vocab.editor.keyMove.searchPlaceholder")}
              value={text}
              onChange={setText}
            />
            {search.items.length > 0 ? (
              <ul className="grid gap-1" aria-label={t("vocab.editor.keyMove.search")}>
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
          </>
        ) : null}

        {lastKey ? (
          <p className="rounded-md border border-sev-warning p-2 text-sm">
            <span aria-hidden="true">⚠ </span>
            {t("vocab.editor.keyMove.lastKey", { lemma })} {notify}
          </p>
        ) : null}
        <FormError
          message={move.error ? describeApiError(move.error, t("vocab.editor.actionFailed")) : ""}
        />
      </div>
    </Dialog>
  );
}
