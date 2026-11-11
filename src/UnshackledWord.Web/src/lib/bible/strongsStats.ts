import { queryOptions, useQuery } from "@tanstack/react-query";
import { bibleApi } from "./api";
import type {
  GetStrongsSummaryResponse,
  OriginalLanguage,
  StrongsBookStat,
  StrongsRendering,
  Testament,
  WordStatsTestament,
} from "./types";

export const strongsStatsKeys = {
  summary: (strongs: string) => ["bible", "strongsSummary", strongs] as const,
  wordStats: (word: string, testament: WordStatsTestament) =>
    ["bible", "wordStats", word.toLowerCase(), testament] as const,
};

/** The API accepts a single German word only (1-50 letters, apostrophe or hyphen). */
const STATS_WORD = /^[\p{L}'-]{1,50}$/u;
export const isStatsWord = (word: string): boolean => STATS_WORD.test(word);

export const strongsSummaryQuery = (strongs: string) =>
  queryOptions({
    queryKey: strongsStatsKeys.summary(strongs),
    queryFn: ({ signal }) => bibleApi.strongsSummary(strongs, signal),
    staleTime: Number.POSITIVE_INFINITY,
  });

export const wordStatsQuery = (word: string, testament: WordStatsTestament) =>
  queryOptions({
    queryKey: strongsStatsKeys.wordStats(word, testament),
    queryFn: ({ signal }) => bibleApi.wordStats(word, testament, signal),
    staleTime: Number.POSITIVE_INFINITY,
  });

/** Totals, occurrences per book and all German renderings of a (normalized) Strong's number. */
export function useStrongsSummary(strongs: string | undefined) {
  return useQuery({
    ...strongsSummaryQuery(strongs ?? ""),
    enabled: strongs !== undefined,
  });
}

/** Strong's numbers behind one German word in one testament; stays idle without a valid word. */
export function useWordStats(word: string | undefined, testament: WordStatsTestament) {
  return useQuery({
    ...wordStatsQuery(word ?? "", testament),
    enabled: word !== undefined && isStatsWord(word),
  });
}

/** The most used German renderings (at least half as frequent as the top one, at most 3), for the header gloss. */
export function germanGloss(renderings: StrongsRendering[]): string[] {
  const top = renderings[0];
  if (!top) return [];
  return renderings
    .filter((r) => r.count * 2 >= top.count)
    .slice(0, 3)
    .map((r) => r.word);
}

/** The testaments to chart: that of the original language, plus the other one if the word also occurs there. */
export function statsTestaments(
  language: OriginalLanguage,
  byBook: StrongsBookStat[],
): Testament[] {
  const home: Testament = language === "greek" ? "nt" : "ot";
  const strayed = byBook.some((b) => (b.bibleBookId <= 39 ? "ot" : "nt") !== home);
  return strayed ? ["ot", "nt"] : [home];
}

/** Whether a Strong's number from `wordStats` is the one shown (a lettered number matches exactly, a bare one matches its variants). */
export function isShownStrongs(candidate: string, summary: GetStrongsSummaryResponse): boolean {
  if (candidate === summary.strongs) return true;
  return (
    /^[GH]\d+$/.test(summary.strongs) &&
    candidate.startsWith(summary.strongs) &&
    /^[A-Z]*$/.test(candidate.slice(summary.strongs.length))
  );
}

/** `lang` and `dir` attributes for text in the original language. */
export function originalTextAttributes(language: OriginalLanguage): {
  lang: string;
  dir: "ltr" | "rtl";
} {
  if (language === "greek") return { lang: "el", dir: "ltr" };
  return { lang: language === "aramaic" ? "arc" : "he", dir: "rtl" };
}
