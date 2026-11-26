import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Select } from "@/components/Select";
import { TextField } from "@/components/TextField";
import { lexiconQueryReady, useLxxLexicon } from "@/lib/maintainer/lxxReview";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

export interface LemmaSearchFieldProps {
  lemma: string;
  strongs: string;
  /** Sets lemma and Strong's together (the server checks the pair against TBESG). */
  onPick: (entry: { lemma: string; strongs: string }) => void;
  inputRef?: React.Ref<HTMLInputElement>;
}

const LEXICON_DEBOUNCE_MS = 250;

/**
 * TBESG search (06a "Lemma + Strong's"): query field (a Strong's number or the start of a lemma), hits in a Select, Enter takes
 * the hit when there is exactly one. The current pair is the Select's first option.
 */
export function LemmaSearchField({ lemma, strongs, onPick, inputRef }: LemmaSearchFieldProps) {
  const { t } = useTranslation();
  const [query, setQuery] = useState(strongs || lemma);
  const debounced = useDebouncedValue(query.trim(), LEXICON_DEBOUNCE_MS);
  const ready = lexiconQueryReady(debounced);
  const lexicon = useLxxLexicon(debounced);
  const entries = ready ? (lexicon.data ?? []) : [];
  const settled = ready && debounced === query.trim() && lexicon.isSuccess;
  const current = strongs ? { value: strongs, label: `${strongs} · ${lemma}` } : undefined;

  return (
    <div className="flex flex-col gap-2">
      <TextField
        type="search"
        size="sm"
        label={t("lxxReview.edit.query")}
        value={query}
        onChange={setQuery}
        onKeyDown={(event) => {
          if (event.key === "Enter" && settled && entries.length === 1 && entries[0]) {
            event.preventDefault();
            onPick({ lemma: entries[0].lemma, strongs: entries[0].strongs });
          }
        }}
        inputRef={inputRef}
        required={false}
      />
      <Select
        label={t("lxxReview.edit.entry")}
        size="sm"
        value={strongs}
        onChange={(value) => {
          const entry = entries.find((e) => e.strongs === value);
          if (entry) onPick({ lemma: entry.lemma, strongs: entry.strongs });
        }}
        options={[
          current ?? { value: "", label: "–" },
          ...entries
            .filter((e) => e.strongs !== strongs)
            .map((e) => ({ value: e.strongs, label: `${e.strongs} · ${e.lemma} · ${e.gloss}` })),
        ]}
      />
      {settled && entries.length === 0 ? (
        <p className="text-sm text-fg-muted">
          {t("lxxReview.edit.noEntry")}. {t("lxxReview.edit.noEntryHint")}
        </p>
      ) : null}
    </div>
  );
}
