import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { StrengthBar } from "./StrengthBar";

describe("StrengthBar", () => {
  it("names a move up by whole blocks", () => {
    render(<StrengthBar before={2} after={3} />);
    expect(screen.getByRole("img")).toHaveAccessibleName("Stärke 3 von 5, Stärker geworden");
    expect(screen.getByText("Stärker geworden")).toBeInTheDocument();
  });

  it("holds when the whole blocks stay the same", () => {
    render(<StrengthBar before={2.2} after={2.8} />);
    expect(screen.getByRole("img")).toHaveAccessibleName("Stärke 2 von 5, Gehalten");
  });

  it("names a drop and says that the word comes back", () => {
    render(<StrengthBar before={2} after={1} />);
    expect(screen.getByRole("img")).toHaveAccessibleName(
      "Stärke 1 von 5, Kommt in dieser Sitzung wieder",
    );
  });
});
