import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { StudyCard } from "./StudyCard";

describe("StudyCard", () => {
  it("shows the kind label above the card content", () => {
    render(
      <StudyCard kindLabel="Neues Wort">
        <p>Inhalt der Karte</p>
      </StudyCard>,
    );
    expect(screen.getByText("Neues Wort")).toBeInTheDocument();
    expect(screen.getByText("Inhalt der Karte")).toBeInTheDocument();
  });
});
