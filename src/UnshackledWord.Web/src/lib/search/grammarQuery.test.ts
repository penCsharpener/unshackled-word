import { describe, expect, it } from "vitest";
import type { SourceWordLemma } from "@/lib/bible/types";
import {
  applySuggestion,
  facetRequest,
  type GrammarVocabulary,
  type GrammarVocabularyValue,
  languageLimit,
  lemmaLookupWord,
  lemmaSuggestions,
  tokenAtCaret,
  valueText,
  vocabularySuggestions,
} from "./grammarQuery";
import { isGrammarQuery, registerGrammarVocabulary } from "./grammarSearch";

const value = (
  labelDe: string,
  aliases: string[] = [],
  languages = ["greek"],
): GrammarVocabularyValue => ({ key: labelDe, labelDe, labelEn: labelDe, aliases, languages });

const vocabulary: GrammarVocabulary = {
  germanPrefix: "dt.",
  germanKeysEnabled: true,
  keys: [
    {
      name: "Tempus",
      aliases: ["Zeit"],
      germanPrefixAllowed: true,
      languages: ["greek"],
      kind: "filter",
      freeValue: false,
      values: [
        value("Präsens"),
        value("Futur"),
        value("Aorist"),
        value("Perfekt"),
        value("Futur II", ["Futur2", "FuturII"]),
      ],
    },
    {
      name: "Kasus",
      aliases: ["Fall"],
      germanPrefixAllowed: false,
      languages: ["greek"],
      kind: "filter",
      freeValue: false,
      values: [
        value("Nominativ"),
        value("Genitiv"),
        value("Dativ"),
        value("Akkusativ"),
        value("Vokativ"),
      ],
    },
    {
      name: "Geschlecht",
      aliases: ["Genus"],
      germanPrefixAllowed: true,
      languages: ["greek", "hebrew", "aramaic"],
      kind: "filter",
      freeValue: false,
      values: [value("maskulin", [], ["greek", "hebrew"]), value("feminin")],
    },
    {
      name: "Stamm",
      aliases: [],
      germanPrefixAllowed: false,
      languages: ["hebrew", "aramaic"],
      kind: "filter",
      freeValue: false,
      values: [value("Qal", [], ["hebrew", "aramaic"])],
    },
    {
      name: "Strong",
      aliases: [],
      germanPrefixAllowed: false,
      languages: ["greek", "hebrew"],
      kind: "word",
      freeValue: true,
      values: [],
    },
    {
      name: "Code",
      aliases: [],
      germanPrefixAllowed: false,
      languages: ["greek", "hebrew"],
      kind: "filter",
      freeValue: true,
      values: [],
    },
  ],
};

const at = (input: string, caret = input.length) => tokenAtCaret(input, caret, vocabulary);
const suggest = (input: string, caret = input.length) => {
  const token = at(input, caret);
  return token ? vocabularySuggestions(input, token, vocabulary).map((s) => s.insert) : [];
};

describe("tokenAtCaret", () => {
  it("finds the token around the caret", () => {
    expect(at("agape Kasus:nom", 3)?.text).toBe("agape");
    expect(at("agape Kasus:nom")?.text).toBe("Kasus:nom");
    expect(at("agape ", 6)).toBeNull();
    expect(at("", 0)).toBeNull();
  });

  it("splits key and value, the value only behind the colon", () => {
    const token = at("agape Kasus:nom");
    expect(token?.key).toBe("Kasus");
    expect(token?.part).toEqual({ start: 12, end: 15, prefix: "nom" });
    expect(at("agape Kasus:nom", 8)?.key).toBeUndefined();
    expect(at("agape Kasus:nom", 8)?.bodyPrefix).toBe("Ka");
  });

  it("reads -, dt. and the comma part under the caret", () => {
    const token = at("-dt.Tempus:aor,pr");
    expect(token?.negated).toBe(true);
    expect(token?.german).toBe(true);
    expect(token?.key).toBe("Tempus");
    expect(token?.part).toEqual({ start: 15, end: 17, prefix: "pr" });
    expect(at("Kasus:nom,akk", 11)?.part).toEqual({ start: 10, end: 13, prefix: "a" });
  });

  it("counts slots like the parser", () => {
    expect(at("agape Kasus:nom")?.slot).toBe(0);
    expect(at("Kasus:nom")?.slot).toBe(0);
    expect(at("agape Kasus:nom pistis Kasus:gen")?.slot).toBe(1);
    expect(at("Kasus:nom agape Kasus:gen")?.slot).toBe(1);
    expect(at("Strong:G26 Kasus:gen")?.slot).toBe(0);
    expect(at("agape Strong:G4102 Kasus:gen")?.slot).toBe(1);
  });
});

describe("vocabularySuggestions", () => {
  it("offers values after Key:, exact match first", () => {
    expect(suggest("agape Kasus:nom")).toEqual(["Nominativ"]);
    expect(suggest("Kasus:")).toEqual(["Nominativ", "Genitiv", "Dativ", "Akkusativ", "Vokativ"]);
    expect(suggest("Tempus:futur")).toEqual(["Futur", "Futur2"]);
    expect(suggest("Zeit:futurii")).toEqual(["Futur2"]);
    expect(suggest("Geschlecht:mas")).toEqual(["maskulin"]);
  });

  it("suggests for the part after the last comma", () => {
    expect(suggest("Kasus:nom,ak")).toEqual(["Akkusativ"]);
  });

  it("is empty when the only value is already typed", () => {
    expect(suggest("Kasus:Nominativ")).toEqual([]);
    expect(suggest("Kasus:nominativ")).toEqual([]);
  });

  it("offers keys and keyed values for a bare prefix", () => {
    expect(suggest("aor")).toEqual(["Tempus:Aorist"]);
    expect(suggest("agape mas")).toEqual(["Geschlecht:maskulin"]);
    expect(suggest("Kas")).toEqual(["Kasus:"]);
    expect(suggest("gen")).toEqual(["Geschlecht:", "Kasus:Genitiv"]);
    expect(suggest("ma")).toEqual([]);
  });

  it("keeps - and dt. and limits dt. to the keys that allow it", () => {
    expect(suggest("-aor")).toEqual(["-Tempus:Aorist"]);
    expect(suggest("dt.Ka")).toEqual([]);
    expect(suggest("dt.Tem")).toEqual(["dt.Tempus:"]);
    expect(suggest("-Str")).toEqual([]);
  });

  it("completes a key in front of a typed colon", () => {
    expect(suggest("Kas:nom", 3)).toEqual(["Kasus"]);
  });

  it("offers no values for a free value key", () => {
    expect(suggest("Code:V")).toEqual([]);
  });
});

describe("valueText", () => {
  it("uses a space-less alias for a label with spaces", () => {
    expect(valueText(value("Futur II", ["Futur2", "FuturII"]))).toBe("Futur2");
    expect(valueText(value("Medium oder Passiv"))).toBe("MediumoderPassiv");
    expect(valueText(value("Aorist"))).toBe("Aorist");
  });
});

describe("applySuggestion", () => {
  it("replaces the range and puts the caret behind it", () => {
    const input = "agape Geschlecht:mas Kasus:nom";
    const token = at(input, 20);
    if (!token) throw new Error("no token");
    const [first] = vocabularySuggestions(input, token, vocabulary);
    if (!first) throw new Error("no suggestion");
    expect(applySuggestion(input, first)).toEqual({
      value: "agape Geschlecht:maskulin Kasus:nom",
      caret: 25,
    });
  });
});

const lemma = (strongs: string, transliteration: string, occurrences: number): SourceWordLemma => ({
  strongs,
  language: "greek",
  lemma: strongs,
  transliteration,
  gloss: "",
  occurrences,
  matchedBy: "translit",
  forms: [],
});

describe("lemma suggestions", () => {
  it("looks up plain words of 3+ letters only", () => {
    expect(lemmaLookupWord(at("agap"))).toBe("agap");
    expect(lemmaLookupWord(at("ag"))).toBe("");
    expect(lemmaLookupWord(at("G26"))).toBe("");
    expect(lemmaLookupWord(at("Kasus:nom"))).toBe("");
    expect(lemmaLookupWord(at("-agap"))).toBe("");
  });

  it("puts the shortest completion first and inserts the Strong's number", () => {
    const token = at("agap");
    if (!token) throw new Error("no token");
    const suggestions = lemmaSuggestions(token, [
      lemma("G25", "agapaō", 143),
      lemma("G26", "agapē", 116),
      lemma("G6113", "agapēsis", 0),
      { ...lemma("H3820", "lev", 595), matchedBy: "skeleton" },
    ]);
    expect(suggestions.map((s) => s.insert)).toEqual(["G26", "G25"]);
  });
});

describe("facetRequest", () => {
  it("empties the value of the token and names slot and key", () => {
    expect(
      facetRequest("agape Fall:nom pistis", at("agape Fall:nom pistis", 14), vocabulary),
    ).toEqual({
      q: "agape Fall: pistis",
      slot: 0,
      key: "Kasus",
    });
    expect(facetRequest("agape Kasus:", at("agape Kasus:"), vocabulary)?.q).toBe("agape Kasus:");
  });

  it("is null outside a value or for free values", () => {
    expect(facetRequest("agape", at("agape"), vocabulary)).toBeNull();
    expect(facetRequest("Code:V", at("Code:V"), vocabulary)).toBeNull();
    expect(facetRequest("Kasus:nom", at("Kasus:nom", 3), vocabulary)).toBeNull();
  });
});

describe("languageLimit", () => {
  it("names a single language family", () => {
    expect(languageLimit(["greek"])).toBe("greek");
    expect(languageLimit(["hebrew", "aramaic"])).toBe("hebrew");
    expect(languageLimit(["aramaic"])).toBe("aramaic");
    expect(languageLimit(["greek", "hebrew", "aramaic"])).toBeNull();
  });
});

describe("isGrammarQuery with the vocabulary", () => {
  it("uses the registered keys", () => {
    expect(isGrammarQuery("bara Stamm:qal")).toBe(true);
    registerGrammarVocabulary({
      ...vocabulary,
      keys: [{ ...vocabulary.keys[0], name: "Neu", aliases: [] } as GrammarVocabulary["keys"][0]],
    });
    expect(isGrammarQuery("x Neu:a")).toBe(true);
    expect(isGrammarQuery("agape Kasus:nom")).toBe(false);
    registerGrammarVocabulary(vocabulary);
    expect(isGrammarQuery("agape Kasus:nom")).toBe(true);
  });
});
