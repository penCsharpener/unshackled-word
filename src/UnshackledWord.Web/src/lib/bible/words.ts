import type {
  ChapterWord,
  GetWordDetailResponse,
  HighlightKind,
  VerseWords,
  WordToken,
} from "./types";

/** Letters and combining marks only, so "λόγος," and "λόγος" count as the same form. */
const NON_LETTERS = /[^\p{L}\p{M}]/gu;

const normalizeForm = (word: ChapterWord): string =>
  (word.original ?? "").replace(NON_LETTERS, "").normalize("NFC").toLowerCase();

/**
 * Groups API rows into verses of words. The API returns one row per original-language reading, so
 * rows with the same word id are merged into one token with several senses. A sense's key is the
 * Strong's number, falling back to the lemma (the Old Testament has no lemma, the New Testament both).
 */
export function groupWordsByVerse(words: ChapterWord[]): VerseWords[] {
  const tokens = new Map<number, { verse: number; position: number; token: WordToken }>();
  for (const row of words) {
    let entry = tokens.get(row.id);
    if (!entry) {
      entry = {
        verse: row.verse,
        position: row.positionInVerse,
        token: { id: row.id, text: row.wordInContext, senses: [] },
      };
      tokens.set(row.id, entry);
    }
    const key = row.strongs ?? row.lemma;
    if (!key) continue;
    const form = normalizeForm(row);
    if (!entry.token.senses.some((s) => s.key === key && s.form === form)) {
      entry.token.senses.push({ key, form });
    }
  }

  const verses = new Map<number, { position: number; token: WordToken }[]>();
  for (const { verse, position, token } of tokens.values()) {
    const list = verses.get(verse) ?? [];
    list.push({ position, token });
    verses.set(verse, list);
  }
  return [...verses.entries()]
    .sort(([a], [b]) => a - b)
    .map(([verse, list]) => ({
      verse,
      words: list.sort((a, b) => a.position - b.position).map((e) => e.token),
    }));
}

/**
 * Which words to highlight while `hovered` is active: every word sharing a key with it. A shared key
 * with an identical original form is `same`, otherwise `related`. The hovered word itself is `same`.
 */
export function buildHighlights(
  hovered: WordToken | undefined,
  verses: VerseWords[],
): Map<number, HighlightKind> {
  const result = new Map<number, HighlightKind>();
  if (!hovered || hovered.senses.length === 0) return result;

  for (const { words } of verses) {
    for (const word of words) {
      let kind: HighlightKind | undefined;
      for (const sense of word.senses) {
        for (const own of hovered.senses) {
          if (own.key !== sense.key) continue;
          if (own.form !== "" && own.form === sense.form) kind = "same";
          else kind ??= "related";
        }
      }
      if (word.id === hovered.id) kind = "same";
      if (kind) result.set(word.id, kind);
    }
  }
  return result;
}

/** Ids of the German words translated together with the open word (its phrase), including the word itself. */
export function phraseWordIds(detail: GetWordDetailResponse | undefined): Set<number> {
  const ids = new Set<number>();
  if (!detail?.coMappedWords?.length) return ids;
  ids.add(detail.wordId);
  for (const word of detail.coMappedWords) ids.add(word.wordId);
  return ids;
}
