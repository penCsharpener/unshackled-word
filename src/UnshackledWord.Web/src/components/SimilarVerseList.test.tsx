import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { bibleApi } from "@/lib/bible/api";
import type { BibleStructureBook, SimilarVerseHit, VerseSimilarVerses } from "@/lib/bible/types";
import { markRuns, relativeScore, SimilarVerseList } from "./SimilarVerseList";

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
  targetPositions: [],
  sourcePositions: [],
  ...extra,
});

const source = (hits: SimilarVerseHit[]): VerseSimilarVerses => ({
  hebRefId: 58_004_012,
  bibleBookId: 58,
  chapter: 4,
  verse: 12,
  hits,
});

function renderList(
  verse: VerseSimilarVerses,
  limit?: number,
  onHoverHit?: (hit: SimilarVerseHit | undefined) => void,
) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <SimilarVerseList source={verse} books={books} limit={limit} onHoverHit={onHoverHit} />
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
  it("marks the words at the target positions, consecutive ones as one mark", () => {
    renderList(
      source([
        hit(16, 1, {
          text: "Der wohnt nicht in Tempeln, die mit Händen gemacht sind,",
          targetPositions: [6, 7, 8, 9, 10],
        }),
      ]),
    );
    const marks = document.querySelectorAll("mark");
    expect(marks).toHaveLength(1);
    expect(marks[0]).toHaveTextContent("die mit Händen gemacht sind,");
    expect(marks[0]).toHaveClass("bg-mark-linked");
    expect(screen.getByTestId("similar-hit")).toHaveTextContent(
      "Der wohnt nicht in Tempeln, die mit Händen gemacht sind,",
    );
  });

  it("marks separate words separately and ignores positions beyond the text", () => {
    renderList(source([hit(16, 1, { text: "a b c d", targetPositions: [1, 3, 9] })]));
    const marks = [...document.querySelectorAll("mark")].map((m) => m.textContent);
    expect(marks).toEqual(["a", "c"]);
  });

  it("shows the plain text without a mark when there are no positions", () => {
    renderList(source([hit(16, 1)]));
    expect(document.querySelector("mark")).toBeNull();
    expect(screen.getByText("Text von Offb 1,16")).toBeInTheDocument();
  });

  it("reports the hovered or focused hit and clears it again", async () => {
    const user = userEvent.setup();
    const onHoverHit = vi.fn();
    const first = hit(16, 1, { sourcePositions: [4, 7] });
    renderList(source([first, hit(17, 0.5)]), undefined, onHoverHit);
    const rows = screen.getAllByTestId("similar-hit");

    const row = rows[0] as HTMLElement;
    await user.hover(row);
    expect(onHoverHit).toHaveBeenLastCalledWith(first);
    await user.unhover(row);
    expect(onHoverHit).toHaveBeenLastCalledWith(undefined);

    await user.tab();
    expect(onHoverHit).toHaveBeenLastCalledWith(first);
    await user.tab();
    await user.tab();
    expect(onHoverHit).toHaveBeenCalledWith(undefined);
  });

  it("markRuns splits by position", () => {
    expect(markRuns("a b c", [2])).toEqual([
      { start: 1, text: "a", marked: false },
      { start: 2, text: "b", marked: true },
      { start: 3, text: "c", marked: false },
    ]);
    expect(markRuns("a b", [])).toEqual([{ start: 1, text: "a b", marked: false }]);
  });

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
