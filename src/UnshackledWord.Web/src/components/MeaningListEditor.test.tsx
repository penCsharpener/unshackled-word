import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { MeaningListEditor, type MeaningRow } from "./MeaningListEditor";

const mitte: MeaningRow = {
  id: 101,
  german: "Mitte",
  count: 58,
  share: 24.1,
  isAccepted: true,
  origin: "derived",
  isPinned: false,
};

const inmitten: MeaningRow = {
  id: 102,
  german: "inmitten",
  count: null,
  share: null,
  isAccepted: true,
  origin: "manual",
  isPinned: false,
};

function itemWith(text: string) {
  const item = screen.getAllByRole("listitem").find((li) => li.textContent?.includes(text));
  if (!item) throw new Error(`no meaning ${text}`);
  return item;
}

beforeEach(() => i18n.changeLanguage("en"));

describe("MeaningListEditor", () => {
  it("lists the meanings in their order with origin and derivation numbers", () => {
    render(<MeaningListEditor rows={[mitte, inmitten]} onChange={vi.fn()} />);
    const items = screen.getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent("Mitte");
    expect(items[1]).toHaveTextContent("inmitten");
    expect(screen.getByText("derived · 58× · 24.1 %")).toBeInTheDocument();
    expect(screen.getByText("manual")).toBeInTheDocument();
  });

  it("moves a meaning down by passing the whole list to onChange", async () => {
    const onChange = vi.fn();
    render(<MeaningListEditor rows={[mitte, inmitten]} onChange={onChange} />);
    await userEvent.click(within(itemWith("Mitte")).getByRole("button", { name: "Move down" }));
    expect(onChange).toHaveBeenCalledWith([inmitten, mitte]);
  });

  it("disables moving the first row up and the last row down", () => {
    render(<MeaningListEditor rows={[mitte, inmitten]} onChange={vi.fn()} />);
    expect(within(itemWith("Mitte")).getByRole("button", { name: "Move up" })).toBeDisabled();
    expect(within(itemWith("inmitten")).getByRole("button", { name: "Move down" })).toBeDisabled();
  });

  it("unaccepts a meaning through its switch", async () => {
    const onChange = vi.fn();
    render(<MeaningListEditor rows={[mitte, inmitten]} onChange={onChange} />);
    await userEvent.click(within(itemWith("inmitten")).getByRole("switch", { name: "Accepted" }));
    expect(onChange).toHaveBeenCalledWith([mitte, { ...inmitten, isAccepted: false }]);
  });

  it("pins a meaning and shows the pressed state", async () => {
    const onChange = vi.fn();
    const { rerender } = render(<MeaningListEditor rows={[mitte]} onChange={onChange} />);
    const pin = within(itemWith("Mitte")).getByRole("button", { name: "Pin" });
    expect(pin).toHaveAttribute("aria-pressed", "false");
    await userEvent.click(pin);
    expect(onChange).toHaveBeenCalledWith([{ ...mitte, isPinned: true }]);

    rerender(<MeaningListEditor rows={[{ ...mitte, isPinned: true }]} onChange={onChange} />);
    expect(within(itemWith("Mitte")).getByRole("button", { name: "Pin" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("offers delete only for manual meanings", async () => {
    const onChange = vi.fn();
    render(<MeaningListEditor rows={[mitte, inmitten]} onChange={onChange} />);
    expect(within(itemWith("Mitte")).queryByRole("button", { name: "Remove" })).toBeNull();
    await userEvent.click(within(itemWith("inmitten")).getByRole("button", { name: "Remove" }));
    expect(onChange).toHaveBeenCalledWith([mitte]);
  });
});
