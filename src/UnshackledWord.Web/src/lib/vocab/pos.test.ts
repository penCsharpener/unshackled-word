import i18n from "i18next";
import { beforeAll, describe, expect, it } from "vitest";
import appI18n from "@/i18n";
import { formatPos } from "./pos";

const t = i18n.t.bind(i18n);

describe("formatPos", () => {
  beforeAll(async () => {
    await appI18n.changeLanguage("de");
  });

  it.each([
    ["G:V", "Verb"],
    ["G:T", "Artikel"],
    ["G:N-F", "Nomen (feminin)"],
    ["H:N-M", "Nomen (maskulin)"],
    ["N:N-M-P", "Eigenname (maskulin, Person)"],
    ["N:N--LG", "Eigenname (Ort, Herkunftsbezeichnung)"],
    ["A:V", "Verb (aramäisch)"],
    ["G:ADV-N", "Adverb (verneinend)"],
    ["G:A / G:ADV", "Adjektiv / Adverb"],
    ["G:PRT-N+G:CONJ", "Partikel (verneinend) + Konjunktion"],
    ["G:N-M/F", "Nomen (maskulin/feminin)"],
    ["Ss3m", "Pronominalsuffix"],
    ["Unbekannt", "Unbekannt"],
  ])("spells %s as %s", (code, label) => {
    expect(formatPos(code, t)).toBe(label);
  });

  it("returns null without a code", () => {
    expect(formatPos(null, t)).toBeNull();
    expect(formatPos("", t)).toBeNull();
  });
});
