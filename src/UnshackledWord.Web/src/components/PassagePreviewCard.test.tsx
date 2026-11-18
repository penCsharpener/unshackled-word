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

  it("shows loading and error states and applies the position", () => {
    const { rerender } = render(<PassagePreviewCard position={{ x: 4, y: 8 }} loading />);
    expect(screen.getByRole("tooltip")).toHaveTextContent(i18n.t("bible.sidePanel.previewLoading"));
    expect(screen.getByRole("tooltip").style.left).toBe("4px");
    rerender(<PassagePreviewCard position={{ x: 4, y: 8, above: true }} error />);
    expect(screen.getByRole("tooltip")).toHaveTextContent(i18n.t("bible.sidePanel.previewError"));
  });
});
