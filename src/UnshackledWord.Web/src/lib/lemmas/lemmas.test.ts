import { describe, expect, it } from "vitest";
import { chapterWords, valueMaps, word } from "@/test/fixtures/lemmas";
import { dropdownWidth, navColumns, visibleColumns } from "./columns";
import { applyEdit, buildChanges, dirtyFields, type Edits, resetRow, valuesOf } from "./edits";
import { chapterStats, filterWords, NO_FILTERS } from "./filter";
import { moveCell, nextEditable, typeToPick } from "./gridNav";
import { applicableFields, buildRules, clearInapplicable, invalidFields } from "./rules";

const rules = buildRules(valueMaps.rules);
const none: Edits = new Map();

describe("rules", () => {
  it("allows the fields of the part of speech and, for verbs, of the verb form", () => {
    expect([...applicableFields(rules, "NOUN", null)]).toEqual(["number", "case", "gender"]);
    expect([...applicableFields(rules, "VERB", null)]).toEqual(["verbForm"]);
    expect([...applicableFields(rules, "VERB", "Fin")]).toEqual([
      "verbForm",
      "tense",
      "person",
      "number",
      "mood",
    ]);
    expect(applicableFields(rules, "ADP", null).size).toBe(0);
    expect(applicableFields(rules, null, null).size).toBe(0);
  });

  it("falls back to the built-in table without rules from the backend", () => {
    expect(applicableFields(buildRules(undefined), "ADV", null).has("degree")).toBe(true);
    expect(applicableFields(buildRules([]), "AUX", "Part").has("case")).toBe(true);
  });

  it("finds and clears values the rules do not allow", () => {
    const values = { ...valuesOf(word(1, { partOfSpeech: "NOUN", tense: "Pres", case: "Dat" })) };
    expect(invalidFields(rules, values)).toEqual(["tense"]);
    const cleared = clearInapplicable(rules, values);
    expect(cleared.tense).toBeNull();
    expect(cleared.case).toBe("Dat");
  });
});

describe("edits", () => {
  const noun = chapterWords[0] as (typeof chapterWords)[number];
  const verb = chapterWords[1] as (typeof chapterWords)[number];

  it("tracks a changed field and drops the row again when it equals the loaded value", () => {
    const changed = applyEdit(none, noun, rules, "lemma", "Lichter");
    expect(changed.get(1)?.lemma).toBe("Lichter");
    expect(dirtyFields(valuesOf(noun), changed.get(1) ?? valuesOf(noun))).toEqual(["lemma"]);
    expect(applyEdit(changed, noun, rules, "lemma", "Licht").size).toBe(0);
  });

  it("clears fields that stop applying when the part of speech changes", () => {
    const edited = applyEdit(none, noun, rules, "partOfSpeech", "ADP");
    const values = edited.get(1);
    expect(values?.partOfSpeech).toBe("ADP");
    expect(values?.case).toBeNull();
    expect(values?.number).toBeNull();
  });

  it("clears tense etc. when the verb form leaves finit", () => {
    const edited = applyEdit(none, verb, rules, "verbForm", "Inf");
    expect(edited.get(2)?.tense).toBeNull();
  });

  it("resets a row", () => {
    const edited = applyEdit(none, noun, rules, "lemma", "x");
    expect(resetRow(edited, 1).size).toBe(0);
  });

  it("builds full-replacement changes and reports incomplete rows", () => {
    let edits = applyEdit(none, noun, rules, "lemma", "  Licht2 ");
    edits = applyEdit(
      edits,
      chapterWords[2] as (typeof chapterWords)[number],
      rules,
      "lemma",
      "in",
    );
    const { changes, incompleteWordIds } = buildChanges(edits);
    expect(changes).toEqual([
      { wordId: 1, lemma: "Licht2", partOfSpeech: "NOUN", number: "S", case: "Dat" },
      { wordId: 3, lemma: "in", partOfSpeech: "ADP" },
    ]);
    expect(incompleteWordIds).toEqual([]);
    const missing = applyEdit(none, noun, rules, "lemma", null);
    expect(buildChanges(missing).incompleteWordIds).toEqual([1]);
  });
});

describe("filter", () => {
  it("matches search on word and lemma, part of speech and missing lemma", () => {
    expect(
      filterWords(chapterWords, none, { ...NO_FILTERS, search: "licht" }).map((w) => w.wordId),
    ).toEqual([1]);
    expect(
      filterWords(chapterWords, none, { ...NO_FILTERS, onlyMissingLemma: true }).map(
        (w) => w.wordId,
      ),
    ).toEqual([3]);
    expect(
      filterWords(chapterWords, none, { ...NO_FILTERS, partOfSpeech: "VERB" }).map((w) => w.wordId),
    ).toEqual([2]);
    expect(filterWords([word(9)], none, { ...NO_FILTERS, partOfSpeech: "none" })).toHaveLength(1);
  });

  it("keeps a row after an edit when evaluated against the snapshot", () => {
    const filters = { ...NO_FILTERS, onlyMissingLemma: true };
    const snapshot = none;
    const edited = applyEdit(
      none,
      chapterWords[2] as (typeof chapterWords)[number],
      rules,
      "lemma",
      "in",
    );
    expect(filterWords(chapterWords, snapshot, filters).map((w) => w.wordId)).toEqual([3]);
    expect(filterWords(chapterWords, edited, filters)).toHaveLength(0);
  });

  it("counts missing lemmas and inconsistencies", () => {
    const words = [...chapterWords, word(4, { lemma: "a", partOfSpeech: "ADP", tense: "Pres" })];
    expect(chapterStats(words, none, rules)).toEqual({
      total: 4,
      missingLemma: 1,
      inconsistencies: 1,
    });
  });
});

describe("grid navigation", () => {
  const size = { rows: 5, cols: 4 };

  it("moves and clamps", () => {
    expect(moveCell({ row: 0, col: 0 }, "ArrowUp", size)).toEqual({ row: 0, col: 0 });
    expect(moveCell({ row: 1, col: 1 }, "ArrowRight", size)).toEqual({ row: 1, col: 2 });
    expect(moveCell({ row: 1, col: 1 }, "PageDown", size)).toEqual({ row: 4, col: 1 });
    expect(moveCell({ row: 2, col: 2 }, "Home", size)).toEqual({ row: 2, col: 0 });
    expect(moveCell({ row: 2, col: 2 }, "End", size, true)).toEqual({ row: 4, col: 3 });
    expect(moveCell({ row: 2, col: 2 }, "Home", size, true)).toEqual({ row: 0, col: 0 });
  });

  it("tabs through editable cells, wrapping rows and leaving at the ends", () => {
    const editable = ({ col }: { col: number }) => col === 1 || col === 3;
    expect(nextEditable({ row: 0, col: 1 }, 1, size, editable)).toEqual({ row: 0, col: 3 });
    expect(nextEditable({ row: 0, col: 3 }, 1, size, editable)).toEqual({ row: 1, col: 1 });
    expect(nextEditable({ row: 1, col: 1 }, -1, size, editable)).toEqual({ row: 0, col: 3 });
    expect(nextEditable({ row: 4, col: 3 }, 1, size, editable)).toBeNull();
    expect(nextEditable({ row: 0, col: 1 }, -1, size, editable)).toBeNull();
  });

  it("picks dropdown values by first letter and cycles", () => {
    const options = [
      { value: "ADP", label: "präposition" },
      { value: "PRON", label: "pronomen" },
      { value: "VERB", label: "verb" },
    ];
    expect(typeToPick(options, null, "p")).toBe("ADP");
    expect(typeToPick(options, "ADP", "p")).toBe("PRON");
    expect(typeToPick(options, "PRON", "p")).toBe("ADP");
    expect(typeToPick(options, null, "v")).toBe("VERB");
    expect(typeToPick(options, null, "a")).toBe("ADP"); // matches the code
    expect(typeToPick(options, null, "z")).toBeNull();
  });
});

describe("columns", () => {
  it("hides optional columns and sizes dropdowns by their longest label", () => {
    expect(navColumns(new Set(["plain"]))).not.toContain("plain");
    expect(visibleColumns(new Set())).toContain("verse");
    expect(navColumns(new Set())).not.toContain("verse");
    expect(dropdownWidth(["a", "Präteritum"], "Tempus")).toContain("12ch");
    expect(dropdownWidth([], "Modus")).toContain("5ch");
  });
});
