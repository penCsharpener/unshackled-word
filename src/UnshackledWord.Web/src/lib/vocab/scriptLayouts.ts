// Port of Lexio roadmap 0049 (docs/Roadmaps/0049 The script keyboard.md): the Greek and Hebrew layouts of
// the on-screen keyboard and the physical key mapping (Latin key -> letter). No diacritics are needed: the
// typing card compares letters only.

export type ScriptKeyboardLayout = "greek" | "hebrew";

/** Lowercase QWERTY key to the letter it types. */
type PhysicalMap = Record<string, string>;

// The classic transliteration table: q -> θ, c -> ξ, y -> ψ, w -> ω, h -> η; the spare j is the final sigma.
const GREEK_PHYSICAL: PhysicalMap = {
  a: "α",
  b: "β",
  g: "γ",
  d: "δ",
  e: "ε",
  z: "ζ",
  h: "η",
  q: "θ",
  i: "ι",
  k: "κ",
  l: "λ",
  m: "μ",
  n: "ν",
  c: "ξ",
  o: "ο",
  p: "π",
  r: "ρ",
  s: "σ",
  j: "ς",
  t: "τ",
  u: "υ",
  f: "φ",
  x: "χ",
  y: "ψ",
  w: "ω",
};

// Hebrew has no case. The five final forms get their own keys (e, f, i, u and `;`).
const HEBREW_PHYSICAL: PhysicalMap = {
  a: "א",
  b: "ב",
  g: "ג",
  d: "ד",
  h: "ה",
  v: "ו",
  z: "ז",
  x: "ח",
  j: "ט",
  y: "י",
  k: "כ",
  l: "ל",
  m: "מ",
  n: "נ",
  s: "ס",
  o: "ע",
  p: "פ",
  c: "צ",
  q: "ק",
  r: "ר",
  w: "ש",
  t: "ת",
  e: "ך",
  f: "ם",
  i: "ן",
  u: "ף",
  ";": "ץ",
};

const PHYSICAL: Record<ScriptKeyboardLayout, PhysicalMap> = {
  greek: GREEK_PHYSICAL,
  hebrew: HEBREW_PHYSICAL,
};

const QWERTY_ROWS: readonly (readonly string[])[] = [
  ["q", "w", "e", "r", "t", "y", "u", "i", "o", "p"],
  ["a", "s", "d", "f", "g", "h", "j", "k", "l"],
  ["z", "x", "c", "v", "b", "n", "m"],
];
const PUNCT_ROW: readonly string[] = [";"];

/** The backspace key of the panel. */
export const BACKSPACE_KEY = "⌫";

/**
 * Rows as the panel draws them: the letters sit in physical QWERTY position, so the panel doubles as a
 * legend for what each physical key types. Keys the table does not cover (Greek `v`) are left out. The
 * last entry of the first row is the backspace key.
 */
export function scriptRows(layout: ScriptKeyboardLayout): string[][] {
  const map = PHYSICAL[layout];
  const letters = (keys: readonly string[]) =>
    keys.flatMap((key) => {
      const letter = map[key];
      return letter === undefined ? [] : [letter];
    });
  const rows = QWERTY_ROWS.map(letters);
  const punct = letters(PUNCT_ROW);
  rows[0]?.push(BACKSPACE_KEY);
  if (punct.length > 0) rows.push(punct);
  return rows;
}

/**
 * Maps the `key` of a keydown through the layout's physical table, or returns `undefined` when the key
 * passes through untouched (anything but one lowercase or uppercase ASCII letter or a covered punctuation key).
 */
export function remapPhysicalKey(layout: ScriptKeyboardLayout, key: string): string | undefined {
  if (key.length !== 1 || !/^[\x20-\x7E]$/.test(key)) return undefined;
  const mapped = PHYSICAL[layout][key.toLowerCase()];
  if (mapped === undefined) return undefined;
  if (key === key.toLowerCase()) return mapped;
  const upper = mapped.toUpperCase();
  return [...upper].length === 1 ? upper : undefined;
}
