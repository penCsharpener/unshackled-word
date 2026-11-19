import { describe, expect, it } from "vitest";
import { isSourceWord, splitSourceWords } from "./sourceWords";

describe("isSourceWord", () => {
  it.each(["λόγος", "θεός", "מלך", "דָּבָר", "G1577", "h430", "H0430", "G2424a"])(
    "accepts %s",
    (word) => expect(isSourceWord(word)).toBe(true),
  );
  it.each(["Gott", "ging", "Joh", "3,16", "G", "H12345", "gehen2"])("rejects %s", (word) =>
    expect(isSourceWord(word)).toBe(false),
  );
});

describe("splitSourceWords", () => {
  it("separates script and Strong's words from the German rest, keeping the order", () => {
    expect(splitSourceWords("Gott θεός G3056 liebt")).toEqual({
      source: "θεός G3056",
      german: "Gott liebt",
    });
  });
  it("leaves German queries alone and handles empty input", () => {
    expect(splitSourceWords("im Anfang")).toEqual({ source: "", german: "im Anfang" });
    expect(splitSourceWords("")).toEqual({ source: "", german: "" });
  });
  it("sends at most five source words", () => {
    expect(splitSourceWords("G1 G2 G3 G4 G5 G6").source).toBe("G1 G2 G3 G4 G5");
  });
});
