import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import i18n from "@/i18n";
import { StrongsRenderings } from "./StrongsRenderings";

describe("StrongsRenderings", () => {
  it("lists renderings by count with locale formatted counts", async () => {
    await i18n.changeLanguage("de");
    render(
      <StrongsRenderings
        renderings={[
          { word: "sagen", key: "sagen", count: 1204 },
          { word: "sprechen", key: "sprechen", count: 3912 },
        ]}
      />,
    );
    expect(screen.getByTestId("strongs-renderings")).toHaveTextContent(
      "sprechen (3.912) · sagen (1.204)",
    );
  });

  it("shows multi-lemma keys as they are and the no-own-lemma row muted at the end", async () => {
    await i18n.changeLanguage("de");
    render(
      <StrongsRenderings
        renderings={[
          { word: "", key: "", count: 21, noOwnLemma: true },
          { word: "werden töten", key: "werden töten", count: 5 },
          { word: "töten", key: "töten", count: 40 },
        ]}
      />,
    );
    expect(screen.getByTestId("strongs-renderings")).toHaveTextContent(
      "töten (40) · werden töten (5) · ohne eigenes Lemma (21)",
    );
  });

  it("uses English wording and formatting", async () => {
    await i18n.changeLanguage("en");
    render(
      <StrongsRenderings
        renderings={[
          { word: "sprechen", key: "sprechen", count: 3912 },
          { word: "", key: "", count: 2000, noOwnLemma: true },
        ]}
      />,
    );
    expect(screen.getByTestId("strongs-renderings")).toHaveTextContent(
      "sprechen (3,912) · no own lemma (2,000)",
    );
  });
});
