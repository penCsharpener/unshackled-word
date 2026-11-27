import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { VocabEntryList, type VocabEntryRow } from "./VocabEntryList";

const greek = (over: Partial<VocabEntryRow> = {}): VocabEntryRow => ({
  id: 1,
  rank: 87,
  lemma: "λόγος",
  transliteration: "logos",
  dictionaryForm: "λόγος, -ου, ὁ",
  meanings: ["Wort", "Rede"],
  occurrences: 330,
  hasAudio: false,
  reviewed: true,
  strongsKeys: ["G3056"],
  lang: "grc",
  ...over,
});

const compound = (over: Partial<VocabEntryRow> = {}): VocabEntryRow =>
  greek({
    id: 21,
    rank: 5000,
    lemma: "συνεπιμαρτυρέω",
    transliteration: "synepimartyreō",
    dictionaryForm: null,
    meanings: ["mitbezeugen"],
    occurrences: 1,
    strongsKeys: ["G4901"],
    root: {
      id: 4,
      root: "μαρτυρέω",
      strongsKey: "G3140",
      prefixes: "σύν + ἐπί",
      origin: "agent",
      isSimple: false,
    },
    ...over,
  });

const simple = (over: Partial<VocabEntryRow> = {}): VocabEntryRow =>
  greek({
    id: 22,
    rank: 300,
    lemma: "μαρτυρέω",
    transliteration: "martyreō",
    dictionaryForm: null,
    meanings: ["bezeugen"],
    occurrences: 76,
    strongsKeys: ["G3140"],
    root: { id: 4, root: "μαρτυρέω", strongsKey: "G3140", origin: "prefill", isSimple: true },
    ...over,
  });

function renderList(
  entries: VocabEntryRow[],
  onPlay?: (id: number) => void,
  searchTo?: (query: string) => string,
) {
  return render(
    <MemoryRouter>
      <VocabEntryList
        mode="public"
        entries={entries}
        linkTo={(id) => `/vocab/${id}`}
        onPlay={onPlay}
        searchTo={searchTo}
      />
    </MemoryRouter>,
  );
}

beforeEach(() => i18n.changeLanguage("en"));

describe("VocabEntryList", () => {
  it("renders rank, lemma and occurrences in the table", () => {
    renderList([greek({ occurrences: 20754, rank: 1, lemma: "ὁ" })]);
    const table = screen.getByRole("table");
    expect(within(table).getByText("1")).toBeInTheDocument();
    expect(within(table).getByRole("link", { name: "ὁ" })).toHaveAttribute("href", "/vocab/1");
    expect(within(table).getByText("20,754")).toBeInTheDocument();
    expect(within(table).getByText("Wort, Rede")).toBeInTheDocument();
  });

  it("shows the unreviewed badge only for unreviewed entries", () => {
    renderList([
      greek({ id: 1, reviewed: true }),
      greek({ id: 2, rank: 2, lemma: "καί", reviewed: false }),
    ]);
    const table = screen.getByRole("table");
    expect(within(table).getAllByText(/unreviewed/)).toHaveLength(1);
  });

  it("renders no play button without audio", () => {
    renderList([greek({ hasAudio: false })]);
    expect(screen.queryByRole("button", { name: "Play pronunciation" })).not.toBeInTheDocument();
    expect(screen.getAllByText("no audio").length).toBeGreaterThan(0);
  });

  it("calls onPlay with the entry id when the play button is pressed", async () => {
    const onPlay = vi.fn();
    renderList([greek({ id: 7, hasAudio: true })], onPlay);
    await userEvent.click(
      within(screen.getByRole("table")).getByRole("button", { name: "Play pronunciation" }),
    );
    expect(onPlay).toHaveBeenCalledWith(7);
  });

  it("marks Hebrew lemmas with lang he and dir rtl", () => {
    renderList([
      greek({ id: 9, lemma: "בָּרָא", transliteration: "bara", dictionaryForm: null, lang: "he" }),
    ]);
    for (const link of screen.getAllByRole("link", { name: "בָּרָא" })) {
      expect(link).toHaveAttribute("lang", "he");
      expect(link).toHaveAttribute("dir", "rtl");
    }
  });

  it("does not set a direction on Greek lemmas", () => {
    renderList([greek()]);
    const link = within(screen.getByRole("table")).getByRole("link", { name: "λόγος" });
    expect(link).toHaveAttribute("lang", "grc");
    expect(link).not.toHaveAttribute("dir");
  });

  it("shows prefixes, root with the AI badge and the root's Strong's in the table", () => {
    renderList([compound()]);
    const table = screen.getByRole("table");
    expect(within(table).getByRole("columnheader", { name: "Prefixes" })).toBeInTheDocument();
    expect(within(table).getByRole("columnheader", { name: "Root" })).toBeInTheDocument();
    expect(within(table).getByRole("columnheader", { name: "Root Strong's" })).toBeInTheDocument();
    expect(within(table).getByText("σύν + ἐπί")).toBeInTheDocument();
    expect(within(table).getByText("μαρτυρέω")).not.toHaveAttribute("data-simple");
    expect(within(table).getByText("AI")).toBeInTheDocument();
    expect(within(table).getByRole("link", { name: "Open root G3140" })).toHaveTextContent("G3140");
  });

  it("links the root's Strong's to the root entry when it is on the page", () => {
    renderList([compound(), simple()], undefined, (q) => `/vocab?q=${q}`);
    const table = screen.getByRole("table");
    for (const link of within(table).getAllByRole("link", { name: "Open root G3140" }))
      expect(link).toHaveAttribute("href", "/vocab/22");
  });

  it("links the root's Strong's to the list searched for the root when it is not on the page", () => {
    renderList([compound()], undefined, (q) => `/vocab?q=${encodeURIComponent(q)}`);
    expect(
      within(screen.getByRole("table")).getByRole("link", { name: "Open root G3140" }),
    ).toHaveAttribute("href", `/vocab?q=${encodeURIComponent("μαρτυρέω")}`);
  });

  it("falls back to the Strong's page without a search link", () => {
    renderList([compound()]);
    expect(
      within(screen.getByRole("table")).getByRole("link", { name: "Open root G3140" }),
    ).toHaveAttribute("href", "/bible/strongs/G3140");
  });

  it("links the root text to its family when rootTo is given", () => {
    render(
      <MemoryRouter>
        <VocabEntryList
          mode="public"
          entries={[compound()]}
          linkTo={(id) => `/vocab/${id}`}
          rootTo={(id) => `/vocab?root=${id}`}
        />
      </MemoryRouter>,
    );
    const link = within(screen.getByRole("table")).getByRole("link", {
      name: "Show the word family of μαρτυρέω",
    });
    expect(link).toHaveAttribute("href", "/vocab?root=4");
  });

  it("dims the root of a simple word and shows no AI badge for prefill roots", () => {
    renderList([simple()]);
    const table = screen.getByRole("table");
    const root = within(table)
      .getAllByText("μαρτυρέω")
      .find((node) => node.tagName === "SPAN");
    expect(root).toHaveAttribute("data-simple", "true");
    expect(root).toHaveClass("text-fg-muted");
    expect(within(table).queryByText("AI")).toBeNull();
  });

  it("leaves the root cells empty without a root, never 'undefined'", () => {
    const { container } = renderList([greek()]);
    expect(container.textContent).not.toContain("undefined");
    const cells = within(screen.getByRole("table")).getAllByRole("cell");
    // rank, lemma, form, then the three root cells
    expect(cells[3]).toBeEmptyDOMElement();
    expect(cells[4]).toBeEmptyDOMElement();
    expect(cells[5]).toBeEmptyDOMElement();
    expect(screen.queryByTestId("root-line")).toBeNull();
  });

  it("collapses the root into one line under the lemma on phone width", () => {
    renderList([compound()]);
    const line = screen.getByTestId("root-line");
    expect(line.closest("ol")).not.toBeNull();
    expect(line).toHaveTextContent("σύν + ἐπί · μαρτυρέω (G3140)");
  });

  it("marks Hebrew roots with lang he and dir rtl", () => {
    renderList([
      greek({
        id: 9,
        lemma: "כָּתַב",
        dictionaryForm: null,
        lang: "he",
        strongsKeys: ["H3789"],
        root: { id: 8, root: "כתב", strongsKey: "H3789", origin: "prefill", isSimple: true },
      }),
    ]);
    for (const root of screen.getAllByText("כתב")) {
      expect(root).toHaveAttribute("lang", "he");
      expect(root).toHaveAttribute("dir", "rtl");
      expect(root).toHaveClass("font-original");
    }
  });
});

describe("VocabEntryList in editor mode", () => {
  function renderEditor(entries: VocabEntryRow[]) {
    return render(
      <MemoryRouter>
        <VocabEntryList mode="editor" entries={entries} linkTo={(id) => `/edit/vocab/${id}`} />
      </MemoryRouter>,
    );
  }

  it("links each lemma to the editor and shows audio, reviewed and learners", () => {
    renderEditor([greek({ id: 3, hasAudio: true, reviewed: false, learners: 12 })]);
    const table = screen.getByRole("table");
    expect(within(table).getByRole("link", { name: "λόγος" })).toHaveAttribute(
      "href",
      "/edit/vocab/3",
    );
    expect(within(table).getByText("yes")).toBeInTheDocument();
    expect(within(table).getByText("no")).toBeInTheDocument();
    expect(within(table).getByText("12")).toBeInTheDocument();
    expect(within(table).queryByRole("button", { name: "Play pronunciation" })).toBeNull();
  });

  it("shows the agent origin of a study form and no occurrences column", () => {
    renderEditor([greek({ formOrigin: "agent" })]);
    const table = screen.getByRole("table");
    expect(within(table).getByText("from AI")).toBeInTheDocument();
    expect(within(table).queryByText("330")).toBeNull();
  });

  it("shows a dash for an entry without a rank", () => {
    renderEditor([greek({ rank: null })]);
    expect(
      within(screen.getByRole("table")).getByText("–", { selector: "td" }),
    ).toBeInTheDocument();
  });

  it("shows the root columns in the editor too", () => {
    renderEditor([compound()]);
    const table = screen.getByRole("table");
    expect(within(table).getByRole("columnheader", { name: "Root Strong's" })).toBeInTheDocument();
    expect(within(table).getByText("σύν + ἐπί")).toBeInTheDocument();
    expect(within(table).getByRole("link", { name: "Open root G3140" })).toHaveAttribute(
      "href",
      "/bible/strongs/G3140",
    );
  });
});
