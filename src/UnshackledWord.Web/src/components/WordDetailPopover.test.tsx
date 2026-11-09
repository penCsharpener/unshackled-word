import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { GetWordDetailResponse, OriginalReading, WordToken } from "@/lib/bible/types";
import { mockWordDetail } from "@/lib/bible/wordDetail.mock";
import { WordDetailPopover, type WordDetailPopoverProps } from "./WordDetailPopover";

const word: WordToken = { id: 1, text: "Wort", senses: [] };

function setup(props: Partial<WordDetailPopoverProps> = {}) {
  const onClose = vi.fn();
  const onRetry = vi.fn();
  render(
    <WordDetailPopover
      id="pop"
      word={word}
      status="success"
      onRetry={onRetry}
      onClose={onClose}
      {...props}
    />,
  );
  return { onClose, onRetry };
}

const reading = (over: Partial<OriginalReading>): OriginalReading => ({
  stepWordId: 1,
  language: "greek",
  positionInVerse: 1,
  original: "x",
  strongs: "G1",
  ...over,
});

beforeEach(async () => {
  window.HTMLElement.prototype.showPopover ??= vi.fn();
  await i18n.changeLanguage("de");
});

describe("WordDetailPopover", () => {
  it("shows loading state, named by the word", () => {
    setup({ status: "pending" });
    expect(screen.getByText("Grundtext wird geladen …")).toHaveAttribute("aria-busy", "true");
    expect(screen.getByRole("dialog", { name: /Grundtext zu „Wort“/ })).toBeInTheDocument();
  });

  it("shows an error and retries", async () => {
    const { onRetry } = setup({ status: "error" });
    expect(screen.getByRole("alert")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Erneut versuchen" }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("shows empty states without attribution", () => {
    setup({ detail: { wordId: 1, wordInContext: "Wort", positionInVerse: 1, readings: [] } });
    expect(
      screen.getByText("Dieses Wort ist noch keinem Grundtextwort zugeordnet."),
    ).toBeInTheDocument();
    expect(screen.queryByText(/STEPBible/)).toBeNull();
  });

  it("shows the added-word state", () => {
    setup({
      detail: {
        wordId: 1,
        wordInContext: "Wort",
        positionInVerse: 1,
        isAddedWord: true,
        readings: [],
      },
    });
    expect(screen.getByText(/der Übersetzer hat es ergänzt/)).toBeInTheDocument();
  });

  it("shows a Greek reading with decoded morphology and attribution", async () => {
    const detail = await mockWordDetail(
      { id: 1, text: "Wort", senses: [{ key: "G3056", form: "λόγος" }] },
      0,
    );
    setup({ detail });
    expect(screen.getByText("λόγος", { selector: "p" })).toHaveAttribute("lang", "grc");
    expect(screen.getByText("G3056")).toBeInTheDocument();
    expect(screen.getByText("Substantiv · Nominativ · Singular · maskulin")).toBeInTheDocument();
    expect(screen.getByText("N-NSM")).toBeInTheDocument();
    expect(screen.getByText(/STEPBible\.org/)).toBeInTheDocument();
  });

  it("shows several readings with word parts in order", () => {
    const detail: GetWordDetailResponse = {
      wordId: 1,
      wordInContext: "Menschensohn",
      positionInVerse: 1,
      readings: [
        reading({
          stepWordId: 2,
          positionInVerse: 2,
          original: "υἱός",
          strongs: "G5207",
          germanWordPart: "sohn",
        }),
        reading({
          stepWordId: 1,
          positionInVerse: 1,
          original: "ἄνθρωπος",
          strongs: "G444",
          germanWordPart: "Menschen",
        }),
      ],
    };
    setup({ detail });
    expect(screen.getByText("2 Grundtextwörter")).toBeInTheDocument();
    expect(screen.getByText("Wortteil „Menschen“")).toBeInTheDocument();
    expect(screen.getByText("Wortteil „sohn“")).toBeInTheDocument();
  });

  it("shows Hebrew right-to-left with morphemes", async () => {
    const detail = await mockWordDetail(
      { id: 1, text: "Anfang", senses: [{ key: "H7225G", form: "בְּרֵאשִׁית" }] },
      0,
    );
    setup({ detail });
    const original = screen.getByText("בְּרֵאשִׁית", { selector: "p" });
    expect(original).toHaveAttribute("dir", "rtl");
    expect(original).toHaveAttribute("lang", "he");
    expect(screen.getByText("Präfix")).toBeInTheDocument();
    expect(screen.getByText("Wortstamm")).toBeInTheDocument();
  });

  it("closes via Escape, button and outside press, but not on a word trigger", async () => {
    const { onClose } = setup({
      detail: { wordId: 1, wordInContext: "Wort", positionInVerse: 1, readings: [] },
    });
    await userEvent.keyboard("{Escape}");
    expect(onClose).toHaveBeenLastCalledWith("escape");
    await userEvent.click(screen.getByRole("button", { name: "Schließen" }));
    expect(onClose).toHaveBeenLastCalledWith("button");
    fireEvent.pointerDown(document.body);
    expect(onClose).toHaveBeenLastCalledWith("outside");
    onClose.mockClear();
    const trigger = document.createElement("span");
    trigger.setAttribute("data-word-trigger", "");
    document.body.appendChild(trigger);
    fireEvent.pointerDown(trigger);
    expect(onClose).not.toHaveBeenCalled();
    trigger.remove();
  });

  it("focuses the dialog on mount", () => {
    setup();
    expect(screen.getByRole("dialog")).toHaveFocus();
  });

  it("shows the sample badge", () => {
    setup({ isSample: true });
    expect(screen.getByText("Beispieldaten")).toBeInTheDocument();
  });

  it("is translated to English", async () => {
    await i18n.changeLanguage("en");
    const detail = await mockWordDetail(
      { id: 1, text: "Wort", senses: [{ key: "G3056", form: "λόγος" }] },
      0,
    );
    setup({ detail });
    expect(screen.getByText("Grammar")).toBeInTheDocument();
    expect(screen.getByText("noun · nominative · singular · masculine")).toBeInTheDocument();
  });

  describe("mapping provenance and phrase", () => {
    const base: GetWordDetailResponse = {
      wordId: 3,
      wordInContext: "Erde",
      positionInVerse: 3,
      readings: [reading({ language: "hebrew", original: "וְהָאָרֶץ", strongs: "H776G" })],
    };

    it("shows the AI badge, date and model, and a disabled edit action", () => {
      setup({
        detail: {
          ...base,
          mapping: {
            source: "ai",
            model: "gemini-test",
            createdAt: "2026-03-14T10:00:00Z",
          },
        },
      });
      expect(screen.getByText("KI-Vorschlag")).toBeInTheDocument();
      expect(screen.getByText(/Zuordnung von der KI am 14\.03\.2026/)).toBeInTheDocument();
      expect(screen.getByText(/gemini-test/)).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Zuordnung bearbeiten" })).toBeDisabled();
      expect(screen.getByText("Kommt mit dem Tagging-Werkzeug.")).toBeInTheDocument();
    });

    it("shows confirmed and manual badges", () => {
      const confirmed = {
        source: "ai" as const,
        createdAt: "2026-03-14T10:00:00Z",
        confirmedAt: "2026-10-02T08:30:00Z",
      };
      const { unmount } = render(
        <WordDetailPopover
          id="a"
          word={word}
          status="success"
          detail={{ ...base, mapping: confirmed }}
          onRetry={vi.fn()}
          onClose={vi.fn()}
        />,
      );
      expect(screen.getByText("Bestätigt")).toBeInTheDocument();
      expect(screen.getByText(/bestätigt am 02\.10\.2026/)).toBeInTheDocument();
      unmount();
      setup({
        detail: { ...base, mapping: { source: "human", createdAt: "2026-10-02T08:30:00Z" } },
      });
      expect(screen.getByText("Manuell")).toBeInTheDocument();
      expect(screen.getByText(/Zuordnung von Hand/)).toBeInTheDocument();
    });

    it("enables the edit action when a handler is given", async () => {
      const onEditMapping = vi.fn();
      setup({ detail: base, onEditMapping });
      await userEvent.click(screen.getByRole("button", { name: "Zuordnung bearbeiten" }));
      expect(onEditMapping).toHaveBeenCalledOnce();
      expect(screen.queryByText("Kommt mit dem Tagging-Werkzeug.")).toBeNull();
    });

    it("shows the words translated together in verse order", () => {
      setup({
        detail: {
          ...base,
          coMappedWords: [
            { wordId: 1, wordInContext: "Und", positionInVerse: 1, relation: "sameOriginal" },
            { wordId: 2, wordInContext: "die", positionInVerse: 2, relation: "sameOriginal" },
          ],
        },
      });
      const line = screen.getByText("Gemeinsam übersetzt:");
      expect(line.textContent).toBe("Gemeinsam übersetzt: Und die Erde");
      expect(screen.getByText("Erde", { selector: "strong" })).toBeInTheDocument();
    });

    it("names the parent of an added word", () => {
      setup({
        detail: {
          wordId: 1,
          wordInContext: "Im",
          positionInVerse: 1,
          isAddedWord: true,
          readings: [],
          coMappedWords: [
            { wordId: 2, wordInContext: "Anfang", positionInVerse: 2, relation: "parent" },
          ],
        },
      });
      expect(screen.getByText(/der Übersetzer hat es zu „Anfang“ ergänzt/)).toBeInTheDocument();
    });

    it("shows hints and shared words per reading when there are several", () => {
      setup({
        detail: {
          wordId: 5,
          wordInContext: "Der",
          positionInVerse: 1,
          hints: ["manyOriginals"],
          coMappedWords: [
            {
              wordId: 6,
              wordInContext: "Hohepriester",
              positionInVerse: 2,
              relation: "sameOriginal",
            },
          ],
          readings: [
            reading({ stepWordId: 1, positionInVerse: 1, original: "ὁ", sharedWithWordIds: [6] }),
            reading({ stepWordId: 2, positionInVerse: 5, original: "τὸν", strongs: "G3588" }),
            reading({ stepWordId: 3, positionInVerse: 8, original: "τῶν", strongs: "G3588" }),
          ],
        },
      });
      expect(screen.getByRole("note")).toHaveTextContent(/Ungewöhnlich viele Grundtextwörter/);
      expect(screen.getByText("gemeinsam mit „Hohepriester“")).toBeInTheDocument();
    });

    it("is translated to English", async () => {
      await i18n.changeLanguage("en");
      setup({ detail: { ...base, mapping: { source: "ai", createdAt: "2026-03-14T10:00:00Z" } } });
      expect(screen.getByText("AI suggestion")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Edit mapping" })).toBeDisabled();
    });
  });
});
