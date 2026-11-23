import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import i18n from "@/i18n";
import { PassagePreviewCard } from "./PassagePreviewCard";

describe("PassagePreviewCard", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("shows one verse without a number and a range with numbers", () => {
    const { rerender } = render(
      <PassagePreviewCard position={{ x: 4, y: 8 }} verses={[{ verse: 16, text: "Denn also" }]} />,
    );
    expect(screen.getByRole("tooltip")).toHaveTextContent("Denn also");
    expect(document.querySelector("sup")).toBeNull();
    rerender(
      <PassagePreviewCard
        position={{ x: 4, y: 8 }}
        title="Joh 3,16–17"
        verses={[
          { verse: 16, text: "Denn also" },
          { verse: 17, text: "Denn Gott" },
        ]}
      />,
    );
    expect(screen.getByText("Joh 3,16–17")).toBeInTheDocument();
    expect(document.querySelectorAll("sup")).toHaveLength(2);
  });

  it("shows a curator comment as a muted line under the texts", () => {
    const { rerender } = render(
      <PassagePreviewCard
        position={{ x: 4, y: 8 }}
        verses={[{ verse: 6, text: "Durch des HERRN Wort" }]}
        comment="Schöpfung durch das Wort"
      />,
    );
    const comment = screen.getByTestId("preview-comment");
    expect(comment).toHaveTextContent("Schöpfung durch das Wort");
    expect(comment.className).toContain("text-fg-muted");
    expect(screen.getByText("Durch des HERRN Wort").compareDocumentPosition(comment)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
    rerender(
      <PassagePreviewCard position={{ x: 4, y: 8 }} verses={[{ verse: 6, text: "Durch" }]} />,
    );
    expect(screen.queryByTestId("preview-comment")).toBeNull();
  });

  it("shows loading and error states and applies the position", () => {
    const { rerender } = render(<PassagePreviewCard position={{ x: 4, y: 8 }} loading />);
    expect(screen.getByRole("tooltip")).toHaveTextContent(i18n.t("bible.sidePanel.previewLoading"));
    expect(screen.getByRole("tooltip").style.left).toBe("4px");
    rerender(<PassagePreviewCard position={{ x: 4, y: 8, above: true }} error />);
    expect(screen.getByRole("tooltip")).toHaveTextContent(i18n.t("bible.sidePanel.previewError"));
  });

  it("looks as before without originals: narrow, no columns", () => {
    render(
      <PassagePreviewCard
        position={{ x: 4, y: 8 }}
        verses={[
          { verse: 16, text: "Denn also" },
          { verse: 17, text: "Denn Gott" },
        ]}
      />,
    );
    const card = screen.getByRole("tooltip");
    expect(card).toHaveClass("w-80");
    expect(card.className).not.toContain("md:w-160");
    expect(screen.queryByTestId("preview-columns")).toBeNull();
    expect(card.querySelector("[lang]")).toBeNull();
  });

  it("shows Hebrew right to left in a second column from md, one row per verse with numbers on both sides", () => {
    render(
      <PassagePreviewCard
        position={{ x: 4, y: 8 }}
        verses={[
          {
            verse: 22,
            text: "Jehova besaß mich",
            original: { language: "hebrew", text: "יְהוָה קָנָנִי" },
          },
          {
            verse: 23,
            text: "Ich war eingesetzt",
            original: { language: "hebrew", text: "מֵעוֹלָם נִסַּכְתִּי" },
          },
          { verse: 24, text: "Ich war geboren", original: { language: "aramaic", text: "בְּאֵין" } },
        ]}
      />,
    );
    const card = screen.getByRole("tooltip");
    expect(card).toHaveClass("w-80", "md:w-160");
    const columns = screen.getByTestId("preview-columns");
    // stacked below md, two columns from md
    expect(columns).toHaveClass("grid-cols-1", "md:grid-cols-2");
    const cells = Array.from(columns.children);
    expect(cells.map((c) => c.getAttribute("data-column"))).toEqual([
      "german",
      "original",
      "german",
      "original",
      "german",
      "original",
    ]);
    const hebrew = screen.getByText("יְהוָה קָנָנִי");
    expect(hebrew).toHaveAttribute("lang", "he");
    expect(hebrew).toHaveAttribute("dir", "rtl");
    expect(hebrew).toHaveClass("font-original");
    expect(cells[1]).toHaveAttribute("dir", "rtl");
    expect(screen.getByText("בְּאֵין")).toHaveAttribute("lang", "arc");
    // verse numbers in both columns
    expect(card.querySelectorAll("sup")).toHaveLength(6);
    expect(cells[0]?.querySelector("sup")).toHaveTextContent("22");
    expect(cells[1]?.querySelector("sup")).toHaveTextContent("22");
    expect(cells[5]?.querySelector("sup")).toHaveTextContent("24");
  });

  it("shows Greek left to right with lang grc and keeps rows aligned when a verse has no original", () => {
    render(
      <PassagePreviewCard
        position={{ x: 4, y: 8 }}
        verses={[
          {
            verse: 1,
            text: "Im Anfang war das Wort",
            original: { language: "greek", text: "Ἐν ἀρχῇ ἦν ὁ λόγος" },
          },
          { verse: 2, text: "Dieses war im Anfang" },
        ]}
      />,
    );
    const greek = screen.getByText("Ἐν ἀρχῇ ἦν ὁ λόγος");
    expect(greek).toHaveAttribute("lang", "grc");
    expect(greek).toHaveAttribute("dir", "ltr");
    const cells = Array.from(screen.getByTestId("preview-columns").children);
    expect(cells).toHaveLength(4);
    expect(cells[1]).not.toHaveAttribute("dir");
    expect(cells[3]).toBeEmptyDOMElement();
  });
});
