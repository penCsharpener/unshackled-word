import { beforeEach, describe, expect, it } from "vitest";
import i18n from "@/i18n";
import { formatMorphology } from "./morphology";
import type { Morphology } from "./types";

const cases: [Morphology, string, string][] = [
  [
    {
      code: "N-NSM",
      partOfSpeech: "noun",
      case: "nominative",
      number: "singular",
      gender: "masculine",
    },
    "Substantiv · Nominativ · Singular · maskulin",
    "noun · nominative · singular · masculine",
  ],
  [
    {
      code: "V-IAI-3S",
      partOfSpeech: "verb",
      tense: "imperfect",
      voice: "active",
      mood: "indicative",
      person: "third",
      number: "singular",
    },
    "Verb · Imperfekt · Aktiv · Indikativ · 3. Person · Singular",
    "verb · imperfect · active · indicative · 3rd person · singular",
  ],
  [
    {
      code: "HNcfsa",
      partOfSpeech: "noun",
      gender: "feminine",
      number: "singular",
      state: "absolute",
    },
    "Substantiv · Singular · feminin · Status absolutus",
    "noun · singular · feminine · absolute",
  ],
  [{ code: "XYZ", description: "Unknown form" }, "Unknown form", "Unknown form"],
  [{ code: "XYZ" }, "XYZ", "XYZ"],
];

describe("formatMorphology", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it.each(cases)("formats %j in German", (morphology, de) => {
    expect(formatMorphology(morphology, i18n.t)).toBe(de);
  });

  it.each(cases)("formats %j in English", async (morphology, _de, en) => {
    await i18n.changeLanguage("en");
    expect(formatMorphology(morphology, i18n.t)).toBe(en);
  });
});
