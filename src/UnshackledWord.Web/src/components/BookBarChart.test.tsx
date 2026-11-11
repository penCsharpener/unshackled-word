import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { fixtureBooks } from "@/test/fixtures/bibleStructure";
import { BookBarChart, type BookBarChartProps } from "./BookBarChart";

const counts = [
  { bibleBookId: 40, count: 3 },
  { bibleBookId: 66, count: 20 },
];

function setup(props: Partial<BookBarChartProps> = {}) {
  return render(
    <BookBarChart
      books={fixtureBooks}
      counts={counts}
      testaments={["nt"]}
      title="Vorkommen im Neuen Testament"
      summary="23 Vorkommen"
      {...props}
    />,
  );
}

describe("BookBarChart", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("names the section by its title and shows the summary", () => {
    setup();
    expect(
      screen.getByRole("region", { name: "Vorkommen im Neuen Testament" }),
    ).toBeInTheDocument();
    expect(screen.getByText("23 Vorkommen")).toBeInTheDocument();
  });

  it("gives every book a text alternative, zeros included", () => {
    setup({ testaments: ["ot", "nt"] });
    const columns = screen.getByRole("list", { name: "Neues Testament: Vorkommen je Buch" });
    expect(within(columns).getByText("Matthäus: 3")).toBeInTheDocument();
    expect(within(columns).getByText("Offenbarung: 20")).toBeInTheDocument();
    const ot = screen.getByRole("list", { name: "Altes Testament: Vorkommen je Buch" });
    expect(within(ot).getByText("1. Mose: 0")).toBeInTheDocument();
  });

  it("lists every book in the list view, muted when it has no occurrences", () => {
    setup({ testaments: ["ot", "nt"] });
    const nt = screen.getByRole("list", { name: "Neues Testament: Bücher und Vorkommen" });
    expect(within(nt).getAllByRole("listitem")).toHaveLength(2);
    const ot = screen.getByRole("list", { name: "Altes Testament: Bücher und Vorkommen" });
    const zero = within(ot)
      .getAllByRole("listitem")
      .filter((li) => li.hasAttribute("data-zero"));
    expect(zero).toHaveLength(4);
    expect(within(zero[0] as HTMLElement).getByText("0")).toBeInTheDocument();
  });

  it("shows testament totals only when several testaments are shown", () => {
    const { unmount } = setup();
    expect(screen.queryByRole("heading", { level: 3 })).not.toBeInTheDocument();
    unmount();
    setup({ testaments: ["ot", "nt"] });
    expect(screen.getByRole("heading", { level: 3, name: "Neues Testament · 23" })).toBeVisible();
    expect(screen.getByRole("heading", { level: 3, name: "Altes Testament · 0" })).toBeVisible();
  });

  it("scales bars on one scale and gives zero books a flat bar", () => {
    const { container } = setup({ testaments: ["ot", "nt"] });
    const bars = container.querySelectorAll(".bookchart-cols .bookchart-bar");
    expect(bars).toHaveLength(2);
    expect(bars[1]).toHaveAttribute("height", "100%");
    expect(bars[0]).toHaveAttribute("height", "15%");
    expect(container.querySelectorAll(".bookchart-cols .bookchart-bar-zero")).toHaveLength(4);
  });

  it("colours bars by Bible section and lists only sections that occur", () => {
    const { container } = setup();
    const legend = screen.getByRole("list", { name: "Farben: Bibelteile" });
    expect(within(legend).getByText("Evangelien & Apg")).toBeInTheDocument();
    expect(within(legend).getByText("Offenbarung")).toBeInTheDocument();
    expect(within(legend).queryByText("Gesetz")).not.toBeInTheDocument();
    expect(container.querySelector(".bookchart-cols li[data-slot='5']")).not.toBeNull();
    expect(container.querySelector(".bookchart-cols li[data-slot='8']")).not.toBeNull();
  });

  it("switches between chart and list view", async () => {
    const { container } = setup();
    const root = container.querySelector(".bookchart");
    expect(root).toHaveAttribute("data-view", "chart");
    await userEvent.click(screen.getByRole("button", { name: "Als Liste" }));
    expect(root).toHaveAttribute("data-view", "list");
    expect(screen.getByRole("button", { name: "Als Diagramm" })).toBeInTheDocument();
  });

  it("is not interactive without onSelectBook", () => {
    setup();
    expect(screen.queryByRole("button", { name: "Matthäus: 3" })).not.toBeInTheDocument();
  });

  it("selects and deselects a book", async () => {
    const onSelectBook = vi.fn();
    const { rerender } = setup({ onSelectBook });
    const columns = screen.getByRole("list", { name: "Neues Testament: Vorkommen je Buch" });
    await userEvent.click(within(columns).getByRole("button", { name: "Offenbarung: 20" }));
    expect(onSelectBook).toHaveBeenLastCalledWith(66);

    rerender(
      <BookBarChart
        books={fixtureBooks}
        counts={counts}
        testaments={["nt"]}
        title="t"
        onSelectBook={onSelectBook}
        selectedBookId={66}
      />,
    );
    const selected = within(
      screen.getByRole("list", { name: "Neues Testament: Vorkommen je Buch" }),
    ).getByRole("button", { name: "Offenbarung: 20" });
    expect(selected).toHaveAttribute("aria-pressed", "true");
    await userEvent.click(selected);
    expect(onSelectBook).toHaveBeenLastCalledWith(null);
  });

  it("disables books without occurrences", () => {
    setup({ onSelectBook: () => {}, testaments: ["ot"] });
    expect(screen.getByRole("button", { name: "Psalmen: 0" })).toBeDisabled();
  });

  it("selects from the list view too", async () => {
    const onSelectBook = vi.fn();
    setup({ onSelectBook });
    const list = screen.getByRole("list", { name: "Neues Testament: Bücher und Vorkommen" });
    await userEvent.click(within(list).getByRole("button", { name: "Matthäus: 3" }));
    expect(onSelectBook).toHaveBeenCalledWith(40);
  });

  it("uses English names and abbreviations", async () => {
    await i18n.changeLanguage("en");
    setup();
    expect(screen.getByText("Revelation: 20")).toBeInTheDocument();
    expect(screen.getAllByText("Rev").length).toBeGreaterThan(0);
    expect(
      screen.getByRole("list", { name: "New Testament: books and occurrences" }),
    ).toBeVisible();
  });
});
