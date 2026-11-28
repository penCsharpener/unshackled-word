import { useQuery } from "@tanstack/react-query";
import type { KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/Button";
import { ScriptKeyboard } from "@/components/ScriptKeyboard";
import { TextField } from "@/components/TextField";
import { useDebouncedValue } from "@/lib/useDebouncedValue";
import type { VocabTrack } from "@/lib/vocab/api";
import { searchVocabRoots, type VocabRootCandidate, vocabEditorKeys } from "@/lib/vocab/editorApi";
import { BACKSPACE_KEY, remapPhysicalKey } from "@/lib/vocab/scriptLayouts";

const ROMAN = ["", "", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"];

/** `ענה II` for the second root with the same letters; the first one stays plain. */
export function rootLabel(root: string, homograph: number) {
  return homograph > 1 ? `${root} ${ROMAN[homograph] ?? homograph}` : root;
}

export interface RootSearchProps {
  track: VocabTrack;
  label: string;
  placeholder?: string;
  text: string;
  onTextChange: (text: string) => void;
  /** Marks the row of the current pick. */
  isPicked?: (candidate: VocabRootCandidate) => boolean;
  onPick: (candidate: VocabRootCandidate) => void;
}

/**
 * Root picker of the vocab editor (roadmap 0030.5f): a text field and the matching candidates below it,
 * Greek words by lemma prefix, Hebrew roots by letters. The first result of a word without a root row
 * has no `rootId`; the caller sends its Strong's key instead.
 */
export function RootSearch({
  track,
  label,
  placeholder,
  text,
  onTextChange,
  isPicked,
  onPick,
}: RootSearchProps) {
  const { t } = useTranslation();
  const term = useDebouncedValue(text.trim(), 300);
  const query = useQuery({
    queryKey: vocabEditorKeys.rootSearch(track, term),
    queryFn: ({ signal }) => searchVocabRoots(track, term, signal),
    enabled: term.length > 0,
  });
  const lang = track === "H" ? "he" : "grc";
  const items = query.data?.items ?? [];
  const layout = track === "H" ? "hebrew" : "greek";

  // Latin keys type Greek or Hebrew letters (the physical mapping the on-screen keyboard shows).
  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    const letter = remapPhysicalKey(layout, event.key);
    if (letter === undefined) return;
    event.preventDefault();
    onTextChange(text + letter);
  }

  function handleKey(key: string) {
    onTextChange(key === BACKSPACE_KEY ? [...text].slice(0, -1).join("") : text + key);
  }

  return (
    <div className="grid gap-2">
      <TextField
        required={false}
        size="sm"
        label={label}
        placeholder={placeholder}
        value={text}
        onChange={onTextChange}
        lang={lang}
        dir={track === "H" ? "rtl" : undefined}
        className="font-original"
        onKeyDown={handleKeyDown}
      />
      <ScriptKeyboard layout={layout} onKey={handleKey} />
      {items.length > 0 ? (
        <ul className="grid gap-1" aria-label={label}>
          {items.map((item) => (
            <li key={`${item.rootId ?? "word"}-${item.strongsKey ?? item.root}-${item.homograph}`}>
              <Button
                variant="secondary"
                size="sm"
                className="w-full justify-between gap-3"
                aria-pressed={isPicked?.(item) ?? false}
                onClick={() => onPick(item)}
              >
                <span lang={lang} className="font-original">
                  {rootLabel(item.root, item.homograph)}
                </span>
                <span className="truncate text-fg-muted">
                  {[item.strongsKey, item.meaning].filter(Boolean).join(" · ")}
                  {track === "G" && item.rootId == null
                    ? ` · ${t("vocab.editor.root.noRootRow")}`
                    : ""}
                </span>
              </Button>
            </li>
          ))}
        </ul>
      ) : term && !query.isFetching && query.isSuccess ? (
        <p className="text-sm text-fg-muted">{t("vocab.editor.noMatches")}</p>
      ) : null}
    </div>
  );
}
