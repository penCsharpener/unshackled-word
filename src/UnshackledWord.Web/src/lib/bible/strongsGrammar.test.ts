import { describe, expect, it } from "vitest";
import {
  activeGrammarValue,
  grammarTerms,
  grammarValueLabel,
  grammarValueToken,
  parseGrammarFilter,
  removeGrammarTerm,
  setGrammarValue,
  slotGrammarFilter,
  slotStrongsLink,
} from "./strongsGrammar";

const slot = (
  filters: { key: string; values: string[]; negated?: boolean; german?: boolean }[],
) => ({
  text: "agape",
  readAs: "lemma" as const,
  lemmas: [{ strongs: "G26", lemma: "ἀγάπη", transliteration: "agape", gloss: "" }],
  filters: filters.map((f) => ({ negated: false, german: false, ...f })),
});

describe("strongsGrammar", () => {
  it("parses the grammar parameter", () => {
    expect(parseGrammarFilter(null)).toBeUndefined();
    expect(parseGrammarFilter("  ")).toBeUndefined();
    expect(parseGrammarFilter(" Kasus:nom   Numerus:Plural ")).toBe("Kasus:nom Numerus:Plural");
    expect(parseGrammarFilter("x".repeat(201))).toBeUndefined();
  });

  it("splits terms with OR values and negation, ignoring junk", () => {
    expect(grammarTerms("Kasus:nom,gen -Numerus:Plural foo")).toEqual([
      { raw: "Kasus:nom,gen", negated: false, key: "Kasus", values: ["nom", "gen"] },
      { raw: "-Numerus:Plural", negated: true, key: "Numerus", values: ["Plural"] },
    ]);
  });

  it("makes value tokens the server reads", () => {
    expect(grammarValueToken("Form", "Perfekt (Qatal)")).toBe("PerfektQatal");
    expect(grammarValueToken("Person", "1. Person")).toBe("1.Person");
    expect(grammarValueToken("Kasus", "Nominativ")).toBe("Nominativ");
  });

  it("sets, replaces and removes one value per key", () => {
    const one = setGrammarValue(undefined, "Kasus", "Nominativ");
    expect(one).toBe("Kasus:Nominativ");
    const two = setGrammarValue(one, "Numerus", "Plural");
    expect(two).toBe("Kasus:Nominativ Numerus:Plural");
    expect(setGrammarValue(two, "Kasus", "Genitiv")).toBe("Kasus:Genitiv Numerus:Plural");
    expect(setGrammarValue(two, "Kasus", null)).toBe("Numerus:Plural");
    expect(setGrammarValue("Kasus:Genitiv", "Kasus", null)).toBeUndefined();
    expect(setGrammarValue("-Kasus:nom", "Kasus", "Dativ")).toBe("-Kasus:nom Kasus:Dativ");
  });

  it("removes a term and reads the active value", () => {
    const [first] = grammarTerms("Kasus:nom Numerus:Plural");
    expect(first && removeGrammarTerm("Kasus:nom Numerus:Plural", first)).toBe("Numerus:Plural");
    expect(activeGrammarValue("Kasus:nom", "kasus")).toBe("nom");
    expect(activeGrammarValue("Kasus:nom,gen", "Kasus")).toBeUndefined();
    expect(activeGrammarValue("-Kasus:nom", "Kasus")).toBeUndefined();
  });

  it("labels values by language, also through aliases", () => {
    const vocabulary = {
      germanPrefix: "dt.",
      germanKeysEnabled: true,
      keys: [
        {
          name: "Kasus",
          aliases: [],
          germanPrefixAllowed: true,
          languages: ["greek"],
          kind: "filter" as const,
          freeValue: false,
          values: [
            {
              key: "nominative",
              labelDe: "Nominativ",
              labelEn: "nominative",
              aliases: ["nom"],
              languages: [],
            },
          ],
        },
      ],
    };
    expect(grammarValueLabel(vocabulary, "Kasus", "nom", "de")).toBe("Nominativ");
    expect(grammarValueLabel(vocabulary, "Kasus", "Nominativ", "en")).toBe("nominative");
    expect(grammarValueLabel(vocabulary, "Kasus", "xyz", "en")).toBe("xyz");
    expect(grammarValueLabel(undefined, "Kasus", "nom", "de")).toBe("nom");
  });

  it("builds the Strong's link of an overlay slot from its non-German filters", () => {
    expect(slotGrammarFilter(slot([{ key: "Kasus", values: ["Nominativ"] }]))).toBe(
      "Kasus:Nominativ",
    );
    expect(
      slotGrammarFilter(
        slot([
          { key: "Form", values: ["Perfekt (Qatal)", "Weqatal"], negated: true },
          { key: "Wortart", values: ["Substantiv"], german: true },
        ]),
      ),
    ).toBe("-Form:PerfektQatal,Weqatal");
    expect(slotStrongsLink(slot([{ key: "Kasus", values: ["Nominativ"] }]))).toBe(
      "/bible/strongs/G26?grammar=Kasus%3ANominativ",
    );
    expect(slotStrongsLink(slot([]))).toBe("/bible/strongs/G26");
    expect(slotStrongsLink({ ...slot([]), readAs: "german", lemmas: [] })).toBeNull();
  });
});
