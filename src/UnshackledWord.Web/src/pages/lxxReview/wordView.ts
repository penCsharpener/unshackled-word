import {
  initialWordDraft,
  type LxxReviewHebrewWord,
  type LxxReviewWord,
  sameWordDraft,
  type WordDraft,
  type WordDraftError,
} from "@/lib/maintainer/lxxReview";

export interface FieldView {
  value: string;
  /** Differs from the state the card showed first (`bg-mark-same` + sr-only "geändert"). */
  changed: boolean;
  /** The stored value when the AI answer differs from it (shown struck through under the AI value). */
  stored?: string;
  invalid: boolean;
}

export interface WordView {
  lemma: FieldView;
  strongs: FieldView;
  morphology: FieldView;
  hebrewChanged: boolean;
  hebrew: LxxReviewHebrewWord[];
  invalid: boolean;
}

/** Everything the desktop table and the mobile cards show for one word. */
export function viewWord(
  word: LxxReviewWord,
  draft: WordDraft | undefined,
  errors: readonly WordDraftError[],
  hebrewById: ReadonlyMap<number, LxxReviewHebrewWord>,
): WordView {
  const current = draft ?? initialWordDraft(word);
  const initial = initialWordDraft(word);
  const field = (key: "lemma" | "strongs" | "morphology", invalid: boolean): FieldView => {
    const stored = word.tag?.[key];
    return {
      value: current[key],
      changed: current[key] !== initial[key],
      stored: word.ai && stored && stored !== word.ai[key] ? stored : undefined,
      invalid,
    };
  };
  const partial = errors.includes("partial");
  return {
    lemma: field("lemma", partial),
    strongs: field("strongs", partial || errors.includes("strongs")),
    morphology: field("morphology", partial || errors.includes("morphology")),
    hebrewChanged: !sameWordDraft(
      { ...current, lemma: "", strongs: "", morphology: "" },
      { ...initial, lemma: "", strongs: "", morphology: "" },
    ),
    hebrew: current.hebrewWordIds
      .map((id) => hebrewById.get(id))
      .filter((h): h is LxxReviewHebrewWord => h !== undefined),
    invalid: errors.length > 0,
  };
}
