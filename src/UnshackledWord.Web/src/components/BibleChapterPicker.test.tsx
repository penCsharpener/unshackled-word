import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it } from "vitest";
import i18n from "@/i18n";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { BibleChapterPicker, type BibleChapterPickerProps } from "./BibleChapterPicker";

function setup(props: Partial<BibleChapterPickerProps> = {}) {
  return render(
    <MemoryRouter>
      <BibleChapterPicker books={fixtureBooks} {...props} />
    </MemoryRouter>,
  );
}

const ot = () => screen.getByRole("button", { name: "Altes Testament" });
const nt = () => screen.getByRole("button", { name: "Neues Testament" });

describe("BibleChapterPicker", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("is collapsed by default", () => {
    setup();
    expect(ot()).toHaveAttribute("aria-expanded", "false");
    expect(nt()).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("region")).not.toBeInTheDocument();
  });

  it("disables the bar while books are loading", () => {
    setup({ books: undefined });
    expect(ot()).toBeDisabled();
    expect(nt()).toBeDisabled();
  });

  it("shows a message when there are no books", () => {
    setup({ books: [] });
    expect(screen.getByText("Keine Bücher verfügbar.")).toBeInTheDocument();
  });

  it("opens, switches and collapses testaments", async () => {
    setup();
    await userEvent.click(ot());
    const region = screen.getByRole("region", { name: "Altes Testament" });
    expect(within(region).getAllByRole("heading", { name: /1\. Mose/ }).length).toBeGreaterThan(0);
    expect(within(region).getByRole("link", { name: "1. Mose 1" })).toHaveAttribute(
      "href",
      "/bible/1/1",
    );
    expect(screen.queryByRole("heading", { name: /Matthäus/ })).not.toBeInTheDocument();

    await userEvent.click(nt());
    expect(screen.getByRole("region", { name: "Neues Testament" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: /1\. Mose/ })).not.toBeInTheDocument();

    await userEvent.click(nt());
    expect(screen.queryByRole("region")).not.toBeInTheDocument();
  });

  it("splits the Psalms into five books", async () => {
    setup();
    await userEvent.click(ot());
    for (const title of [
      "Psalmen, Buch 1 (1–41)",
      "Psalmen, Buch 2 (42–72)",
      "Psalmen, Buch 3 (73–89)",
      "Psalmen, Buch 4 (90–106)",
      "Psalmen, Buch 5 (107–150)",
    ]) {
      expect(
        screen.getAllByRole("heading", { name: new RegExp(title.replace(/[()]/g, "\\$&")) }).length,
      ).toBeGreaterThan(0);
    }
    expect(screen.getByRole("link", { name: "Psalmen 42" })).toHaveAttribute(
      "href",
      "/bible/19/42",
    );
  });

  it("marks the current chapter", () => {
    setup({ currentBookId: 40, currentChapter: 5, defaultTestament: "nt" });
    expect(screen.getByRole("link", { name: "Matthäus 5" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(screen.getByRole("link", { name: "Matthäus 6" })).not.toHaveAttribute("aria-current");
  });

  it("renders open with defaultTestament", () => {
    setup({ defaultTestament: "ot" });
    expect(ot()).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("region", { name: "Altes Testament" })).toBeInTheDocument();
  });

  it("closes on Escape and focuses the testament button", async () => {
    setup();
    await userEvent.click(ot());
    await userEvent.tab();
    expect(ot()).not.toHaveFocus();
    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("region")).not.toBeInTheDocument();
    expect(ot()).toHaveFocus();
  });

  it("collapses after choosing a chapter", async () => {
    setup();
    await userEvent.click(ot());
    await userEvent.click(screen.getByRole("link", { name: "1. Mose 3" }));
    expect(screen.queryByRole("region")).not.toBeInTheDocument();
  });

  it("uses English names when the UI language is English", async () => {
    await i18n.changeLanguage("en");
    setup({ defaultTestament: "ot" });
    expect(screen.getAllByRole("heading", { name: /Genesis/ }).length).toBeGreaterThan(0);
  });
});
