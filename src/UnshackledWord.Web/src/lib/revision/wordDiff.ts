export interface DiffPart {
  kind: "same" | "added" | "removed";
  text: string;
}

const words = (text: string): string[] => text.split(/\s+/).filter((w) => w.length > 0);

/** Word-level diff (LCS over whitespace-split words); consecutive words of one kind are merged. */
export function wordDiff(before: string, after: string): DiffPart[] {
  const a = words(before);
  const b = words(after);
  const width = b.length + 1;
  const lcs = new Array<number>((a.length + 1) * width).fill(0);
  const at = (i: number, j: number): number => lcs[i * width + j] ?? 0;
  for (let i = a.length - 1; i >= 0; i--) {
    for (let j = b.length - 1; j >= 0; j--) {
      lcs[i * width + j] =
        a[i] === b[j] ? at(i + 1, j + 1) + 1 : Math.max(at(i + 1, j), at(i, j + 1));
    }
  }
  const parts: DiffPart[] = [];
  const push = (kind: DiffPart["kind"], word: string) => {
    const last = parts[parts.length - 1];
    if (last?.kind === kind) last.text += ` ${word}`;
    else parts.push({ kind, text: word });
  };
  let i = 0;
  let j = 0;
  while (i < a.length || j < b.length) {
    const wa = a[i];
    const wb = b[j];
    if (wa !== undefined && wa === wb) {
      push("same", wa);
      i++;
      j++;
    } else if (wb !== undefined && (wa === undefined || at(i, j + 1) >= at(i + 1, j))) {
      push("added", wb);
      j++;
    } else if (wa !== undefined) {
      push("removed", wa);
      i++;
    }
  }
  return parts;
}
