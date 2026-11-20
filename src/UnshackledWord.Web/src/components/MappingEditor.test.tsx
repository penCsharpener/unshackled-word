import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { GetVerseMappingResponse, VerseMappingLink } from "@/lib/bible/types";
import { MappingEditor, type MappingEditorProps } from "./MappingEditor";

const data: GetVerseMappingResponse = {
  bibleBookId: 1,
  chapter: 1,
  verse: 1,
  language: "hebrew",
  editingEnabled: true,
  germanWords: [
    { id: 1, positionInVerse: 1, text: "Im" },
    { id: 2, positionInVerse: 2, text: "Anfang" },
    { id: 3, positionInVerse: 3, text: "schuf" },
    { id: 4, positionInVerse: 4, text: "Gott" },
  ],
  originalWords: [
    {
      id: 10,
      position: 1,
      original: "בְּרֵאשִׁית",
      lemma: "רֵאשִׁית",
      strongs: "H7225G",
      gloss: "beginning",
      morphologyCode: "HR/Ncfsa",
    },
    { id: 11, position: 2, original: "בָּרָא", strongs: "H1254A" },
    { id: 12, position: 3, original: "אֱלֹהִים" },
  ],
  links: [
    { elbWordId: 1, isAddedWord: true, parentElbWordId: 2 },
    { elbWordId: 2, stepWordId: 10, isAddedWord: false },
    { elbWordId: 3, stepWordId: 11, isAddedWord: false },
  ],
};

const node = (key: string) => document.querySelector(`[data-node="${key}"]`) as HTMLElement;
const badge = (key: string) => node(key).querySelector(".map-badge")?.textContent;

function setup(props: Partial<MappingEditorProps> = {}) {
  const onSave = vi.fn();
  const onCancel = vi.fn();
  render(
    <MappingEditor
      data={data}
      reference="1. Mose 1,1"
      onSave={onSave}
      onCancel={onCancel}
      {...props}
    />,
  );
  return { onSave, onCancel };
}

beforeEach(async () => {
  await i18n.changeLanguage("de");
});

/** A diff list item by its full text; Hebrew runs sit in their own spans. */
function diffItem(text: string): HTMLElement {
  return screen.getByText((_, el) => el?.tagName === "LI" && el.textContent === text);
}

describe("MappingEditor", () => {
  it("shows numbered group badges, added words and unmapped words", () => {
    setup();
    expect(badge("g2")).toBe("1");
    expect(badge("o10")).toBe("1");
    expect(badge("g1")).toBe("+1");
    expect(badge("g3")).toBe("2");
    expect(badge("g4")).toBe("?");
    expect(badge("o12")).toBe("–");
  });

  it("lays out the Hebrew row right to left and labels the direction", () => {
    setup();
    expect(node("o10").closest(".map-row")).toHaveAttribute("dir", "rtl");
    expect(screen.getByText(/liest von rechts nach links/)).toBeInTheDocument();
    expect(node("g1").closest(".map-row")).not.toHaveAttribute("dir", "rtl");
  });

  it("uses a left-to-right original row for Greek", () => {
    setup({ data: { ...data, language: "greek" } });
    expect(node("o10").closest(".map-row")).toHaveAttribute("dir", "ltr");
  });

  it("reverses the German row on demand and remembers it per original language", async () => {
    localStorage.removeItem("uw.mapping.reverseGerman.hebrew");
    setup();
    const row = node("g1").closest(".map-row");
    expect(row).not.toHaveAttribute("data-reversed");
    const toggle = screen.getByRole("button", { name: /Deutsch rückwärts/ });
    await userEvent.click(toggle);
    expect(row).toHaveAttribute("data-reversed");
    expect(toggle).toHaveAttribute("aria-pressed", "true");
    expect(localStorage.getItem("uw.mapping.reverseGerman.hebrew")).toBe("1");
    await userEvent.click(toggle);
    expect(row).not.toHaveAttribute("data-reversed");
  });
  it("gives every tile a descriptive accessible name", () => {
    setup();
    expect(node("g2")).toHaveAccessibleName("Anfang, verbunden mit בְּרֵאשִׁית");
    expect(node("g1")).toHaveAccessibleName("Im, ergänzt, gehört zu Anfang");
    expect(node("g4")).toHaveAccessibleName("Gott, nicht zugeordnet");
  });

  it("shows Strong's, morphology and gloss on demand", async () => {
    setup();
    expect(screen.queryByText("HR/Ncfsa")).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: "Strong's & Morphologie" }));
    expect(screen.getByText("HR/Ncfsa")).toBeInTheDocument();
    expect(screen.getByText("beginning")).toBeInTheDocument();
  });

  describe("select, then tap", () => {
    it("connects a German and an original word and saves the new rows", async () => {
      const { onSave } = setup();
      await userEvent.click(node("g4"));
      expect(node("g4")).toHaveAttribute("aria-pressed", "true");
      await userEvent.click(node("o12"));
      expect(badge("g4")).toBe("3");
      expect(node("g4")).toHaveAttribute("data-status", "edited");
      expect(screen.getByRole("status")).toHaveTextContent("Gott – אֱלֹהִים verbunden.");
      expect(diffItem("Verbunden: Gott – אֱלֹהִים")).toBeInTheDocument();
      await userEvent.click(screen.getByRole("button", { name: "Speichern (1)" }));
      expect(onSave).toHaveBeenCalledWith(
        [
          { elbWordId: 2, stepWordId: 10, isAddedWord: false },
          { elbWordId: 3, stepWordId: 11, isAddedWord: false },
          { elbWordId: 4, stepWordId: 12, isAddedWord: false },
          { elbWordId: 1, isAddedWord: true, parentElbWordId: 2 },
        ],
        { clearParts: false },
      );
    });

    it("works from the original word first", async () => {
      setup();
      await userEvent.click(node("o12"));
      await userEvent.click(node("g4"));
      expect(badge("o12")).toBe("3");
    });

    it("tapping a connected pair again disconnects it", async () => {
      setup();
      await userEvent.click(node("g3"));
      await userEvent.click(node("o11"));
      expect(badge("g3")).toBe("?");
      expect(diffItem("Gelöst: schuf – בָּרָא")).toBeInTheDocument();
    });

    it("moves the selection when tapping a word of the same row", async () => {
      setup();
      await userEvent.click(node("g3"));
      await userEvent.click(node("g4"));
      expect(node("g3")).toHaveAttribute("aria-pressed", "false");
      expect(node("g4")).toHaveAttribute("aria-pressed", "true");
    });

    it("marks a word as added via the base-word picker", async () => {
      setup();
      await userEvent.click(node("g4"));
      await userEvent.click(screen.getByRole("button", { name: "Als ergänzt markieren …" }));
      expect(screen.getByText(/Bezugswort wählen/)).toBeInTheDocument();
      await userEvent.click(node("g3"));
      expect(badge("g4")).toBe("+2");
      expect(screen.getByText("Ergänzt: Gott gehört zu schuf")).toBeInTheDocument();
    });

    it("removes a connection with the chip's cross", async () => {
      setup();
      await userEvent.click(node("g3"));
      await userEvent.click(screen.getByRole("button", { name: "Verbindung schuf – בָּרָא lösen" }));
      expect(badge("g3")).toBe("?");
    });

    it("removes an addition with the chip's cross", async () => {
      setup();
      await userEvent.click(node("g1"));
      await userEvent.click(screen.getByRole("button", { name: "Ergänzung lösen" }));
      expect(badge("g1")).toBe("?");
    });

    it("clears all connections of the selected word with Delete", async () => {
      setup();
      await userEvent.click(node("g3"));
      await userEvent.keyboard("{Delete}");
      expect(badge("g3")).toBe("?");
      expect(badge("o11")).toBe("–");
    });
  });

  describe("drag and drop", () => {
    it("connects a German word dropped on an original word", () => {
      setup();
      fireEvent.dragStart(node("g4"));
      fireEvent.dragOver(node("o12"));
      expect(node("o12")).toHaveAttribute("data-drop");
      fireEvent.drop(node("o12"));
      fireEvent.dragEnd(node("g4"));
      expect(badge("g4")).toBe("3");
      expect(node("o12")).not.toHaveAttribute("data-drop");
    });

    it("connects an original word dropped on a German word", () => {
      setup();
      fireEvent.dragStart(node("o12"));
      fireEvent.drop(node("g4"));
      expect(badge("o12")).toBe("3");
    });

    it("makes a German word dropped on another German word an added word of it", () => {
      setup();
      fireEvent.dragStart(node("g4"));
      fireEvent.drop(node("g3"));
      expect(badge("g4")).toBe("+2");
    });

    it("rejects original onto original", () => {
      setup();
      fireEvent.dragStart(node("o11"));
      fireEvent.dragOver(node("o12"));
      expect(node("o12")).not.toHaveAttribute("data-drop");
      fireEvent.drop(node("o12"));
      expect(screen.getByRole("status")).toHaveTextContent("Zwei Urtextwörter");
    });

    it("clears all connections of a word dropped on the drop zone", () => {
      setup();
      fireEvent.dragStart(node("g3"));
      const zone = screen.getByText(/alle seine Verbindungen lösen/);
      fireEvent.dragOver(zone);
      expect(zone).toHaveAttribute("data-drop");
      fireEvent.drop(zone);
      expect(badge("g3")).toBe("?");
    });

    it("ignores a drop without a started drag", () => {
      setup();
      fireEvent.drop(node("o12"));
      expect(badge("o12")).toBe("–");
    });
  });

  describe("group over-assignment hint", () => {
    it("marks a German word with three originals", async () => {
      setup();
      await userEvent.click(node("g2"));
      await userEvent.click(node("o11"));
      await userEvent.click(node("o12"));
      expect(node("g2").querySelector(".map-warn")).not.toBeNull();
    });
  });

  describe("keyboard", () => {
    it("Escape clears the selection first, then cancels", async () => {
      const { onCancel } = setup();
      await userEvent.click(node("g4"));
      await userEvent.keyboard("{Escape}");
      expect(node("g4")).toHaveAttribute("aria-pressed", "false");
      expect(onCancel).not.toHaveBeenCalled();
      await userEvent.keyboard("{Escape}");
      expect(onCancel).toHaveBeenCalledOnce();
    });

    it("Ctrl+Z undoes edit by edit", async () => {
      setup();
      await userEvent.click(node("g4"));
      await userEvent.click(node("o12"));
      await userEvent.click(node("o11"));
      expect(screen.getByRole("button", { name: "Rückgängig (2)" })).toBeInTheDocument();
      await userEvent.keyboard("{Control>}z{/Control}");
      expect(badge("g4")).toBe("3");
      expect(node("o11").getAttribute("aria-label")).not.toContain("Gott");
      await userEvent.keyboard("{Control>}z{/Control}");
      expect(badge("g4")).toBe("?");
      expect(screen.getByRole("button", { name: "Rückgängig" })).toBeDisabled();
    });

    it("focuses the first word when it opens", () => {
      setup();
      expect(node("g1")).toHaveFocus();
    });
  });

  describe("actions", () => {
    it("Save without edits just closes", async () => {
      const { onSave, onCancel } = setup();
      expect(screen.getByText("Keine Änderungen.")).toBeInTheDocument();
      await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
      expect(onSave).not.toHaveBeenCalled();
      expect(onCancel).toHaveBeenCalledOnce();
    });

    it("Cancel discards", async () => {
      const { onSave, onCancel } = setup();
      await userEvent.click(node("g4"));
      await userEvent.click(node("o12"));
      await userEvent.click(screen.getByRole("button", { name: "Abbrechen" }));
      expect(onCancel).toHaveBeenCalledOnce();
      expect(onSave).not.toHaveBeenCalled();
    });

    it("hides the AI action without onAiRedo and runs it when set", async () => {
      setup();
      expect(screen.queryByRole("button", { name: /KI neu zuordnen/ })).not.toBeInTheDocument();
      cleanup();
      const onAiRedo = vi.fn();
      setup({ onAiRedo });
      const redo = screen.getByRole("button", { name: /KI neu zuordnen/ });
      expect(redo).toBeEnabled();
      await userEvent.click(redo);
      expect(onAiRedo).toHaveBeenCalledTimes(1);
      expect(screen.queryByText(/Kommt später/)).not.toBeInTheDocument();
    });

    it("disables Save while saving and shows a save error", () => {
      setup({ saving: true, saveError: "validation" });
      expect(screen.getByRole("button", { name: "Wird gespeichert …" })).toBeDisabled();
      expect(within(screen.getByRole("alert")).getByText(/ungültig/)).toBeInTheDocument();
    });
  });

  describe("read-only", () => {
    it("has no Save, no undo, no dragging and explains why", async () => {
      const { onSave, onCancel } = setup({ data: { ...data, editingEnabled: false } });
      expect(screen.getByText(/noch nicht freigeschaltet/)).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: /Speichern/ })).toBeNull();
      expect(screen.queryByRole("button", { name: /Rückgängig/ })).toBeNull();
      expect(node("g4")).toHaveAttribute("draggable", "false");
      expect(screen.queryByText(/alle seine Verbindungen lösen/)).toBeNull();
      await userEvent.click(node("g4"));
      await userEvent.click(node("o12"));
      expect(badge("g4")).toBe("?");
      await userEvent.click(screen.getByRole("button", { name: "Schließen" }));
      expect(onCancel).toHaveBeenCalledOnce();
      expect(onSave).not.toHaveBeenCalled();
    });
  });

  describe("compound parts", () => {
    const partsData: GetVerseMappingResponse = {
      ...data,
      germanWords: [
        { id: 1, positionInVerse: 1, text: "Im" },
        { id: 2, positionInVerse: 2, text: "Gerstenernte" },
      ],
      originalWords: [
        { id: 10, position: 1, original: "שְׂעֹרִים" },
        { id: 11, position: 2, original: "קָצִיר" },
        { id: 12, position: 3, original: "בְּ" },
      ],
      links: [
        {
          elbWordId: 2,
          stepWordId: 10,
          isAddedWord: false,
          partOrder: 1,
          germanWordPart: "Gersten",
        },
        { elbWordId: 2, stepWordId: 11, isAddedWord: false, partOrder: 2, germanWordPart: "ernte" },
      ],
    };

    it("draws the parts of a word as joined tiles that are linked on their own", () => {
      setup({ data: partsData });
      expect(node("g2")).toBeNull();
      expect(node("g2.1")).toHaveTextContent("Gersten");
      expect(node("g2.1")).toHaveAttribute("data-part", "first");
      expect(node("g2.2")).toHaveTextContent("ernte");
      expect(node("g2.2")).toHaveAttribute("data-part", "last");
      expect(node("g2.1").parentElement).toBe(node("g2.2").parentElement);
      expect(badge("g2.1")).toBe("1");
      expect(badge("g2.2")).toBe("2");
      expect(node("g2.2")).toHaveAccessibleName(
        /Teil 2 »ernte« von Gerstenernte, verbunden mit קָצִיר/,
      );
    });

    it("saves the parts back with their order, and clearParts stays false", async () => {
      const { onSave } = setup({ data: partsData, partsEditable: true });
      await userEvent.click(node("g1"));
      await userEvent.click(node("o12"));
      await userEvent.click(screen.getByRole("button", { name: "Speichern (1)" }));
      expect(onSave).toHaveBeenCalledWith(
        [
          { elbWordId: 1, stepWordId: 12, isAddedWord: false },
          {
            elbWordId: 2,
            stepWordId: 10,
            isAddedWord: false,
            partOrder: 1,
            germanWordPart: "Gersten",
          },
          {
            elbWordId: 2,
            stepWordId: 11,
            isAddedWord: false,
            partOrder: 2,
            germanWordPart: "ernte",
          },
        ],
        { clearParts: false },
      );
    });

    it("links a part to another original word", async () => {
      const { onSave } = setup({ data: partsData, partsEditable: true });
      await userEvent.click(node("g2.2"));
      await userEvent.click(node("o12"));
      expect(diffItem("Verbunden: ernte – בְּ")).toBeInTheDocument();
      await userEvent.click(screen.getByRole("button", { name: "Speichern (1)" }));
      const links = onSave.mock.calls[0]?.[0] as VerseMappingLink[];
      expect(links).toContainEqual({
        elbWordId: 2,
        stepWordId: 12,
        isAddedWord: false,
        partOrder: 2,
        germanWordPart: "ernte",
      });
    });

    it("undoes a split: whole-word links, a visible note and clearParts", async () => {
      const { onSave } = setup({ data: partsData, partsEditable: true });
      await userEvent.click(node("g2.1"));
      await userEvent.click(screen.getByRole("button", { name: "Teilung aufheben" }));
      expect(node("g2")).toHaveTextContent("Gerstenernte");
      expect(node("g2.1")).toBeNull();
      expect(
        screen.getByText("Teilung aufgehoben: Gerstenernte (war Gersten + ernte)"),
      ).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Speichern (1)" })).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Teilung aufheben" })).not.toBeInTheDocument();
      expect(node("g2")).toHaveAttribute("aria-pressed", "true");
      expect(screen.getByText(/werden die Wortteile von Gerstenernte entfernt/)).toBeVisible();
      await userEvent.click(screen.getByRole("button", { name: /Speichern/ }));
      expect(onSave).toHaveBeenCalledWith(
        [
          { elbWordId: 2, stepWordId: 10, isAddedWord: false },
          { elbWordId: 2, stepWordId: 11, isAddedWord: false },
        ],
        { clearParts: true },
      );
    });

    it("offers no split changes for data without editable parts", async () => {
      setup({ data: partsData });
      await userEvent.click(node("g2.1"));
      expect(screen.queryByRole("button", { name: "Teilung aufheben" })).not.toBeInTheDocument();
    });

    it("blocks saving while a part has no link and says why", async () => {
      const { onSave } = setup({ data: partsData, partsEditable: true });
      await userEvent.click(node("g2.1"));
      await userEvent.click(screen.getByRole("button", { name: "Alle lösen" }));
      expect(screen.getByRole("alert")).toHaveTextContent(
        "Der Teil „Gersten“ von „Gerstenernte“ ist keinem Urtextwort zugeordnet.",
      );
      const save = screen.getByRole("button", { name: /Speichern/ });
      expect(save).toBeDisabled();
      await userEvent.click(save);
      expect(onSave).not.toHaveBeenCalled();
    });

    it("does not make a part an added word", async () => {
      setup({ data: partsData, partsEditable: true });
      await userEvent.click(node("g2.1"));
      expect(
        screen.queryByRole("button", { name: /Als ergänzt markieren/ }),
      ).not.toBeInTheDocument();
      cleanup();
      setup({ data: partsData, partsEditable: true });
      await userEvent.click(node("g1"));
      await userEvent.click(screen.getByRole("button", { name: /Als ergänzt markieren/ }));
      await userEvent.click(node("g2.2"));
      expect(screen.getByText(/Ergänzt: Im gehört zu Gerstenernte/)).toBeInTheDocument();
    });
  });

  describe("split a word", () => {
    const splitData: GetVerseMappingResponse = {
      ...data,
      germanWords: [
        { id: 1, positionInVerse: 1, text: "Im" },
        { id: 2, positionInVerse: 2, text: "Weizenernte" },
        { id: 3, positionInVerse: 3, text: "a" },
      ],
      originalWords: [
        { id: 10, position: 1, original: "חִטִּים" },
        { id: 11, position: 2, original: "קְצִיר" },
      ],
      links: [{ elbWordId: 2, stepWordId: 10, isAddedWord: false }],
    };

    it("splits the selected word with the splitter, links the parts and saves them", async () => {
      const { onSave } = setup({ data: splitData, partsEditable: true });
      await userEvent.click(node("g2"));
      await userEvent.click(screen.getByRole("button", { name: "Wort teilen …" }));
      await userEvent.click(document.querySelector('[data-gap="6"]') as HTMLElement);
      expect(screen.getByText("Weizen | ernte")).toBeInTheDocument();
      await userEvent.click(screen.getByRole("button", { name: "Teilen" }));
      expect(node("g2.1")).toHaveTextContent("Weizen");
      expect(node("g2.2")).toHaveTextContent("ernte");
      expect(screen.getByRole("status")).toHaveTextContent(
        "„Weizenernte“ geteilt in Weizen + ernte.",
      );
      // the whole word's link is dropped; Save waits until both parts are linked
      expect(screen.getByRole("button", { name: /Speichern/ })).toBeDisabled();
      // the first part is selected after the split
      await userEvent.click(node("o10"));
      await userEvent.click(node("g2.2"));
      await userEvent.click(node("o11"));
      await userEvent.click(screen.getByRole("button", { name: /Speichern/ }));
      expect(onSave).toHaveBeenCalledWith(
        [
          {
            elbWordId: 2,
            stepWordId: 10,
            isAddedWord: false,
            partOrder: 1,
            germanWordPart: "Weizen",
          },
          {
            elbWordId: 2,
            stepWordId: 11,
            isAddedWord: false,
            partOrder: 2,
            germanWordPart: "ernte",
          },
        ],
        { clearParts: false },
      );
    });

    it("offers the splitter only for a whole word of at least two letters on editable data", async () => {
      setup({ data: splitData, partsEditable: true });
      await userEvent.click(node("g3"));
      expect(screen.queryByRole("button", { name: "Wort teilen …" })).toBeNull();
      await userEvent.click(node("g2"));
      expect(screen.getByRole("button", { name: "Wort teilen …" })).toBeInTheDocument();
      cleanup();
      setup({ data: splitData });
      await userEvent.click(node("g2"));
      expect(screen.queryByRole("button", { name: "Wort teilen …" })).toBeNull();
    });

    it("closes the splitter with Escape without leaving the editor", async () => {
      const { onCancel } = setup({ data: splitData, partsEditable: true });
      await userEvent.click(node("g2"));
      await userEvent.click(screen.getByRole("button", { name: "Wort teilen …" }));
      await userEvent.keyboard("{Escape}");
      expect(screen.queryByRole("group", { name: /teilen/ })).toBeNull();
      expect(onCancel).not.toHaveBeenCalled();
      expect(node("g2")).toBeInTheDocument();
    });
  });

  describe("compare mode", () => {
    const proposed: VerseMappingLink[] = [
      { elbWordId: 2, stepWordId: 10, isAddedWord: false },
      { elbWordId: 3, stepWordId: 12, isAddedWord: false },
      { elbWordId: 4, isAddedWord: true, parentElbWordId: 3 },
    ];

    it("draws every line: new solid, removed dashed, unchanged neutral", () => {
      setup({ mode: "compare", compareLinks: proposed });
      const kinds = [...document.querySelectorAll(".map-lines path")].map((p) =>
        p.getAttribute("data-kind"),
      );
      expect(kinds.filter((k) => k === "unchanged")).toHaveLength(1);
      expect(kinds.filter((k) => k === "added")).toHaveLength(2);
      expect(kinds.filter((k) => k === "removed")).toHaveLength(2);
    });

    it("is read-only: no save row, no undo, no editing hint, nothing focused on its own", async () => {
      const onSave = vi.fn();
      setup({ mode: "compare", compareLinks: proposed, onSave });
      expect(screen.queryByRole("button", { name: /Speichern/ })).toBeNull();
      expect(screen.queryByRole("button", { name: /Rückgängig/ })).toBeNull();
      expect(screen.queryByText(/Bearbeiten ist abgeschaltet/)).toBeNull();
      expect(document.activeElement).toBe(document.body);
      expect(document.querySelector("[data-mapping-editor]")).toBeNull();
      await userEvent.click(node("g3"));
      await userEvent.click(node("o12"));
      expect(onSave).not.toHaveBeenCalled();
      expect(screen.queryByRole("button", { name: /Verbindung .* lösen/ })).toBeNull();
    });

    it("explains the lines and lists the differences", () => {
      setup({ mode: "compare", compareLinks: proposed });
      expect(screen.getByText("neu")).toBeInTheDocument();
      expect(screen.getByText("entfällt")).toBeInTheDocument();
      expect(screen.getByText("unverändert")).toBeInTheDocument();
      expect(diffItem("Verbunden: schuf – אֱלֹהִים")).toBeInTheDocument();
      expect(diffItem("Gelöst: schuf – בָּרָא")).toBeInTheDocument();
      expect(screen.getByText("Ergänzt: Gott gehört zu schuf")).toBeInTheDocument();
      expect(screen.getByText("Ergänzung gelöst: Im (war bei Anfang)")).toBeInTheDocument();
    });

    it("hides unit lines of any kind with the unit switch, independent of the kind switches", async () => {
      setup({
        mode: "compare",
        compareLinks: [
          ...proposed,
          { elbWordId: 1, stepWordId: 10, isAddedWord: false, role: "unit" },
        ],
      });
      const paths = () => [...document.querySelectorAll(".map-lines path")];
      const units = () => paths().filter((p) => p.getAttribute("data-role") === "unit");
      expect(units()).toHaveLength(1);
      const total = paths().length;
      const unitSwitch = screen.getByRole("switch", { name: /Formteil/ });
      expect(unitSwitch).toHaveAttribute("aria-checked", "true");

      await userEvent.click(unitSwitch);
      expect(unitSwitch).toHaveAttribute("aria-checked", "false");
      expect(units()).toHaveLength(0);
      expect(paths()).toHaveLength(total - 1);

      // on again, but its kind switch off: still hidden
      await userEvent.click(unitSwitch);
      const kind = units()[0]?.getAttribute("data-kind");
      expect(kind).toBeTruthy();
      const kindName = { added: "neu", removed: "entfällt", unchanged: "unverändert" }[kind ?? ""];
      await userEvent.click(screen.getByRole("switch", { name: new RegExp(kindName ?? "") }));
      expect(units()).toHaveLength(0);
    });

    it("says so when the proposal equals the live mapping", () => {
      setup({ mode: "compare", compareLinks: data.links });
      expect(screen.getByText("Der Vorschlag ändert die Zuordnung nicht.")).toBeInTheDocument();
    });

    it("shows proposed parts as joined tiles", () => {
      setup({
        mode: "compare",
        compareLinks: [
          { elbWordId: 4, stepWordId: 10, isAddedWord: false, partOrder: 1, germanWordPart: "Go" },
          { elbWordId: 4, stepWordId: 12, isAddedWord: false, partOrder: 2, germanWordPart: "tt" },
        ],
      });
      expect(node("g4.1")).toHaveAttribute("data-part", "first");
      expect(node("g4.2")).toHaveAttribute("data-part", "last");
    });

    it("renders the footer instead of the buttons", () => {
      setup({
        mode: "compare",
        compareLinks: proposed,
        footer: <p>Aktionen der Prüfung</p>,
      });
      expect(screen.getByText("Aktionen der Prüfung")).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Abbrechen" })).toBeNull();
    });
  });

  describe("edit from initial links (edit-merge)", () => {
    const initial: VerseMappingLink[] = [
      { elbWordId: 2, stepWordId: 10, isAddedWord: false },
      { elbWordId: 4, stepWordId: 12, isAddedWord: false },
    ];

    it("starts from the initial links, counts changes against the live ones and uses the save label", async () => {
      const { onSave } = setup({ initialLinks: initial, saveLabel: "Bearbeitet übernehmen" });
      expect(badge("g4")).toBe("2");
      expect(badge("g3")).toBe("?");
      expect(node("g4")).toHaveAttribute("data-status", "edited");
      await userEvent.click(screen.getByRole("button", { name: "Bearbeitet übernehmen" }));
      expect(onSave).toHaveBeenCalledWith(
        [
          { elbWordId: 2, stepWordId: 10, isAddedWord: false },
          { elbWordId: 4, stepWordId: 12, isAddedWord: false },
        ],
        { clearParts: false },
      );
    });

    it("saves even when the start equals the live mapping", async () => {
      const { onSave, onCancel } = setup({
        initialLinks: data.links,
        saveLabel: "Bearbeitet übernehmen",
      });
      await userEvent.click(screen.getByRole("button", { name: "Bearbeitet übernehmen" }));
      expect(onSave).toHaveBeenCalledOnce();
      expect(onCancel).not.toHaveBeenCalled();
    });

    it("shows the footer above the buttons", () => {
      setup({ initialLinks: initial, footer: <p>Lemma-Tabelle</p> });
      expect(screen.getByText("Lemma-Tabelle")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Speichern/ })).toBeInTheDocument();
    });
  });

  describe("link roles (0018)", () => {
    // Mk 1:8 "ich habe getauft" / ἐβάπτισα, Mk 1:35 "ging hinaus" / ἐξῆλθεν: before the backfill every link is a head.
    const greek: GetVerseMappingResponse = {
      ...data,
      language: "greek",
      germanWords: [
        { id: 1, positionInVerse: 1, text: "habe" },
        { id: 2, positionInVerse: 2, text: "getauft" },
        { id: 3, positionInVerse: 3, text: "ging" },
        { id: 4, positionInVerse: 4, text: "hinaus" },
        { id: 5, positionInVerse: 5, text: "euch" },
      ],
      originalWords: [
        { id: 10, position: 1, original: "ἐβάπτισα", strongs: "G0907" },
        { id: 11, position: 2, original: "ἐξῆλθεν", strongs: "G1831" },
        { id: 12, position: 3, original: "ὑμᾶς", strongs: "G4771" },
      ],
      links: [
        { elbWordId: 1, stepWordId: 10, isAddedWord: false, role: "head" },
        { elbWordId: 2, stepWordId: 10, isAddedWord: false, role: "head" },
        { elbWordId: 3, stepWordId: 11, isAddedWord: false, role: "head" },
        { elbWordId: 4, stepWordId: 11, isAddedWord: false, role: "head" },
      ],
    };
    const toggle = (german: string) =>
      screen.getByRole("button", { name: new RegExp(`^${german} – .*: Formteil`) });
    const save = () => userEvent.click(screen.getByRole("button", { name: /^Speichern/ }));

    it("sends the legacy payload when no role, morpheme or part lemma was touched", async () => {
      const { onSave } = setup({ data: greek });
      await userEvent.click(node("g5"));
      await userEvent.click(node("o12"));
      await save();
      const links = onSave.mock.calls[0]?.[0] as VerseMappingLink[];
      expect(links).toHaveLength(5);
      for (const link of links) {
        expect(link).not.toHaveProperty("role");
        expect(link).not.toHaveProperty("strongsNumber");
        expect(link).not.toHaveProperty("allowMultiHead");
      }
      expect(screen.queryByText(/ganze Vers geprüft/)).toBeNull();
    });

    it("toggles a link to unit in the chip, mutes it and saves role-aware with a role on every link", async () => {
      const { onSave } = setup({ data: greek });
      await userEvent.click(node("g1"));
      const button = toggle("habe");
      expect(button).toHaveAttribute("aria-pressed", "false");
      expect(button).toHaveTextContent("Kern");
      await userEvent.click(button);
      expect(toggle("habe")).toHaveAttribute("aria-pressed", "true");
      expect(toggle("habe")).toHaveTextContent("Formteil");
      expect(node("g1")).toHaveAttribute("data-unit");
      expect(node("g2")).not.toHaveAttribute("data-unit");
      expect(node("g1")).toHaveAccessibleName("habe, verbunden mit ἐβάπτισα (Formteil)");
      expect(diffItem("Rolle: habe – \u2068ἐβάπτισα\u2069 → Formteil")).toBeInTheDocument();
      // role-aware now: the other morpheme still has two heads; accept them or make one a unit
      expect(screen.getByText(/ἐξῆλθεν hat mehrere Kerne \(ging, hinaus\)/)).toBeInTheDocument();
      await userEvent.click(node("g4"));
      await userEvent.click(toggle("hinaus"));
      expect(screen.queryByText(/hat mehrere Kerne/)).toBeNull();
      await save();
      expect(onSave).toHaveBeenCalledWith(
        [
          { elbWordId: 1, stepWordId: 10, isAddedWord: false, role: "unit" },
          { elbWordId: 2, stepWordId: 10, isAddedWord: false, role: "head" },
          { elbWordId: 3, stepWordId: 11, isAddedWord: false, role: "head" },
          { elbWordId: 4, stepWordId: 11, isAddedWord: false, role: "unit" },
        ],
        { clearParts: false },
      );
    });

    it("toggles with the U key on a selected German tile, and back", async () => {
      setup({ data: greek });
      node("g4").focus();
      await userEvent.keyboard("{Enter}");
      expect(node("g4")).toHaveAttribute("aria-pressed", "true");
      await userEvent.keyboard("u");
      expect(node("g4")).toHaveAttribute("data-unit");
      expect(screen.getByRole("status")).toHaveTextContent(
        "„hinaus“ ist jetzt Formteil von ἐξῆλθεν.",
      );
      await userEvent.keyboard("u");
      expect(node("g4")).not.toHaveAttribute("data-unit");
      // back to the loaded state: nothing to save
      expect(screen.getByRole("button", { name: "Speichern" })).toBeInTheDocument();
    });

    it("offers to accept several heads and sends allowMultiHead", async () => {
      const { onSave } = setup({ data: greek });
      await userEvent.click(node("g1"));
      await userEvent.click(toggle("habe"));
      await userEvent.click(screen.getByRole("button", { name: "Mehrere Kerne zulassen" }));
      expect(screen.getByText(/Auf ἐξῆλθεν sind mehrere Kerne zugelassen/)).toBeInTheDocument();
      await save();
      const links = onSave.mock.calls[0]?.[0] as VerseMappingLink[];
      expect(links.filter((l) => l.allowMultiHead).map((l) => l.elbWordId)).toEqual([3, 4]);
    });

    it("saves role-aware when the loaded verse already has a unit, even for an unrelated edit", async () => {
      const loaded = {
        ...greek,
        links: greek.links.map((l) => (l.elbWordId === 4 ? { ...l, role: "unit" as const } : l)),
      };
      const { onSave } = setup({ data: loaded });
      expect(node("g4")).toHaveAttribute("data-unit");
      await userEvent.click(node("g5"));
      await userEvent.click(node("o12"));
      await save();
      const links = onSave.mock.calls[0]?.[0] as VerseMappingLink[];
      expect(links.map((l) => l.role)).toEqual(["head", "head", "head", "unit", "head"]);
    });

    it("shows the server's validation messages with words instead of ids", () => {
      setup({
        data: greek,
        saveError: "validation",
        saveErrorDetails: [
          "Original word 11: 2 head links (words 3, 4); make all but one 'unit' or set allowMultiHead.",
        ],
      });
      expect(
        screen.getByText(
          "Original word ἐξῆλθεν: 2 head links (words ging, hinaus); make all but one 'unit' or set allowMultiHead.",
        ),
      ).toBeInTheDocument();
    });

    // Gen 1:3 "Und Gott sprach" / וַיֹּאמֶר = H9001 (und) + H0559 (sprechen, main)
    const hebrew: GetVerseMappingResponse = {
      ...data,
      germanWords: [
        { id: 1, positionInVerse: 1, text: "Und" },
        { id: 2, positionInVerse: 2, text: "Gott" },
        { id: 3, positionInVerse: 3, text: "sprach" },
      ],
      originalWords: [
        {
          id: 10,
          position: 1,
          original: "וַיֹּאמֶר",
          strongs: "H0559",
          morphemes: [
            { strongs: "H9001", isMain: false, original: "וַ", gloss: "and" },
            { strongs: "H0559", isMain: true, original: "יֹּאמֶר", gloss: "to say" },
          ],
        },
        { id: 11, position: 2, original: "אֱלֹהִים", strongs: "H0430G" },
      ],
      links: [
        { elbWordId: 1, stepWordId: 10, isAddedWord: false, role: "head" },
        { elbWordId: 2, stepWordId: 11, isAddedWord: false, role: "head" },
        { elbWordId: 3, stepWordId: 10, isAddedWord: false, role: "head" },
      ],
    };

    it("lists the morphemes of a Hebrew link with the main one marked and saves the pick", async () => {
      const { onSave } = setup({ data: hebrew });
      await userEvent.click(node("g1"));
      const picker = screen.getByRole("combobox", { name: "Morphem von וַיֹּאמֶר für Und" });
      expect(picker).toHaveValue("H0559");
      const options = within(picker).getAllByRole("option");
      expect(options.map((o) => o.textContent)).toEqual([
        "וַ · and (H9001)",
        "★ יֹּאמֶר · to say (H0559)",
      ]);
      // both on the main morpheme: two heads once roles are in play
      await userEvent.selectOptions(picker, "H9001");
      expect(
        diffItem("Morphem: Und – \u2068וַיֹּאמֶר\u2069 → \u2068וַ\u2069 · and · H9001"),
      ).toBeInTheDocument();
      expect(screen.queryByText(/hat mehrere Kerne/)).toBeNull();
      await save();
      expect(onSave).toHaveBeenCalledWith(
        [
          {
            elbWordId: 1,
            stepWordId: 10,
            isAddedWord: false,
            role: "head",
            strongsNumber: "H9001",
          },
          { elbWordId: 2, stepWordId: 11, isAddedWord: false, role: "head" },
          { elbWordId: 3, stepWordId: 10, isAddedWord: false, role: "head" },
        ],
        { clearParts: false },
      );
    });

    it("shows no morpheme picker for a Greek link", async () => {
      setup({ data: greek });
      await userEvent.click(node("g1"));
      expect(screen.queryByRole("combobox")).toBeNull();
    });

    const parts: GetVerseMappingResponse = {
      ...data,
      germanWords: [{ id: 2, positionInVerse: 1, text: "Gerstenernte" }],
      originalWords: [
        { id: 10, position: 1, original: "שְׂעֹרִים" },
        { id: 11, position: 2, original: "קָצִיר" },
      ],
      links: [
        {
          elbWordId: 2,
          stepWordId: 10,
          isAddedWord: false,
          partOrder: 1,
          germanWordPart: "Gersten",
          partLemma: "Gerste",
          role: "head",
        },
        {
          elbWordId: 2,
          stepWordId: 11,
          isAddedWord: false,
          partOrder: 2,
          germanWordPart: "ernte",
          role: "head",
        },
      ],
    };

    it("round-trips a part lemma: shown, edited as one undo step, saved", async () => {
      const { onSave } = setup({ data: parts, partsEditable: true });
      await userEvent.click(node("g2.1"));
      expect(screen.getByRole("textbox", { name: "Lemma des Teils „Gersten“" })).toHaveValue(
        "Gerste",
      );
      await userEvent.click(node("g2.2"));
      const field = screen.getByRole("textbox", { name: "Lemma des Teils „ernte“" });
      await userEvent.type(field, "Ernte{Enter}");
      expect(screen.getByText("Lemma von „ernte“: Ernte")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /Rückgängig \(1\)/ })).toBeInTheDocument();
      await save();
      expect(onSave).toHaveBeenCalledWith(
        [
          {
            elbWordId: 2,
            stepWordId: 10,
            isAddedWord: false,
            partOrder: 1,
            germanWordPart: "Gersten",
            role: "head",
            partLemma: "Gerste",
          },
          {
            elbWordId: 2,
            stepWordId: 11,
            isAddedWord: false,
            partOrder: 2,
            germanWordPart: "ernte",
            role: "head",
            partLemma: "Ernte",
          },
        ],
        { clearParts: false },
      );
    });

    it("takes part lemmas in the splitter", async () => {
      const splitData: GetVerseMappingResponse = {
        ...data,
        germanWords: [{ id: 2, positionInVerse: 1, text: "Weizenernte" }],
        originalWords: [
          { id: 10, position: 1, original: "חִטִּים" },
          { id: 11, position: 2, original: "קְצִיר" },
        ],
        links: [{ elbWordId: 2, stepWordId: 10, isAddedWord: false }],
      };
      const { onSave } = setup({ data: splitData, partsEditable: true });
      await userEvent.click(node("g2"));
      await userEvent.click(screen.getByRole("button", { name: "Wort teilen …" }));
      await userEvent.click(document.querySelector('[data-gap="6"]') as HTMLElement);
      await userEvent.type(
        screen.getByRole("textbox", { name: "Lemma des Teils „Weizen“" }),
        "Weizen",
      );
      await userEvent.click(screen.getByRole("button", { name: "Teilen" }));
      await userEvent.click(node("o10"));
      await userEvent.click(node("g2.2"));
      await userEvent.click(node("o11"));
      await save();
      const links = onSave.mock.calls[0]?.[0] as VerseMappingLink[];
      expect(links.map((l) => [l.germanWordPart, l.partLemma, l.role])).toEqual([
        ["Weizen", "Weizen", "head"],
        ["ernte", undefined, "head"],
      ]);
    });
  });
});
