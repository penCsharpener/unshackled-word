import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { lxxOriginalWords } from "@/lib/bible/lxx";
import { formatMorphology } from "@/lib/bible/morphology";
import type { BibleStructureBook, LxxUnit, Morphology } from "@/lib/bible/types";
import { OriginalVerse } from "./OriginalVerse";

const books = [
  { id: 1, nameDe: "1. Mose", nameEn: "Genesis", abbreviationDe: "Gen", abbreviationEn: "Gen" },
] as BibleStructureBook[];

const NDSF: Morphology = {
  code: "N-DSF",
  partOfSpeech: "noun",
  case: "dative",
  number: "singular",
  gender: "feminine",
};

const unit: LxxUnit = {
  lxxVerseId: 1,
  bibleBookId: 1,
  chapter: 1,
  verse: 1,
  subverse: 0,
  ref: "Gen.1:1",
  textForm: "",
  mapKind: "one",
  isAddition: false,
  differsFromHebrew: false,
  words: [
    {
      id: 486_320,
      position: 1,
      greek: "ΑΡΧΗ",
      tag: {
        lemma: "ἀρχή",
        strongs: "G0746",
        gloss: "beginning",
        morphology: NDSF,
        status: "confirmed",
      },
    },
    { id: 486_321, position: 2, greek: "ἐποίησεν" },
    {
      id: 486_322,
      position: 3,
      greek: "ὁ",
      tag: { lemma: "ὁ", strongs: "G3588", morphology: { code: "T-NSM" }, status: "proposed" },
    },
  ],
};

function renderLxx() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <OriginalVerse
          variant="margin"
          reference="1. Mose 1,1"
          data={{ language: "lxx", originalWords: lxxOriginalWords([unit], books, "de") }}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const card = () => screen.findByTestId("original-word-info");

beforeEach(async () => {
  await i18n.changeLanguage("de");
  vi.stubGlobal(
    "fetch",
    vi.fn(async () =>
      Response.json({
        id: 486_320,
        greek: "ΑΡΧΗ",
        tag: {
          lemma: "ἀρχή",
          strongs: "G0746",
          morphology: NDSF,
          status: "confirmed",
          lexicon: { strongs: "G0746", lemma: "ἀρχή", definition: "Beginning, origin." },
        },
      }),
    ),
  );
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("OriginalVerse language lxx", () => {
  it("links a tagged word to its Strong's page and shows lemma, gloss and parse on hover", async () => {
    const user = userEvent.setup();
    renderLxx();
    const link = screen.getByRole("link", { name: /ΑΡΧΗ, G746/ });
    expect(link).toHaveAttribute("href", "/bible/strongs/G746");
    await user.hover(link);
    const tip = await card();
    expect(tip).toHaveTextContent("ἀρχή");
    expect(tip).toHaveTextContent("G0746");
    expect(tip).toHaveTextContent("beginning");
    // the parse is the NT wording ("Substantiv", not the server's "Nomen")
    const t = i18n.getFixedT("de");
    expect(formatMorphology(NDSF, t)).toBe("Substantiv · Dativ · Singular · feminin");
    expect(tip).toHaveTextContent(formatMorphology(NDSF, t));
    expect(tip).not.toHaveTextContent("Vorschlag");
  });

  it("pins the detail on click with the lexicon text and closes it again", async () => {
    const user = userEvent.setup();
    renderLxx();
    await user.click(screen.getByRole("link", { name: /ΑΡΧΗ, G746/ }));
    const tip = await card();
    expect(await within(tip).findByText("Beginning, origin.")).toBeInTheDocument();
    expect(tip).toHaveTextContent("Gen 1,1");
    await user.click(within(tip).getByRole("button", { name: "Schließen" }));
    expect(screen.queryByTestId("original-word-info")).not.toBeInTheDocument();
  });

  it("shows 'Noch nicht bestimmt' for an untagged word, opened by a button", async () => {
    const user = userEvent.setup();
    renderLxx();
    await user.click(screen.getByRole("button", { name: /ἐποίησεν.*nicht bestimmt/ }));
    const tip = await card();
    expect(tip).toHaveTextContent("ἐποίησεν");
    expect(tip).toHaveTextContent("Noch nicht bestimmt");
    expect(within(tip).queryByRole("link")).not.toBeInTheDocument();
  });

  it("marks a proposed tag with a dotted underline and the badge", async () => {
    const user = userEvent.setup();
    renderLxx();
    const link = screen.getByRole("link", { name: /ὁ, G3588/ });
    expect(link).toHaveClass("decoration-dotted");
    expect(screen.getByRole("link", { name: /ΑΡΧΗ/ })).not.toHaveClass("decoration-dotted");
    await user.hover(link);
    expect(await card()).toHaveTextContent("Vorschlag");
  });

  it("says 'Not yet tagged' in English", async () => {
    await i18n.changeLanguage("en");
    const user = userEvent.setup();
    renderLxx();
    await user.click(screen.getByRole("button", { name: /ἐποίησεν/ }));
    expect(await card()).toHaveTextContent("Not yet tagged");
  });
});
