import type { SelectOption } from "@/components/Select";
import type { Labels } from "./labels";
import type { ValueMapsResponse } from "./types";

/** Option value of the dropdown that widens the STTS list to every tag. */
export const SHOW_ALL_STTS = "__all__";

export interface SttsHelper {
  /** The tag does not accept the part of speech (both set). */
  mismatch: (partOfSpeech: string | null, stts: string | null) => boolean;
  /** The tag that matches the part of speech (refined by verb form / mood), or null. */
  proposal: (
    partOfSpeech: string | null,
    verbForm: string | null,
    mood: string | null,
  ) => string | null;
  /** Dropdown options: all tags, or only those accepting the part of speech (the current value always stays). */
  options: (
    partOfSpeech: string | null,
    current: string | null,
    showAll: boolean,
  ) => readonly SelectOption[];
}

/** Built from the value maps (SttsTags of the table, proposal rules of the backend). Without tags nothing is flagged or filtered. */
export function buildStts(maps: ValueMapsResponse | undefined, labels: Labels): SttsHelper {
  const tags = new Map((maps?.sttsTags ?? []).map((tag) => [tag.tag, tag]));
  const proposals = maps?.sttsProposals ?? [];
  const all = labels.stts ?? [];
  const allowed = (tag: string, pos: string) => tags.get(tag)?.allowedUpos.includes(pos) ?? true;

  return {
    mismatch: (pos, stts) => !!pos && !!stts && tags.size > 0 && !allowed(stts, pos),
    proposal: (pos, verbForm, mood) => {
      if (!pos) return null;
      const rule = proposals.find(
        (r) =>
          r.partOfSpeech === pos &&
          (r.verbForm === undefined || r.verbForm === verbForm) &&
          (r.mood === undefined || r.mood === mood),
      );
      return rule && allowed(rule.stts, pos) ? rule.stts : null;
    },
    options: (pos, current, showAll) => {
      const fits = (tag: string) => !pos || tags.size === 0 || allowed(tag, pos) || tag === current;
      const list = showAll ? all : all.filter((option) => fits(option.value));
      return list.map((option) => ({
        value: option.value,
        label: `${option.value} – ${option.label}`,
      }));
    },
  };
}
