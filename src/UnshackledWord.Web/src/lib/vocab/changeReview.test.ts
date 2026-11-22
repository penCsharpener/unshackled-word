import { describe, expect, it } from "vitest";
import type { VocabChangedEntry } from "./changeApi";
import { type ChangeReviewItem, toChangeReviewItems } from "./changeReview";

const translate = (key: string, options?: Record<string, string>) =>
  options ? `${key}:${JSON.stringify(options)}` : key;

const at = "2026-11-20T10:00:00Z";

function first(items: ChangeReviewItem[]): ChangeReviewItem {
  const [head] = items;
  if (!head) throw new Error("no item");
  return head;
}

describe("toChangeReviewItems", () => {
  it("maps meanings before and after from the stored JSON", () => {
    const entries: VocabChangedEntry[] = [
      {
        entryId: 7,
        lemma: "μέσος",
        dictionaryForm: "μέσος, -η, -ον",
        lang: "grc",
        status: "changed",
        changes: [
          {
            kind: "meanings",
            before: { meanings: ["Mitte"] },
            after: { meanings: ["Mitte", "mitten"] },
            changedAt: at,
          },
        ],
      },
    ];
    expect(first(toChangeReviewItems(entries, translate))).toMatchObject({
      entryId: 7,
      form: "μέσος, -η, -ον",
      lang: "grc",
      removed: false,
      changes: [
        {
          kind: "learn.changes.kind.meanings",
          before: ["Mitte"],
          after: ["Mitte", "mitten"],
        },
      ],
    });
  });

  it("shows both old forms of a kept merge, the merged-away one struck out in before", () => {
    const entries: VocabChangedEntry[] = [
      {
        entryId: 2,
        lemma: "μεσόω",
        dictionaryForm: "μεσόω",
        lang: "grc",
        status: "changed",
        changes: [
          {
            kind: "merge",
            before: { keys: ["G3319"], meanings: ["Mitte"] },
            after: { keys: ["G3319"], meanings: ["Mitte"], mergedFrom: 1 },
            changedAt: at,
            targetEntryId: 1,
            targetLemma: "μέσος",
            targetDictionaryForm: "μέσος, -η, -ον",
          },
        ],
      },
    ];
    expect(first(toChangeReviewItems(entries, translate)).changes[0]).toMatchObject({
      kind: "learn.changes.kind.merge",
      before: ["μεσόω", "μέσος, -η, -ον"],
      after: ["μεσόω"],
    });
  });

  it("gives a removed merged-away word one label and the target as note", () => {
    const entries: VocabChangedEntry[] = [
      {
        entryId: 9,
        lemma: "κράβαττος",
        lang: "grc",
        status: "removed",
        changes: [
          {
            kind: "merge",
            after: { mergedInto: 4 },
            changedAt: at,
            targetEntryId: 4,
            targetLemma: "κράβατος",
          },
        ],
      },
    ];
    const item = first(toChangeReviewItems(entries, translate));
    expect(item.removed).toBe(true);
    expect(item.changes).toEqual([
      {
        key: `${at}-merge`,
        kind: "learn.changes.removed",
        note: `learn.changes.mergedInto:${JSON.stringify({ lemma: "κράβατος" })}`,
      },
    ]);
  });

  it("skips change kinds it does not know", () => {
    const entries: VocabChangedEntry[] = [
      {
        entryId: 3,
        lemma: "λόγος",
        lang: "grc",
        status: "changed",
        changes: [{ kind: "something-new", changedAt: at }],
      },
    ];
    expect(first(toChangeReviewItems(entries, translate)).changes).toEqual([]);
  });
});
