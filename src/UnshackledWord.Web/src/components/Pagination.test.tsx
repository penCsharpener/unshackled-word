import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { Pagination, pageCountOf, pagerItems, pageWindow, parseJump } from "./Pagination";

beforeEach(() => i18n.changeLanguage("en"));

describe("pageWindow", () => {
  it("lists every page when they all fit", () => {
    expect(pageWindow(1, 1)).toEqual([1]);
    expect(pageWindow(4, 9)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
  });

  it("shows the first and last page, two neighbours and gaps in the middle", () => {
    expect(pageWindow(6, 41)).toEqual([1, "gap", 4, 5, 6, 7, 8, "gap", 41]);
  });

  it("keeps the width at the start and at the end", () => {
    expect(pageWindow(1, 41)).toEqual([1, 2, 3, 4, 5, 6, 7, "gap", 41]);
    expect(pageWindow(5, 41)).toEqual([1, 2, 3, 4, 5, 6, 7, "gap", 41]);
    expect(pageWindow(37, 41)).toEqual([1, "gap", 35, 36, 37, 38, 39, 40, 41]);
    expect(pageWindow(41, 41)).toEqual([1, "gap", 35, 36, 37, 38, 39, 40, 41]);
  });

  it("never hides a single page behind a gap", () => {
    for (let page = 1; page <= 30; page++) {
      const items = pageWindow(page, 30);
      items.forEach((item, i) => {
        if (item !== "gap") return;
        const before = items[i - 1] as number;
        const after = items[i + 1] as number;
        expect(after - before).toBeGreaterThan(2);
      });
    }
  });

  it("shows only the current page between first and last with no neighbours", () => {
    expect(pageWindow(6, 41, 0)).toEqual([1, "gap", 6, "gap", 41]);
    expect(pageWindow(2, 41, 0)).toEqual([1, 2, 3, "gap", 41]);
  });

  it("clamps the current page and handles no pages", () => {
    expect(pageWindow(99, 41)).toEqual(pageWindow(41, 41));
    expect(pageWindow(1, 0)).toEqual([]);
  });
});

describe("pagerItems", () => {
  it("marks the numbers only shown from sm up and keeps page order", () => {
    const items = pagerItems(6, 41);
    expect(items.filter((item) => item.page !== undefined).map((item) => item.page)).toEqual([
      1, 4, 5, 6, 7, 8, 41,
    ]);
    expect(items.find((item) => item.page === 4)).toMatchObject({ wide: true, narrow: false });
    expect(items.find((item) => item.page === 6)).toMatchObject({ wide: true, narrow: true });
    expect(items.filter((item) => item.page === undefined && item.narrow)).toHaveLength(2);
    expect(items.filter((item) => item.page === undefined && item.wide)).toHaveLength(2);
  });
});

describe("pageCountOf / parseJump", () => {
  it("counts pages of at least one", () => {
    expect(pageCountOf(0, 50)).toBe(1);
    expect(pageCountOf(50, 50)).toBe(1);
    expect(pageCountOf(51, 50)).toBe(2);
  });

  it("clamps a jump and ignores invalid input", () => {
    expect(parseJump("7", 41)).toBe(7);
    expect(parseJump(" 99 ", 41)).toBe(41);
    expect(parseJump("0", 41)).toBe(1);
    expect(parseJump("", 41)).toBeNull();
    expect(parseJump("abc", 41)).toBeNull();
    expect(parseJump("2.5", 41)).toBeNull();
    expect(parseJump("-3", 41)).toBeNull();
  });
});

describe("Pagination", () => {
  it("renders nothing for a single page", () => {
    const { container } = render(<Pagination page={1} pageCount={1} onPageChange={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("marks the current page and disables first/previous on page 1", () => {
    render(<Pagination page={1} pageCount={41} onPageChange={() => {}} />);
    expect(screen.getByRole("navigation", { name: "Pages" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Page 1" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("button", { name: "First page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Previous page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Next page" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Last page" })).toBeEnabled();
  });

  it("disables next/last on the last page", () => {
    render(<Pagination page={41} pageCount={41} onPageChange={() => {}} />);
    expect(screen.getByRole("button", { name: "Next page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Last page" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "First page" })).toBeEnabled();
  });

  it("moves with the arrow buttons and the page numbers", async () => {
    const onPageChange = vi.fn();
    render(<Pagination page={6} pageCount={41} onPageChange={onPageChange} />);
    await userEvent.click(screen.getByRole("button", { name: "Next page" }));
    await userEvent.click(screen.getByRole("button", { name: "Previous page" }));
    await userEvent.click(screen.getByRole("button", { name: "First page" }));
    await userEvent.click(screen.getByRole("button", { name: "Last page" }));
    await userEvent.click(screen.getByRole("button", { name: "Page 8" }));
    await userEvent.click(screen.getByRole("button", { name: "Page 6" }));
    expect(onPageChange.mock.calls.map(([page]) => page)).toEqual([7, 5, 1, 41, 8]);
  });

  it("jumps with Enter, clamped, and ignores invalid input", async () => {
    const onPageChange = vi.fn();
    render(<Pagination page={6} pageCount={41} onPageChange={onPageChange} />);
    const field = screen.getByLabelText("Go to page (1–41)");
    await userEvent.type(field, "12{Enter}");
    expect(onPageChange).toHaveBeenLastCalledWith(12);
    expect(field).toHaveValue(null);
    await userEvent.type(field, "500{Enter}");
    expect(onPageChange).toHaveBeenLastCalledWith(41);
    onPageChange.mockClear();
    await userEvent.type(field, "{Enter}");
    await userEvent.type(field, "e{Enter}");
    expect(onPageChange).not.toHaveBeenCalled();
  });

  it("labels the controls in German", async () => {
    await i18n.changeLanguage("de");
    render(<Pagination page={2} pageCount={5} onPageChange={() => {}} />);
    expect(screen.getByRole("navigation", { name: "Seiten" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Vorherige Seite" })).toBeEnabled();
    expect(screen.getByLabelText("Gehe zu Seite (1–5)")).toBeInTheDocument();
  });
});
