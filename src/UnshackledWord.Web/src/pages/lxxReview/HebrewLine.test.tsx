import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import type { LxxReviewHebrewWord } from "@/lib/maintainer/lxxReview";
import { HebrewLine, hebrewTipHead } from "./HebrewLine";

const word: LxxReviewHebrewWord = {
  id: 501,
  number: 1,
  text: "וַיְהִי",
  strongs: "H1961",
  gloss: "to be",
  morphology: "HC/Vqw3ms",
  morphologyDetail: { code: "HC/Vqw3ms", partOfSpeech: "verb" },
  german: "Und es geschah",
  germanWordIds: [71, 72],
  note: "1) to be\n2) to become",
};

const line = (props: Partial<Parameters<typeof HebrewLine>[0]> = {}) =>
  render(
    <HebrewLine
      words={[word, { id: 502, number: 2, text: "בִּימֵי" }]}
      activeLinks={new Set([501])}
      unitLinks={new Set()}
      enabled
      onToggle={() => {}}
      {...props}
    />,
  );

describe("hebrewTipHead", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("joins Strong's, gloss and the decoded morphology", () => {
    expect(hebrewTipHead(word, i18n.t)).toBe(
      `H1961 · to be · ${i18n.t("bible.grammar.partOfSpeech.verb")}`,
    );
  });

  it("falls back to the code and omits what is missing", () => {
    expect(hebrewTipHead({ id: 1, number: 1, text: "x", morphology: "HNcmpa" }, i18n.t)).toBe(
      "HNcmpa",
    );
    expect(hebrewTipHead({ id: 1, number: 1, text: "x", strongs: "H0853" }, i18n.t)).toBe("H0853");
  });
});

describe("HebrewLine tip", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("opens an LTR card on hover with head, German words and the note, and closes on leave", () => {
    const onTip = vi.fn();
    line({ onTip });
    const button = screen.getByRole("button", { name: /וַיְהִי/ });
    expect(button).not.toHaveAttribute("title");

    fireEvent.mouseEnter(button.parentElement as HTMLElement);
    const tip = screen.getByRole("tooltip");
    expect(tip).toHaveAttribute("dir", "ltr");
    expect(tip).toHaveTextContent("H1961 · to be");
    expect(tip).toHaveTextContent("Deutsch: Und es geschah");
    expect(screen.getByTestId("word-tip-note")).toHaveClass("line-clamp-5");
    expect(button).toHaveAttribute("aria-describedby", tip.id);
    expect(onTip).toHaveBeenLastCalledWith(word);

    fireEvent.mouseLeave(button.parentElement as HTMLElement);
    expect(screen.queryByRole("tooltip")).toBeNull();
    expect(onTip).toHaveBeenLastCalledWith(undefined);
  });

  it("opens on keyboard focus, closes on Escape, and leaves click and aria-pressed alone", () => {
    const onToggle = vi.fn();
    line({ onToggle });
    const button = screen.getByRole("button", { name: /וַיְהִי/ });

    fireEvent.focus(button);
    expect(screen.getByRole("tooltip")).toHaveTextContent("וַיְהִי");
    fireEvent.keyDown(button, { key: "Escape" });
    expect(screen.queryByRole("tooltip")).toBeNull();

    fireEvent.click(button);
    expect(onToggle).toHaveBeenCalledWith(501);
    expect(button).toHaveAttribute("aria-pressed", "true");
  });

  it("shows only the word when there is nothing else", () => {
    line();
    fireEvent.mouseEnter(screen.getByRole("button", { name: /בִּימֵי/ }).parentElement as HTMLElement);
    const tip = screen.getByRole("tooltip");
    expect(tip).not.toHaveTextContent("Deutsch");
    expect(screen.queryByTestId("word-tip-note")).toBeNull();
  });
});
