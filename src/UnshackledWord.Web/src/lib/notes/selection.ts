import type { VerseWords } from "@/lib/bible/types";
import { wordKey } from "@/lib/bible/words";
import type { Note, NoteTarget } from "./api";

/** A word of the reader: verse number and index in that verse's shown words. */
export interface WordPoint {
  verse: number;
  index: number;
}

/** Words marked for a note; `anchor` is where it started, `focus` where it ends (either order). */
export interface WordSelection {
  anchor: WordPoint;
  focus: WordPoint;
  /** touch: the first word was long-pressed, a tap on the last word completes the selection */
  pending?: boolean;
}

const before = (a: WordPoint, b: WordPoint) =>
  a.verse < b.verse || (a.verse === b.verse && a.index <= b.index);

/** Start and end of the selection in reading order. */
export function selectionRange(selection: WordSelection): { start: WordPoint; end: WordPoint } {
  const { anchor, focus } = selection;
  return before(anchor, focus) ? { start: anchor, end: focus } : { start: focus, end: anchor };
}

export function isWordSelected(selection: WordSelection | undefined, point: WordPoint): boolean {
  if (!selection) return false;
  const { start, end } = selectionRange(selection);
  return before(start, point) && before(point, end);
}

/**
 * What a selection can anchor: words of one verse (Elb1871 positions, also through a variant word's base position), a verse
 * range when it crosses verses, or the verse when a selected word exists only in the variant (no Elb1871 position).
 */
export function selectionTarget(
  selection: WordSelection,
  verses: readonly VerseWords[],
): NoteTarget | undefined {
  const { start, end } = selectionRange(selection);
  if (start.verse !== end.verse) return { kind: "verses", verse: start.verse, endVerse: end.verse };
  const words = verses
    .find((v) => v.verse === start.verse)
    ?.words.slice(start.index, end.index + 1);
  if (!words || words.length === 0) return undefined;
  const positions = words.map((w) => w.basePosition);
  if (positions.some((p) => p === undefined)) return { kind: "verse", verse: start.verse };
  const numbers = positions as number[];
  return {
    kind: "words",
    verse: start.verse,
    startPosition: Math.min(...numbers),
    endPosition: Math.max(...numbers),
    quote: words.map((w) => w.text).join(" "),
  };
}

/**
 * Key (`wordKey`) of the shown word that carries a words note's marker: the last word of the anchored range, through
 * `mappedWords` on a variant, else through the Elb1871 positions. Undefined: the marker belongs at the verse number
 * (other kinds, `drifted`, `noBaseWords`, or the shown wording does not contain the words).
 */
export function noteMarkerWord(note: Note, verse: VerseWords | undefined): string | undefined {
  const { anchor } = note;
  if (!verse || anchor.kind !== "words" || note.anchorState !== "ok") return undefined;
  const startPosition = anchor.startPosition;
  if (startPosition === undefined) return undefined;
  const endPosition = anchor.endPosition ?? startPosition;
  const mapped = note.mappedWords && new Set(note.mappedWords.map((m) => m.revisionWordId));
  const carrying = verse.words.filter((w) =>
    mapped
      ? w.revisionWordId !== undefined && mapped.has(w.revisionWordId)
      : w.revisionWordId === undefined &&
        w.basePosition !== undefined &&
        w.basePosition >= startPosition &&
        w.basePosition <= endPosition,
  );
  const last = carrying.at(-1);
  return last && wordKey(last);
}
