import { useQuery } from "@tanstack/react-query";
import { useCallback, useState } from "react";
import { bibleApi } from "./api";
import { bookAbbreviation } from "./books";
import type { BibleStructureBook, LxxUnit, VerseMappingOriginalWord } from "./types";

/** Daniel's two Greek text forms: Old Greek and Theodotion (roadmap 0028). */
export type LxxForm = "og" | "th";

export const LXX_FORM_KEY = "uw.reader.lxxForm";
export const DANIEL_BOOK_ID = 27;

/** The LXX mode exists for Old Testament chapters only (Elberfelder books 1..39). */
export function hasLxx(bookId: number): boolean {
  return bookId >= 1 && bookId <= 39;
}

/** Stored Daniel text form; Theodotion when nothing (or no storage, or anything unknown) is there. */
export function readLxxForm(): LxxForm {
  try {
    return localStorage.getItem(LXX_FORM_KEY) === "og" ? "og" : "th";
  } catch {
    return "th";
  }
}

/** The reader's Daniel text form, global for all chapters and remembered in this browser. */
export function useLxxForm() {
  const [form, setForm] = useState<LxxForm>(readLxxForm);
  const update = useCallback((next: LxxForm) => {
    try {
      localStorage.setItem(LXX_FORM_KEY, next);
    } catch {
      // storage unavailable: the choice only lasts for this page view
    }
    setForm(next);
  }, []);
  return [form, update] as const;
}

/** Units of the chosen text form; units without a form (every book but Daniel) always stay. */
export function unitsOfForm(units: readonly LxxUnit[], form: LxxForm): LxxUnit[] {
  return units.filter((u) => u.textForm === "" || u.textForm === form);
}

/** A run of the verse's units: one mapped unit, or consecutive additions shown in one frame. */
export type LxxBlock = { kind: "unit"; unit: LxxUnit } | { kind: "addition"; units: LxxUnit[] };

/** Groups consecutive additions (Est addition A has 17 units) into one block; the reading order is kept. */
export function lxxBlocks(units: readonly LxxUnit[]): LxxBlock[] {
  const blocks: LxxBlock[] = [];
  for (const unit of units) {
    const last = blocks[blocks.length - 1];
    if (unit.isAddition && last?.kind === "addition") last.units.push(unit);
    else if (unit.isAddition) blocks.push({ kind: "addition", units: [unit] });
    else blocks.push({ kind: "unit", unit });
  }
  return blocks;
}

/**
 * Rahlfs reference of a unit with the reader's book abbreviation: "Jer 32,1" (German) or "Jer 32:1" (English); a
 * subverse as ".k" like the source label (`1Ki.2:35.1`). The numeric book id is shown until the books are loaded.
 */
export function lxxReference(
  unit: Pick<LxxUnit, "bibleBookId" | "chapter" | "verse" | "subverse">,
  books: readonly BibleStructureBook[] | undefined,
  language: string,
): string {
  const book = books?.find((b) => b.id === unit.bibleBookId);
  const name = book ? bookAbbreviation(book, language) : String(unit.bibleBookId);
  const separator = language.startsWith("en") ? ":" : ",";
  const sub = unit.subverse > 0 ? `.${unit.subverse}` : "";
  return `${name} ${unit.chapter}${separator}${unit.verse}${sub}`;
}

/**
 * The words of one or more units as `OriginalVerse` words, in reading order: positions run on across units, because
 * `OriginalVerse` sorts by position and each unit starts again at 1.
 */
export function lxxOriginalWords(
  units: readonly LxxUnit[],
  books?: readonly BibleStructureBook[],
  language = "de",
): VerseMappingOriginalWord[] {
  let position = 0;
  return units.flatMap((u) => {
    const ref = lxxReference(u, books, language);
    return [...u.words]
      .sort((a, b) => a.position - b.position)
      .map((w) => ({
        id: w.id,
        position: ++position,
        original: w.greek,
        // tagged (roadmap 0028.5): same fields as an NT word, so the margin card shows them alike
        ...(w.tag && {
          lemma: w.tag.lemma,
          strongs: w.tag.strongs,
          gloss: w.tag.gloss,
          morphologyCode: w.tag.morphology.code,
          morphology: w.tag.morphology,
        }),
        lxx: { ref, status: w.tag?.status },
      }));
  });
}

/** One LXX word with tag and lexicon entry, fetched when its detail opens (the tag may change, so it is not kept long). */
export function useLxxWord(lxxWordId: number | undefined) {
  return useQuery({
    queryKey: ["bible", "lxxWord", lxxWordId ?? 0] as const,
    queryFn: ({ signal }) => bibleApi.lxxWord(lxxWordId ?? 0, signal),
    enabled: lxxWordId !== undefined,
    staleTime: 60_000,
  });
}
