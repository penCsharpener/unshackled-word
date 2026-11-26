import { beforeAll, describe, expect, it } from "vitest";
import i18n from "@/i18n";
import { ApiError } from "@/lib/api";
import {
  formatGrammarFilter,
  formatGrammarWords,
  germanMatchText,
  germanReading,
  isGrammarQuery,
  languageOfOriginal,
  parseGrammarProblem,
  replaceGrammarToken,
  toVerseHit,
} from "./grammarSearch";

const t = i18n.t.bind(i18n);

describe("isGrammarQuery", () => {
  it.each([
    "agape Kasus:nom",
    "kasus:NOM",
    "Tempus:p",
    "-Modus:partizip",
    "dt.Numerus:plural",
    "Liebe DT.Geschlecht:fem",
    "Strong:G26",
    "lemma:agape",
    "Kasus:",
    "Wa:verb",
    "Diathese:pass",
  ])("accepts %s", (q) => expect(isGrammarQuery(q)).toBe(true));

  it.each(["agape", "Gott Welt geliebt", "Joh 3:16", "Vers:3", ":nom", "Kasus nom", ""])(
    "rejects %s",
    (q) => expect(isGrammarQuery(q)).toBe(false),
  );
});

describe("parseGrammarProblem", () => {
  it("reads code, token and candidates of a 400", () => {
    const error = new ApiError(
      400,
      "bad",
      JSON.stringify({
        status: 400,
        title: "The grammar query cannot be searched.",
        code: "ambiguousValue",
        token: "Tempus:p",
        candidates: ["Präsens", "Perfekt", "Plusquamperfekt"],
      }),
    );
    expect(parseGrammarProblem(error)).toEqual({
      code: "ambiguousValue",
      token: "Tempus:p",
      candidates: ["Präsens", "Perfekt", "Plusquamperfekt"],
    });
  });

  it("omits what the server omitted", () => {
    const error = new ApiError(400, "bad", JSON.stringify({ code: "emptyQuery" }));
    expect(parseGrammarProblem(error)).toEqual({ code: "emptyQuery" });
  });

  it("returns null for other errors and unreadable bodies", () => {
    expect(parseGrammarProblem(new ApiError(500, "x", JSON.stringify({ code: "x" })))).toBeNull();
    expect(parseGrammarProblem(new ApiError(400, "x", "not json"))).toBeNull();
    expect(parseGrammarProblem(new ApiError(400, "x", JSON.stringify({ errors: {} })))).toBeNull();
    expect(parseGrammarProblem(new Error("x"))).toBeNull();
  });
});

describe("replaceGrammarToken", () => {
  it("replaces the value of an ambiguous filter", () => {
    expect(
      replaceGrammarToken(
        "agape  Tempus:p",
        { code: "ambiguousValue", token: "Tempus:p" },
        "Perfekt",
      ),
    ).toBe("agape Tempus:Perfekt");
  });

  it("replaces the key of an unknown key and keeps prefix and value", () => {
    expect(
      replaceGrammarToken("-dt.Kasu:nom", { code: "unknownKey", token: "-dt.Kasu:nom" }, "Kasus"),
    ).toBe("-dt.Kasus:nom");
  });

  it("replaces a whole word and drops whitespace in a candidate", () => {
    expect(
      replaceGrammarToken("agapee Kasus:nom", { code: "unknownWord", token: "agapee" }, "agape"),
    ).toBe("agape Kasus:nom");
    expect(
      replaceGrammarToken(
        "Tempus:fut",
        { code: "ambiguousValue", token: "Tempus:fut" },
        "Futur II",
      ),
    ).toBe("Tempus:FuturII");
  });

  it("keeps the input without a matching token", () => {
    expect(replaceGrammarToken("agape", { code: "emptyQuery" }, "x")).toBe("agape");
  });
});

describe("grammar in words", () => {
  beforeAll(async () => {
    await i18n.changeLanguage("de");
  });

  it("formats part of speech, then the other parts", () => {
    expect(
      formatGrammarWords(
        { partOfSpeech: "noun", case: "nominative", number: "singular", gender: "feminine" },
        t,
      ),
    ).toBe("Substantiv, Nominativ Singular feminin");
    expect(
      formatGrammarWords(
        {
          partOfSpeech: "verb",
          tense: "aorist",
          voice: "active",
          mood: "indicative",
          person: "third",
          number: "singular",
        },
        t,
      ),
    ).toBe("Verb, Aorist Aktiv Indikativ 3. Person Singular");
  });

  it("formats Hebrew with stem, form and state", () => {
    expect(
      formatGrammarWords(
        {
          partOfSpeech: "verb",
          stem: "qal",
          hebrewForm: "perfect",
          person: "third",
          number: "singular",
          gender: "masculine",
        },
        t,
      ),
    ).toBe("Verb, Qal, Perfekt (Qatal) 3. Person Singular maskulin");
    expect(
      formatGrammarWords({ partOfSpeech: "noun", number: "singular", state: "construct" }, t),
    ).toBe("Substantiv, Singular, Status constructus");
  });

  it("takes the language from the server, else from the Strong's prefix", () => {
    expect(languageOfOriginal({ language: "aramaic", strongs: "H1836" })).toBe("aramaic");
    expect(languageOfOriginal({ strongs: "H1254" })).toBe("hebrew");
    expect(languageOfOriginal({ strongs: "G26" })).toBe("greek");
  });

  it("falls back to the code", () => {
    expect(formatGrammarWords({}, t, "CONJ")).toBe("CONJ");
  });

  it("formats filters with negation, alternatives and the German prefix", () => {
    expect(
      formatGrammarFilter(
        { key: "Kasus", values: ["Nominativ"], negated: false, german: false },
        t,
      ),
    ).toBe("Nominativ");
    expect(
      formatGrammarFilter({ key: "Modus", values: ["Partizip"], negated: true, german: false }, t),
    ).toBe("nicht Partizip");
    expect(
      formatGrammarFilter(
        { key: "Kasus", values: ["Nominativ", "Akkusativ"], negated: false, german: true },
        t,
      ),
    ).toBe("dt. Nominativ oder Akkusativ");
    expect(
      formatGrammarFilter({ key: "Code", values: ["V-2A*"], negated: false, german: false }, t),
    ).toBe("Code V-2A*");
  });
});

describe("toVerseHit", () => {
  it("marks the German positions of all matches", () => {
    const hit = toVerseHit({
      bibleBookId: 40,
      chapter: 24,
      verse: 12,
      words: [
        { text: "die", position: 1 },
        { text: "Liebe", position: 2 },
      ],
      matches: [
        {
          slot: 0,
          germanPositions: [2],
          original: { form: "ἀγάπη", lemma: "ἀγάπη", strongs: "G26", code: "N-NSF", keys: {} },
        },
      ],
    });
    expect(hit.tokens).toEqual([
      { text: "die", matched: false },
      { text: "Liebe", matched: true },
    ]);
  });
});

describe("German side (roadmap 0029.7f)", () => {
  it("reads the German word as typed, also after dt.Wort:", () => {
    expect(germanReading({ text: "Liebe dt.Numerus:plural", germanLemma: "liebe" })).toBe("Liebe");
    expect(germanReading({ text: "dt.Wort:Sohn dt.Numerus:plural", germanLemma: "sohn" })).toBe(
      "Sohn",
    );
    expect(germanReading({ text: "dt.Numerus:plural", germanLemma: "liebe" })).toBe("liebe");
    expect(germanReading({ text: "Liebe" })).toBe("Liebe");
  });

  it("names the German words of a German-only match", () => {
    const hit = {
      words: [
        { text: "Gott", position: 1 },
        { text: "hat", position: 2 },
        { text: "geliebt", position: 3 },
      ],
    };
    expect(germanMatchText(hit, { slot: 0, germanPositions: [3, 1] })).toBe("Gott geliebt");
  });
});

describe("keyNotInLanguage (roadmap 0029 review)", () => {
  it("replaces the key, not the value", () => {
    const problem = { code: "keyNotInLanguage", token: "Kasus:nom", candidates: ["Stamm"] };
    expect(replaceGrammarToken("bara Kasus:nom", problem, "Stamm")).toBe("bara Stamm:nom");
    expect(
      replaceGrammarToken("bara -Kasus:nom", { ...problem, token: "-Kasus:nom" }, "Stamm"),
    ).toBe("bara -Stamm:nom");
  });
});
