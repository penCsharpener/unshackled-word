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

function renderList(entries: VocabEntryRow[], onPlay?: (id: number) => void) {
  return render(
    <MemoryRouter>
      <VocabEntryList
        mode="public"
        entries={entries}
        linkTo={(id) => `/vocab/${id}`}
        onPlay={onPlay}
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
});
