import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { VerseContext } from "./VerseContext";

const original = [
  { position: 1, text: "Ἐν", isHit: false },
  { position: 5, text: "λόγος", isHit: true },
];
const german = [
  { position: 1, text: "Im", isHit: false },
  { position: 4, text: "Wort", isHit: true },
];

function renderVerse(lang: "grc" | "he" = "grc") {
  return render(
    <MemoryRouter>
      <VerseContext
        reference="Joh 1,1"
        href="/bible/43/1#v1"
        lang={lang}
        original={original}
        german={german}
      />
    </MemoryRouter>,
  );
}

describe("VerseContext", () => {
  it("links the reference into the reader", () => {
    renderVerse();
    expect(screen.getByRole("link", { name: "Joh 1,1" })).toHaveAttribute("href", "/bible/43/1#v1");
  });

  it("marks the hit words in both lines", () => {
    renderVerse();
    expect(screen.getByText("λόγος").tagName).toBe("MARK");
    expect(screen.getByText("Wort").tagName).toBe("MARK");
    expect(screen.getByText("Ἐν").tagName).not.toBe("MARK");
  });

  it("sets the script and direction of a Hebrew line", () => {
    const { container } = renderVerse("he");
    const line = container.querySelector('p[lang="he"]');
    expect(line).toHaveAttribute("dir", "rtl");
  });
});
