import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { FormError } from "@/components/FormError";
import type { WordDraft, WordDraftError } from "@/lib/maintainer/lxxReview";
import { LemmaSearchField } from "./LemmaSearchField";
import { MorphologyField } from "./MorphologyField";

export interface WordEditorProps {
  draft: WordDraft;
  errors: WordDraftError[];
  onChange: (patch: Partial<WordDraft>) => void;
  onMorphologyValidity: (code: string, valid: boolean | undefined) => void;
  /** "Tag entfernen": lemma, Strong's and code empty (links only). */
  onClearTag: () => void;
  onReset: () => void;
  queryRef?: React.Ref<HTMLInputElement>;
  codeRef?: React.Ref<HTMLInputElement>;
}

/** The editor itself (06a): TBESG search, parse code, clear tag, reset; stacked on mobile, side by side from md. */
export function WordEditor({
  draft,
  errors,
  onChange,
  onMorphologyValidity,
  onClearTag,
  onReset,
  queryRef,
  codeRef,
}: WordEditorProps) {
  const { t } = useTranslation();
  return (
    <>
      <div className="flex flex-col gap-3 md:flex-row">
        <LemmaSearchField
          lemma={draft.lemma}
          strongs={draft.strongs}
          onPick={(entry) => onChange(entry)}
          inputRef={queryRef}
        />
        <MorphologyField
          value={draft.morphology}
          onChange={(morphology) => onChange({ morphology })}
          onValidity={onMorphologyValidity}
          inputRef={codeRef}
        />
        <div className="flex items-end gap-2">
          <Button variant="ghost" size="sm" onClick={onClearTag}>
            {t("lxxReview.edit.clearTag")}
          </Button>
          <Button variant="ghost" size="sm" onClick={onReset}>
            {t("lxxReview.edit.reset")}
          </Button>
        </div>
      </div>
      {errors.map((error) => (
        <FormError key={error} message={t(`lxxReview.error.${error}`)} />
      ))}
    </>
  );
}

export interface WordEditorRowProps extends WordEditorProps {
  /** Number of table columns for `colSpan`. */
  colSpan: number;
}

/** Editor row below the active word of the desktop table; on mobile `WordCards` renders `WordEditor` inside the card. */
export function WordEditorRow({ colSpan, ...editor }: WordEditorRowProps) {
  return (
    <tr>
      <td colSpan={colSpan} className="bg-surface-muted p-3">
        <WordEditor {...editor} />
      </td>
    </tr>
  );
}
