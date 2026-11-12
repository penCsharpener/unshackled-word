import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { applyEdit, type Edits } from "@/lib/lemmas/edits";
import { buildRules } from "@/lib/lemmas/rules";
import { chapterWords, labelsDe, valueMaps } from "@/test/fixtures/lemmas";
import { MorphologyGrid, type MorphologyGridProps } from "./MorphologyGrid";

const rules = buildRules(valueMaps.rules);

async function setup(props: Partial<MorphologyGridProps> = {}) {
  const onEdit = vi.fn();
  const utils = render(
    <MorphologyGrid
      words={chapterWords}
      edits={new Map()}
      labels={labelsDe}
      rules={rules}
      hiddenColumns={new Set()}
      onEdit={onEdit}
      onResetRow={vi.fn()}
      {...props}
    />,
  );
  // the virtualizer renders the rows after its first measurement
  await waitFor(() => expect(cell(1, "lemma")).not.toBeNull());
  return { onEdit, ...utils };
}

const cell = (wordId: number, col: string) =>
  document.querySelector<HTMLElement>(`[data-cell="${wordId}:${col}"]`) as HTMLElement;

describe("MorphologyGrid", () => {
  beforeAll(() => {
    // jsdom has no layout: give every element a size so the virtualizer renders rows.
    const size = { configurable: true, get: () => 800 };
    Object.defineProperty(HTMLElement.prototype, "clientHeight", size);
    Object.defineProperty(HTMLElement.prototype, "clientWidth", size);
  });
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("renders labels, missing values and not-applicable cells", async () => {
    await setup();
    expect(screen.getByRole("grid")).toBeInTheDocument();
    expect(within(cell(1, "partOfSpeech")).getByText("substantiv")).toBeInTheDocument();
    expect(within(cell(3, "lemma")).getByText("fehlt")).toBeInTheDocument();
    expect(cell(3, "case")).toHaveAttribute("aria-disabled", "true");
    expect(cell(1, "case")).not.toHaveAttribute("aria-disabled");
  });

  it("shows a value the rules do not allow as invalid", async () => {
    await setup({ words: [{ ...chapterWords[0]!, tense: "Pres" }] });
    expect(within(cell(1, "tense")).getByText("Präsens").tagName).toBe("S");
  });

  it("moves the cursor with the arrow keys and uses a single tab stop", async () => {
    await setup();
    cell(1, "lemma").focus();
    await userEvent.keyboard("{ArrowDown}");
    expect(cell(2, "lemma")).toHaveFocus();
    expect(cell(2, "lemma")).toHaveAttribute("tabindex", "0");
    expect(cell(1, "lemma")).toHaveAttribute("tabindex", "-1");
    await userEvent.keyboard("{ArrowRight}");
    expect(cell(2, "partOfSpeech")).toHaveFocus();
  });

  it("tabs to the next editable cell and skips not-applicable ones", async () => {
    await setup();
    cell(3, "partOfSpeech").focus();
    await userEvent.keyboard("{Tab}");
    // ADP allows nothing: no editable cell follows, so Tab leaves the grid
    expect(cell(3, "partOfSpeech")).not.toHaveFocus();
    cell(1, "partOfSpeech").focus();
    await userEvent.keyboard("{Tab}");
    expect(cell(1, "number")).toHaveFocus();
  });

  it("edits the lemma: type to replace, Enter commits", async () => {
    const { onEdit } = await setup();
    cell(1, "lemma").focus();
    await userEvent.keyboard("L");
    const input = screen.getByRole("combobox", { name: /Lemma für Wort1/ });
    expect(input).toHaveValue("L");
    await userEvent.type(input, "x{Enter}");
    expect(onEdit).toHaveBeenCalledWith(chapterWords[0], "lemma", "Lx");
    expect(cell(2, "lemma")).toHaveFocus();
  });

  it("cancels an edit with Escape", async () => {
    const { onEdit } = await setup();
    cell(1, "lemma").focus();
    await userEvent.keyboard("{Enter}");
    await userEvent.keyboard("zzz{Escape}");
    expect(onEdit).not.toHaveBeenCalled();
    expect(cell(1, "lemma")).toHaveFocus();
  });

  it("picks a dropdown value by letter and clears with Delete", async () => {
    const { onEdit } = await setup();
    cell(1, "partOfSpeech").focus();
    await userEvent.keyboard("v");
    expect(onEdit).toHaveBeenCalledWith(chapterWords[0], "partOfSpeech", "VERB");
    await userEvent.keyboard("{Delete}");
    expect(onEdit).toHaveBeenLastCalledWith(chapterWords[0], "partOfSpeech", null);
  });

  it("marks changed cells and offers a row reset", async () => {
    const edits: Edits = applyEdit(new Map(), chapterWords[0]!, rules, "lemma", "Lichter");
    const onResetRow = vi.fn();
    await setup({ edits, onResetRow });
    expect(within(cell(1, "lemma")).getByText("Lichter")).toBeInTheDocument();
    expect(cell(1, "lemma")).toHaveTextContent("geändert");
    await userEvent.click(screen.getByRole("button", { name: "Zeile zurücksetzen" }));
    expect(onResetRow).toHaveBeenCalledWith(1);
  });

  it("does not open editors when read-only", async () => {
    const { onEdit } = await setup({ readOnly: true });
    cell(1, "lemma").focus();
    await userEvent.keyboard("{Enter}x{Delete}");
    expect(screen.queryByRole("combobox", { name: /Lemma/ })).toBeNull();
    expect(onEdit).not.toHaveBeenCalled();
  });

  it("hides columns", async () => {
    await setup({ hiddenColumns: new Set(["plain", "tense"]) });
    expect(screen.queryByRole("columnheader", { name: "Tempus" })).toBeNull();
    expect(screen.getByRole("columnheader", { name: "Kasus" })).toBeInTheDocument();
  });
});
