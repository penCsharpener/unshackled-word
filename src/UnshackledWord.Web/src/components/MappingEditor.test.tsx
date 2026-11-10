import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { GetVerseMappingResponse } from "@/lib/bible/types";
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
      expect(screen.getByText("Verbunden: Gott – אֱלֹהִים")).toBeInTheDocument();
      await userEvent.click(screen.getByRole("button", { name: "Speichern (1)" }));
      expect(onSave).toHaveBeenCalledWith([
        { elbWordId: 2, stepWordId: 10, isAddedWord: false },
        { elbWordId: 3, stepWordId: 11, isAddedWord: false },
        { elbWordId: 4, stepWordId: 12, isAddedWord: false },
        { elbWordId: 1, isAddedWord: true, parentElbWordId: 2 },
      ]);
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
      expect(screen.getByText("Gelöst: schuf – בָּרָא")).toBeInTheDocument();
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

    it("shows the disabled AI placeholder with its hint", () => {
      setup();
      const redo = screen.getByRole("button", {
        name: "KI neu zuordnen (dieser Vers / Versbereich)",
      });
      expect(redo).toBeDisabled();
      expect(redo).toHaveAccessibleDescription(/Kommt später/);
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
});
