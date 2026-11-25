import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import i18n from "@/i18n";
import type { GreekEdition, ReadingEditions } from "@/lib/bible/types";
import { EditionList } from "./EditionList";

const main = ["NA28", "NA27", "Tyn", "SBL", "WH", "Treg", "TR", "Byz"];

/** Hand-written catalog rows (same shape as `GET api/greekEditions`). */
const catalog: GreekEdition[] = [
  { code: "NA28", name: "Nestle-Aland, 28. Auflage", year: 2012, kind: "edition", isMain: true },
  { code: "WH", name: "Westcott-Hort", year: 1881, kind: "edition", isMain: true },
  { code: "TR", name: "Textus Receptus (Scrivener)", year: 1894, kind: "edition", isMain: true },
  { code: "01", name: "Sinaiticus", kind: "manuscript", isMain: false },
];

const edition = (code: string) => ({ code, kind: "edition" as const });

const lacksWhTreg: ReadingEditions = {
  present: main.filter((c) => c !== "WH" && c !== "Treg").map(edition),
  missing: [edition("WH"), edition("Treg")],
};

const trOnly: ReadingEditions = {
  present: [edition("TR"), edition("Byz"), { code: "01", kind: "manuscript" }],
  missing: main.filter((c) => c !== "TR" && c !== "Byz").map(edition),
};

beforeEach(async () => {
  await i18n.changeLanguage("de");
});
afterEach(async () => {
  await i18n.changeLanguage("de");
});

describe("EditionList", () => {
  it("lists present editions and the missing ones, struck through", () => {
    render(<EditionList editions={lacksWhTreg} catalog={catalog} />);
    expect(screen.getByRole("region", { name: "Textausgaben" })).toBeInTheDocument();
    expect(screen.getByText("fehlt in:")).toBeInTheDocument();
    const missing = screen.getByRole("list", { name: "Ausgaben ohne dieses Wort" });
    expect(within(missing).getAllByRole("button")).toHaveLength(2);
    expect(within(missing).getByText("WH").tagName).toBe("S");
    const present = screen.getByRole("list", { name: "Ausgaben mit diesem Wort" });
    expect(within(present).getAllByRole("button")).toHaveLength(6);
    expect(screen.queryByText("nur in:")).not.toBeInTheDocument();
  });

  it("says 'nur in' for a minority word and shows non-main tokens with their kind", () => {
    render(<EditionList editions={trOnly} catalog={catalog} />);
    expect(screen.getByText("nur in:")).toBeInTheDocument();
    const others = screen.getByRole("list", { name: "Weitere Zeugen mit diesem Wort" });
    expect(within(others).getByRole("button")).toHaveTextContent("01 · Handschrift");
  });

  it("shows name and year on hover, focus and tap", async () => {
    const user = userEvent.setup();
    render(<EditionList editions={trOnly} catalog={catalog} />);
    const tr = screen.getByRole("button", { name: /^TR/ });
    await user.hover(tr);
    expect(screen.getByText("TR: Textus Receptus (Scrivener), 1894")).toBeInTheDocument();
    await user.unhover(tr);
    expect(screen.queryByText(/Textus Receptus/)).not.toBeInTheDocument();

    await user.click(tr);
    expect(tr).toHaveAccessibleDescription("TR: Textus Receptus (Scrivener), 1894");
    expect(tr).toHaveAttribute("aria-pressed", "true");

    await user.tab();
    expect(screen.getByText("Byz: Byzantinischer Text (Robinson-Pierpont)")).toBeInTheDocument();
  });

  it("describes shifts and cross-verse placement", () => {
    render(
      <EditionList
        editions={{
          present: [
            { code: "NA28", kind: "edition", shift: 1 },
            { code: "TR", kind: "edition", shift: -2 },
            { code: "Byz", kind: "edition", shiftRaw: "«14.24" },
            { code: "WH", kind: "edition", shiftRaw: "»1»1" },
          ],
          missing: [],
        }}
        catalog={catalog}
      />,
    );
    expect(screen.getByText("1 Wort später")).toBeInTheDocument();
    expect(screen.getByText("2 Wörter früher")).toBeInTheDocument();
    expect(screen.getByText("andere Stelle (14,24)")).toBeInTheDocument();
    expect(screen.getByText("andere Wortstellung (»1»1)")).toBeInTheDocument();
    expect(screen.queryByText("fehlt in:")).not.toBeInTheDocument();
  });

  it("uses English names and wording from i18n", async () => {
    await i18n.changeLanguage("en");
    const user = userEvent.setup();
    render(
      <EditionList
        editions={{
          present: [{ code: "TR", kind: "edition", shift: 2 }],
          missing: [edition("NA28")],
        }}
        catalog={catalog}
      />,
    );
    expect(screen.getByText("2 words later")).toBeInTheDocument();
    expect(screen.getByText("missing in:")).toBeInTheDocument();
    await user.hover(screen.getByRole("button", { name: /NA28/ }));
    expect(screen.getByText("NA28: Nestle-Aland, 28th edition, 2012")).toBeInTheDocument();
  });

  it("works without a catalog and explains what an edition is", async () => {
    const user = userEvent.setup();
    render(<EditionList editions={{ present: [{ code: "X9", kind: "unknown" }], missing: [] }} />);
    await user.hover(screen.getByRole("button", { name: /X9/ }));
    expect(screen.getByText("X9: nicht im Katalog, unbekannt")).toBeInTheDocument();
    await user.click(screen.getByText("Textausgaben"));
    expect(screen.getByText(/gedruckter Grundtext, keine einzelne Handschrift/)).toBeVisible();
  });
});
