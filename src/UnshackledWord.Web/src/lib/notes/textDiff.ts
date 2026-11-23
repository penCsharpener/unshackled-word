import { type DiffPart, wordDiff } from "@/lib/revision/wordDiff";

/** A sentence present in both texts, or a run of changed sentences with its word diff. */
export type SentenceSegment =
  | { kind: "same"; text: string }
  | { kind: "changed"; parts: DiffPart[] };

/** Sentences of a text: split after `.`, `!`, `?` (plus closing quotes/brackets) and at line breaks, whitespace collapsed. */
export function sentences(text: string): string[] {
  return text
    .split(/(?<=[.!?…][)"'“”»«]*)\s+|\n+/)
    .map((s) => s.replace(/\s+/g, " ").trim())
    .filter((s) => s.length > 0);
}

/**
 * Diff of an approved and a current note text: unchanged sentences stay whole, every run of changed sentences
 * carries a word diff (`added` / `removed` / `same`) of the old against the new run. LCS over sentences, no dependency.
 */
export function sentenceDiff(before: string, after: string): SentenceSegment[] {
  const a = sentences(before);
  const b = sentences(after);
  const width = b.length + 1;
  const lcs = new Array<number>((a.length + 1) * width).fill(0);
  const at = (i: number, j: number): number => lcs[i * width + j] ?? 0;
  for (let i = a.length - 1; i >= 0; i--) {
    for (let j = b.length - 1; j >= 0; j--) {
      lcs[i * width + j] =
        a[i] === b[j] ? at(i + 1, j + 1) + 1 : Math.max(at(i + 1, j), at(i, j + 1));
    }
  }

  const segments: SentenceSegment[] = [];
  let removed: string[] = [];
  let added: string[] = [];
  const flush = () => {
    if (removed.length === 0 && added.length === 0) return;
    segments.push({ kind: "changed", parts: wordDiff(removed.join(" "), added.join(" ")) });
    removed = [];
    added = [];
  };

  let i = 0;
  let j = 0;
  while (i < a.length || j < b.length) {
    const sa = a[i];
    const sb = b[j];
    if (sa !== undefined && sa === sb) {
      flush();
      segments.push({ kind: "same", text: sa });
      i++;
      j++;
    } else if (sb !== undefined && (sa === undefined || at(i, j + 1) >= at(i + 1, j))) {
      added.push(sb);
      j++;
    } else if (sa !== undefined) {
      removed.push(sa);
      i++;
    }
  }
  flush();
  return segments;
}
