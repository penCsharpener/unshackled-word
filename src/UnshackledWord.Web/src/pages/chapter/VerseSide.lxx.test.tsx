import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import i18n from "@/i18n";
import { LXX_FORM_KEY, useLxxForm } from "@/lib/bible/lxx";
import type { BibleStructureBook, LxxElbVerse, LxxUnit } from "@/lib/bible/types";
import { SidePanelRail } from "./SidePanelRail";
import { VerseSide } from "./VerseSide";

const books = [
  { id: 17, nameDe: "Ester", nameEn: "Esther", abbreviationDe: "Est", abbreviationEn: "Esth" },
  { id: 24, nameDe: "Jeremia", nameEn: "Jeremiah", abbreviationDe: "Jer", abbreviationEn: "Jer" },
] as BibleStructureBook[];

const NO_IDS: ReadonlySet<number> = new Set();

const unit = (lxxVerseId: number, words: string[], extra: Partial<LxxUnit> = {}): LxxUnit => ({
  lxxVerseId,
  bibleBookId: 24,
  chapter: 25,
  verse: 1,
  subverse: 0,
  ref: "Jer.25:1",
  textForm: "",
  mapKind: "one",
  isAddition: false,
  differsFromHebrew: false,
  words: words.map((greek, i) => ({ id: lxxVerseId * 100 + i, position: i + 1, greek })),
  ...extra,
});

function renderVerse(lxx: LxxElbVerse | undefined, lxxForm?: "og" | "th") {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <VerseSide
          mode="lxx"
          verse={lxx?.verse ?? 14}
          bookId={24}
          chapter={25}
          level={200}
          reference="Jeremia 25,15"
          books={books}
          language="hebrew"
          linkedIds={NO_IDS}
          onHoverOriginal={() => {}}
          lxx={lxx}
          lxxForm={lxxForm}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => i18n.changeLanguage("de"));
afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("VerseSide, mode lxx", () => {
  it("shows the Rahlfs label only where it differs, formatted per language", async () => {
    const shifted = unit(1, ["Ὁ", "λόγος"], {
      chapter: 32,
      verse: 1,
      ref: "Jer.32:1",
      differsFromHebrew: true,
    });
    renderVerse({ hebRefId: 24_025_015, verse: 15, units: [shifted] });
    expect(screen.getByText("Jer 32,1 LXX")).toBeInTheDocument();
    cleanup();
    await i18n.changeLanguage("en");
    renderVerse({ hebRefId: 24_025_015, verse: 15, units: [shifted] });
    expect(screen.getByText("Jer 32:1 LXX")).toBeInTheDocument();
    cleanup();
    renderVerse({ hebRefId: 24_025_001, verse: 1, units: [unit(2, ["Ὁ"])] });
    expect(screen.queryByText(/LXX$/)).not.toBeInTheDocument();
  });

  it("renders untagged words as plain Greek text without links", () => {
    renderVerse({ hebRefId: 24_025_001, verse: 1, units: [unit(1, ["Ὁ", "λόγος"])] });
    const text = screen.getByText("λόγος").closest("p");
    expect(text).toHaveAttribute("lang", "grc");
    expect(text).toHaveAttribute("dir", "ltr");
    expect(text).toHaveClass("font-original");
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("frames additions before the verse's own text, as Swete prints them", () => {
    const add = (id: number, word: string, subverse: number) =>
      unit(id, [word], {
        bibleBookId: 17,
        chapter: 1,
        verse: 1,
        subverse,
        ref: `Est.1:1.${subverse}`,
        isAddition: true,
        mapKind: "addition",
        differsFromHebrew: true,
      });
    const main = unit(3, ["Καὶ"], { bibleBookId: 17, chapter: 1, verse: 1, ref: "Est.1:1" });
    renderVerse({
      hebRefId: 17_001_001,
      verse: 1,
      units: [add(1, "Ἔτους", 1), add(2, "δευτέρου", 2), main],
    });
    const frames = screen.getAllByRole("region", { name: "Zusatz der LXX" });
    expect(frames).toHaveLength(1);
    expect(within(frames[0] as HTMLElement).getByText("Zusatz der LXX")).toBeInTheDocument();
    expect(frames[0]).toHaveTextContent("Ἔτους δευτέρου");
    // no subverse number for additions; the verse's own text follows the frame
    expect(screen.queryByText(/1,1\.1/)).not.toBeInTheDocument();
    const main3 = screen.getByText("Καὶ");
    expect(frames[0]?.compareDocumentPosition(main3)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(frames[0]).not.toContainElement(main3);
  });

  it("says so when the LXX has no text for the verse", () => {
    renderVerse({ hebRefId: 24_025_014, verse: 14, units: [] });
    expect(screen.getByText("In der LXX nicht vorhanden")).toBeInTheDocument();
    cleanup();
    renderVerse(undefined);
    expect(screen.getByText("In der LXX nicht vorhanden")).toBeInTheDocument();
  });

  it("shows only the chosen Daniel form", () => {
    const verse: LxxElbVerse = {
      hebRefId: 27_003_001,
      verse: 1,
      units: [
        unit(1, ["ἔτους"], { bibleBookId: 27, textForm: "og" }),
        unit(2, ["Ναβουχοδονοσορ"], { bibleBookId: 27, textForm: "th" }),
      ],
    };
    renderVerse(verse);
    expect(screen.getByText("Ναβουχοδονοσορ")).toBeInTheDocument();
    expect(screen.queryByText("ἔτους")).not.toBeInTheDocument();
    cleanup();
    renderVerse(verse, "og");
    expect(screen.getByText("ἔτους")).toBeInTheDocument();
    expect(screen.queryByText("Ναβουχοδονοσορ")).not.toBeInTheDocument();
  });
});

function DanielRail() {
  const [form, setForm] = useLxxForm();
  return (
    <>
      <SidePanelRail
        mode="lxx"
        onModeChange={() => {}}
        status="idle"
        onRetry={() => {}}
        lxxForm={{ value: form, onChange: setForm }}
      />
      <output data-testid="form">{form}</output>
    </>
  );
}

describe("SidePanelRail, mode lxx", () => {
  it("shows the heading, the source line and an LXX mode button", () => {
    render(<SidePanelRail mode="lxx" onModeChange={() => {}} status="idle" onRetry={() => {}} />);
    expect(screen.getByRole("heading", { name: "Septuaginta (Swete)" })).toBeInTheDocument();
    expect(
      screen.getByText("Griechischer Text nach Swete, Verszählung nach Rahlfs"),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Septuaginta (Swete) zeigen (G)" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    // no Daniel toggle outside Daniel
    expect(screen.queryByRole("button", { name: "Theodotion" })).not.toBeInTheDocument();
  });

  it("has no LXX button in New Testament chapters", () => {
    render(
      <SidePanelRail
        mode="original"
        onModeChange={() => {}}
        status="idle"
        onRetry={() => {}}
        lxxAvailable={false}
      />,
    );
    expect(screen.getByRole("button", { name: "Grundtext zeigen (G)" })).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Septuaginta (Swete) zeigen (G)" }),
    ).not.toBeInTheDocument();
  });

  it("toggles Daniel's text form, Theodotion first, and keeps it across a reload", async () => {
    const user = userEvent.setup();
    const { unmount } = render(<DanielRail />);
    expect(screen.getByRole("button", { name: "Theodotion" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    await user.click(screen.getByRole("button", { name: "Old Greek" }));
    expect(screen.getByTestId("form")).toHaveTextContent("og");
    expect(localStorage.getItem(LXX_FORM_KEY)).toBe("og");
    unmount();
    render(<DanielRail />);
    expect(screen.getByRole("button", { name: "Old Greek" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });
});
