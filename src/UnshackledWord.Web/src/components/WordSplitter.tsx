import { Fragment, type KeyboardEvent, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { lettersOf, MAX_PART_LENGTH, partsFromCuts, toggleCut } from "@/lib/bible/mappingEditor";
import { Button } from "./Button";
import { FormError } from "./FormError";
import { TextField } from "./TextField";

export interface WordSplitterProps {
  /** The German word to cut into parts. */
  word: string;
  /** Cut positions: a cut after the n-th letter (1 to length - 1), sorted. */
  cuts: readonly number[];
  onChange: (cuts: number[]) => void;
  /** "Teilen": only enabled for two or more parts that are not longer than `maxPartLength`. */
  onConfirm: () => void;
  onCancel: () => void;
  /** Longest allowed part (default: the server's limit of 30 characters). */
  maxPartLength?: number;
  /** Lemma per part in order (roadmap 0018, `Gerste` for `Gersten`); the fields are shown only with `onLemmasChange`. */
  lemmas?: readonly string[];
  onLemmasChange?: (lemmas: string[]) => void;
}

/**
 * Cuts one word into any number of parts: the word is drawn as letters with a toggle in every gap
 * (click, or arrow keys to move between the gaps and Space to toggle) and a live preview ("Weizen | ernte").
 */
export function WordSplitter({
  word,
  cuts,
  onChange,
  onConfirm,
  onCancel,
  maxPartLength = MAX_PART_LENGTH,
  lemmas = [],
  onLemmasChange,
}: WordSplitterProps) {
  const { t } = useTranslation();
  const letters = lettersOf(word);
  const gaps = letters.length - 1;
  const [active, setActive] = useState(1);
  const groupRef = useRef<HTMLFieldSetElement>(null);
  const parts = partsFromCuts(word, cuts);
  const tooLong = parts.filter((part) => lettersOf(part).length > maxPartLength);
  const canConfirm = parts.length >= 2 && tooLong.length === 0;

  const focusGap = (gap: number) => {
    const next = Math.min(Math.max(gap, 1), gaps);
    setActive(next);
    groupRef.current?.querySelector<HTMLElement>(`[data-gap="${next}"]`)?.focus();
  };

  useEffect(() => {
    groupRef.current?.querySelector<HTMLElement>('[data-gap="1"]')?.focus();
  }, []);

  function onKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key === "Escape") {
      event.preventDefault();
      event.stopPropagation();
      onCancel();
      return;
    }
    const onGap = event.target instanceof HTMLElement && event.target.dataset.gap !== undefined;
    if (!onGap) return;
    const keys: Record<string, number> = {
      ArrowLeft: active - 1,
      ArrowRight: active + 1,
      Home: 1,
      End: gaps,
    };
    const target = keys[event.key];
    if (target !== undefined) {
      event.preventDefault();
      focusGap(target);
    }
  }

  return (
    <fieldset
      ref={groupRef}
      className="mt-2 flex flex-col gap-2 rounded-md border border-border bg-surface p-3"
      onKeyDown={onKeyDown}
    >
      <legend className="sr-only">{t("bible.mapping.split.title", { word })}</legend>
      <p className="text-fg-muted">{t("bible.mapping.split.hint")}</p>
      <div className="flex flex-wrap items-center font-scripture text-lg" lang="de" dir="ltr">
        {letters.map((letter, index) => {
          const gap = index + 1;
          const cut = cuts.includes(gap);
          return (
            // biome-ignore lint/suspicious/noArrayIndexKey: letters repeat, the position is the identity
            <Fragment key={index}>
              <span>{letter}</span>
              {gap <= gaps && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="min-h-8 min-w-6 px-1 text-fg-muted aria-pressed:font-bold aria-pressed:text-accent"
                  aria-pressed={cut}
                  aria-label={t("bible.mapping.split.gap", {
                    before: letters.slice(0, gap).join(""),
                    after: letters.slice(gap).join(""),
                  })}
                  data-gap={gap}
                  tabIndex={active === gap ? 0 : -1}
                  onFocus={() => setActive(gap)}
                  onClick={() => onChange(toggleCut(cuts, gap))}
                >
                  <span aria-hidden="true">{cut ? "|" : "·"}</span>
                </Button>
              )}
            </Fragment>
          );
        })}
      </div>
      <p aria-live="polite" lang="de">
        <span className="text-fg-muted">{t("bible.mapping.split.preview")} </span>
        <strong>{parts.join(" | ")}</strong>
      </p>
      {parts.length < 2 && <p className="text-fg-muted">{t("bible.mapping.split.needCut")}</p>}
      {tooLong.map((part) => (
        <FormError
          key={part}
          message={t("bible.mapping.split.tooLong", { part, max: maxPartLength })}
        />
      ))}
      {onLemmasChange && parts.length >= 2 && (
        <div className="flex flex-col gap-1">
          <p className="text-fg-muted">{t("bible.mapping.split.lemmas")}</p>
          <div className="flex flex-wrap gap-2" lang="de">
            {parts.map((part, index) => (
              <TextField
                // biome-ignore lint/suspicious/noArrayIndexKey: parts repeat, the position is the identity
                key={index}
                size="sm"
                required={false}
                maxLength={40}
                label={t("bible.mapping.roles.partLemma", { part })}
                value={lemmas[index] ?? ""}
                onChange={(value) => {
                  const next = parts.map((_, i) => lemmas[i] ?? "");
                  next[index] = value;
                  onLemmasChange(next);
                }}
              />
            ))}
          </div>
        </div>
      )}
      <p className="text-fg-muted">{t("bible.mapping.split.relink")}</p>
      <div className="flex flex-wrap gap-2">
        <Button variant="primary" size="sm" disabled={!canConfirm} onClick={onConfirm}>
          {t("bible.mapping.split.confirm")}
        </Button>
        <Button variant="secondary" size="sm" onClick={onCancel}>
          {t("bible.mapping.split.cancel")}
        </Button>
      </div>
    </fieldset>
  );
}
