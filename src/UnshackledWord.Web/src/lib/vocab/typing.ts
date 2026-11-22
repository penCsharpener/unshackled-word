import type { VocabTypingPart } from "./sessionApi";

/** The boxes of a typing card: a run of letter boxes (`group` = number of boxes) or fixed text between groups. */
export type TypingLayout = ({ group: number } | { sep: string })[];

/** Converts the API layout; the letters themselves are never sent. */
export function toTypingLayout(parts: VocabTypingPart[]): TypingLayout {
  return parts.map((part) =>
    part.kind === "group" ? { group: part.length ?? 0 } : { sep: part.text ?? "" },
  );
}

/** Lengths of the letter groups, in order. */
export function groupLengths(layout: TypingLayout): number[] {
  return layout.flatMap((part) => ("group" in part ? [part.group] : []));
}

/** One empty string per group. */
export function emptyAnswers(layout: TypingLayout): string[] {
  return groupLengths(layout).map(() => "");
}

/** Every box of every group is filled. */
export function isComplete(layout: TypingLayout, value: string[]): boolean {
  return groupLengths(layout).every((length, index) => [...(value[index] ?? "")].length >= length);
}

const IS_LETTER = /^\p{L}$/u;

/** A single letter (any script); anything else is not typed into a box. */
export function isLetter(ch: string): boolean {
  return IS_LETTER.test(ch);
}

/** Sets the letter at `position` of `group` (appends when the position is the first free one). */
export function setLetter(
  layout: TypingLayout,
  value: string[],
  group: number,
  position: number,
  ch: string,
): string[] {
  const length = groupLengths(layout)[group] ?? 0;
  const chars = [...(value[group] ?? "")];
  if (position >= length || position > chars.length) return value;
  chars[position] = ch;
  return value.map((text, index) => (index === group ? chars.join("") : text));
}

/** Removes the letter at `position` of `group`; the letters behind it move up. */
export function removeLetter(value: string[], group: number, position: number): string[] {
  const chars = [...(value[group] ?? "")];
  if (position >= chars.length) return value;
  chars.splice(position, 1);
  return value.map((text, index) => (index === group ? chars.join("") : text));
}

/** Types a letter into the first free box (on-screen keyboard); a full form stays as it is. */
export function pushLetter(layout: TypingLayout, value: string[], ch: string): string[] {
  const lengths = groupLengths(layout);
  const group = lengths.findIndex((length, index) => [...(value[index] ?? "")].length < length);
  if (group < 0) return value;
  return setLetter(layout, value, group, [...(value[group] ?? "")].length, ch);
}

/** Deletes the last typed letter (on-screen keyboard). */
export function popLetter(layout: TypingLayout, value: string[]): string[] {
  for (let group = groupLengths(layout).length - 1; group >= 0; group -= 1) {
    const count = [...(value[group] ?? "")].length;
    if (count > 0) return removeLetter(value, group, count - 1);
  }
  return value;
}
