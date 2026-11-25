import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { bibleApi } from "@/lib/bible/api";
import type { BibleStructureBook, SimilarVerseHit, VerseSimilarVerses } from "@/lib/bible/types";
import { relativeScore, SimilarVerseList } from "./SimilarVerseList";

const books = [
  { id: 58, nameDe: "Hebräer", nameEn: "Hebrews", abbreviationDe: "Hebr", abbreviationEn: "Heb" },
  {
    id: 66,
    nameDe: "Offenbarung",
    nameEn: "Revelation",
    abbreviationDe: "Offb",
    abbreviationEn: "Rev",
  },
] as BibleStructureBook[];

const hit = (
  verse: number,
  score: number,
  extra: Partial<SimilarVerseHit> = {},
): SimilarVerseHit => ({
  hebRefId: 66_001_000 + verse,
  bibleBookId: 66,
  chapter: 1,
  verse,
  text: `Text von Offb 1,${verse}`,
  score,
  rank: 1,
  sharedCount: 0,
  phraseCount: 0,
  shared: [],
  phrases: [],
  ...extra,
});

const source = (hits: SimilarVerseHit[]): VerseSimilarVerses => ({
  hebRefId: 58_004_012,
  bibleBookId: 58,
  chapter: 4,
  verse: 12,
  hits,
});

function renderList(verse: VerseSimilarVerses, limit?: number) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <SimilarVerseList source={verse} books={books} limit={limit} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => i18n.changeLanguage("de"));
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("SimilarVerseList", () => {
  it("links the reference with the full book name and shows the hit text", () => {
    renderList(source([hit(16, 1.2)]));
    const link = screen.getByRole("link", { name: "Offenbarung 1,16" });
    expect(link).toHaveTextContent("Offb 1,16");
    expect(link).toHaveAttribute("href", "/bible/66/1#v16");
    expect(screen.getByText("Text von Offb 1,16")).toHaveClass("line-clamp-2");
  });

  it("lists phrases before single words and leaves words of a phrase out", () => {
    renderList(
      source([
        hit(16, 1, {
          shared: [
            {
              strongs: "G1366",
              lemma: "δίστομος",
              transliteration: "distomos",
              gloss: "zweischneidig",
            },
            { strongs: "G4501", lemma: "ῥομφαία", transliteration: "rhomphaia", gloss: "Schwert" },
            { strongs: "G3588", lemma: "", transliteration: "", gloss: "" },
          ],
          phrases: [{ text: "ῥομφαία δίστομος", strongs: ["G4501", "G1366"] }],
        }),
      ]),
    );
    const chips = within(screen.getByRole("list", { name: "Gemeinsame Grundtextwörter" }))
      .getAllByRole("listitem")
      .map((li) => li.dataset.chip);
    expect(chips).toEqual(["phrase", "word"]);
    const phrase = screen.getByText("ῥομφαία δίστομος");
    expect(phrase).toHaveAttribute("lang", "grc");
    expect(phrase).toHaveAttribute("dir", "ltr");
    const word = screen.getByRole("link", { name: /^G3588/ });
    expect(word).toHaveAttribute("href", "/bible/strongs/G3588");
  });

  it("marks Hebrew chips right to left and links words to the Strong's page", () => {
    renderList({
      ...source([
        hit(4, 1, {
          bibleBookId: 20,
          shared: [
            { strongs: "H2719", lemma: "חֶרֶב", transliteration: "che.rev", gloss: "Schwert" },
          ],
        }),
      ]),
      bibleBookId: 19,
    });
    const link = screen.getByRole("link", { name: /^חֶרֶב \(che\.rev, „Schwert“\), H2719/ });
    expect(link).toHaveAttribute("href", "/bible/strongs/H2719");
    expect(link).toHaveAttribute("title", "che.rev, „Schwert“");
    expect(within(link).getByText("חֶרֶב")).toHaveAttribute("dir", "rtl");
    expect(within(link).getByText("חֶרֶב")).toHaveAttribute("lang", "he");
  });

  it("draws the score relative to the best hit without the raw number", () => {
    renderList(source([hit(16, 2), hit(17, 1.56), hit(18, 0.5)]));
    const meters = screen.getAllByRole("meter");
    expect(meters.map((m) => m.getAttribute("value"))).toEqual(["100", "78", "25"]);
    expect(meters[1]).toHaveAccessibleName("Ähnlichkeit 78 % des besten Treffers");
    expect(screen.queryByText(/1\.56|1,56/)).toBeNull();
    expect(relativeScore(1, 0)).toBe(0);
  });

  it("shows the empty state", () => {
    renderList(source([]));
    expect(
      screen.getByText("Keine ähnlichen Verse (seltene Wörter fehlen oder Berechnung steht aus)"),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Mehr anzeigen" })).toBeNull();
  });

  it("shows the hit text in a hover card", async () => {
    renderList(source([hit(16, 1)]));
    await userEvent.hover(screen.getByRole("link", { name: "Offenbarung 1,16" }));
    const card = screen.getByRole("tooltip");
    expect(card).toHaveTextContent("Offenbarung 1,16");
    expect(card).toHaveTextContent("Text von Offb 1,16");
  });

  it("replaces the list with the verse endpoint on 'Mehr anzeigen'", async () => {
    const spy = vi
      .spyOn(bibleApi, "similarVersesOfVerse")
      .mockResolvedValue(source([hit(16, 2), hit(17, 1), hit(20, 0.5)]));
    renderList(source([hit(16, 2), hit(17, 1)]), 2);
    expect(screen.getAllByTestId("similar-hit")).toHaveLength(2);
    await userEvent.click(screen.getByRole("button", { name: "Mehr anzeigen" }));
    expect(await screen.findByRole("link", { name: "Offenbarung 1,20" })).toBeInTheDocument();
    expect(spy).toHaveBeenCalledWith(58_004_012, expect.anything());
    expect(screen.getAllByTestId("similar-hit")).toHaveLength(3);
    expect(screen.queryByRole("button", { name: "Mehr anzeigen" })).toBeNull();
  });

  it("offers no 'mehr' when the verse has fewer hits than the chapter limit", () => {
    renderList(source([hit(16, 1)]), 10);
    expect(screen.queryByRole("button", { name: "Mehr anzeigen" })).toBeNull();
  });
});
