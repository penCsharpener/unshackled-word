import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import i18n from "@/i18n";
import { AboutPage } from "./AboutPage";

describe("AboutPage", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("shows purpose, features, roles, data quality and how to join", () => {
    render(<AboutPage />);
    for (const name of [
      "Worum es geht",
      "Was du hier findest",
      "Rollen",
      "Wie verlässlich die Daten sind",
      "Mitmachen",
    ]) {
      expect(screen.getByRole("heading", { level: 2, name })).toBeTruthy();
    }
    expect(screen.getByText(/kann Fehler enthalten/)).toBeTruthy();
  });
});
