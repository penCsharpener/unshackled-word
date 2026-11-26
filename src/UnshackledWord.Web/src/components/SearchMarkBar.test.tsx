import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { SearchMarkBar, type SearchMarkBarProps } from "./SearchMarkBar";

function renderBar(props: Partial<SearchMarkBarProps> = {}) {
  const handlers = { onNavigate: vi.fn(), onOpen: vi.fn(), onOff: vi.fn() };
  render(
    <SearchMarkBar query="agape Kasus:nom" status="hits" count={1} {...handlers} {...props} />,
  );
  return handlers;
}

beforeEach(async () => {
  await i18n.changeLanguage("de");
});

describe("SearchMarkBar", () => {
  it("shows the query and the hit count", () => {
    renderBar();
    expect(screen.getByText("Markiert:")).toBeInTheDocument();
    expect(screen.getByText("agape Kasus:nom")).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("1 Treffer");
  });

  it("disables an arrow without a chapter and navigates with the other", async () => {
    const { onNavigate } = renderBar({ next: "Johannes 17" });
    expect(
      screen.getByRole("button", { name: "Kein vorheriges Kapitel mit Treffer" }),
    ).toBeDisabled();
    await userEvent.click(
      screen.getByRole("button", { name: "Nächstes Kapitel mit Treffer: Johannes 17" }),
    );
    expect(onNavigate).toHaveBeenCalledWith("next");
  });

  it("says when the chapter has no hits and keeps the arrows", () => {
    renderBar({ status: "none", count: 0, previous: "Matthäus 22", next: "Johannes 17" });
    expect(screen.getByRole("status")).toHaveTextContent("Keine Treffer in diesem Kapitel");
    expect(
      screen.getByRole("button", { name: "Vorheriges Kapitel mit Treffer: Matthäus 22" }),
    ).toBeEnabled();
  });

  it("opens the search from the query and turns marking off with ×", async () => {
    const { onOpen, onOff } = renderBar();
    await userEvent.click(screen.getByRole("button", { name: /agape Kasus:nom/ }));
    expect(onOpen).toHaveBeenCalled();
    await userEvent.click(screen.getByRole("button", { name: "Markierung ausschalten" }));
    expect(onOff).toHaveBeenCalled();
  });

  it("shows an incomplete query without count and arrows", async () => {
    const { onOpen } = renderBar({ status: "incomplete" });
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Kapitel mit Treffer/ })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Suche unvollständig" }));
    expect(onOpen).toHaveBeenCalled();
  });

  it("uses the plural in English", async () => {
    await i18n.changeLanguage("en");
    renderBar({ count: 3 });
    expect(screen.getByRole("status")).toHaveTextContent("3 hits");
  });
});
