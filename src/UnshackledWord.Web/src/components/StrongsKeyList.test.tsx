import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { StrongsKeyList } from "./StrongsKeyList";

function renderList(keys: { key: string; occurrences: number }[], onMove = vi.fn()) {
  return render(
    <MemoryRouter>
      <StrongsKeyList keys={keys} onMove={onMove} />
    </MemoryRouter>,
  );
}

beforeEach(() => i18n.changeLanguage("en"));

describe("StrongsKeyList", () => {
  it("links each key to its Strong's page and shows its occurrences", () => {
    renderList([{ key: "G3319", occurrences: 58 }]);
    expect(screen.getByRole("link", { name: "G3319" })).toHaveAttribute(
      "href",
      "/bible/strongs/G3319",
    );
    expect(screen.getByText("58 occurrences")).toBeInTheDocument();
  });

  it("passes the key of the pressed move button to onMove", async () => {
    const onMove = vi.fn();
    renderList(
      [
        { key: "G3319", occurrences: 58 },
        { key: "G3588", occurrences: 4 },
      ],
      onMove,
    );
    const row = screen.getByRole("link", { name: "G3588" }).closest("li") as HTMLElement;
    await userEvent.click(within(row).getByRole("button", { name: "Move…" }));
    expect(onMove).toHaveBeenCalledWith("G3588");
  });

  it("says so when the entry has no keys", () => {
    renderList([]);
    expect(screen.getByText("No Strong's number.")).toBeInTheDocument();
    expect(screen.queryByRole("list")).toBeNull();
  });
});
