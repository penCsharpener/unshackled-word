import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { DonutChart, type DonutChartProps, type DonutSlice } from "./DonutChart";

const slices: DonutSlice[] = [
  { key: "H6951", label: "H6951 – qahal", text: "H6951", value: 105 },
  { key: "H4744", label: "H4744 – miqra", text: "H4744", value: 21 },
  { key: "H3862", label: "H3862", value: 1, highlight: true },
];

function setup(props: Partial<DonutChartProps> = {}) {
  return render(<DonutChart title="Versammlung im AT" slices={slices} {...props} />);
}

describe("DonutChart", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("is a figure named by its title with subtitle and note", () => {
    setup({ subtitle: "133 Vorkommen", note: "Hinweis" });
    expect(screen.getByRole("figure", { name: "Versammlung im AT" })).toBeInTheDocument();
    expect(screen.getByText("133 Vorkommen")).toBeInTheDocument();
    expect(screen.getByText("Hinweis")).toBeInTheDocument();
  });

  it("offers a table with count and share per slice", () => {
    setup();
    const table = screen.getByRole("table", { name: "Versammlung im AT" });
    const rows = within(table).getAllByRole("row");
    expect(rows).toHaveLength(4);
    const first = within(rows.at(1) as HTMLElement);
    expect(first.getByRole("rowheader")).toHaveTextContent("H6951 – qahal");
    expect(first.getAllByRole("cell").map((c) => c.textContent?.replace(/\s/g, " "))).toEqual([
      "105",
      "82,7 %",
    ]);
  });

  it("draws one arc per slice with the colour slot in order", () => {
    const { container } = setup();
    const arcs = container.querySelectorAll("circle.donut-seg");
    expect(arcs).toHaveLength(3);
    expect(arcs[0]).toHaveAttribute("data-slot", "1");
    expect(arcs[2]).toHaveAttribute("data-slot", "3");
    expect(arcs[0]).toHaveAttribute("pathLength", "100");
    expect(arcs[1]).toHaveAttribute("stroke-dashoffset", String(-(105 / 127) * 100));
  });

  it("shows the total in the centre by default, or the given centre", () => {
    const { container, unmount } = setup();
    expect(container.querySelector(".donut-center b")).toHaveTextContent("127");
    unmount();
    const second = setup({ centerValue: 3, centerLabel: "hebr. Wörter" });
    expect(second.container.querySelector(".donut-center b")).toHaveTextContent("3");
    expect(screen.getByText("hebr. Wörter")).toBeInTheDocument();
  });

  it("highlights a slice without dimming the rest", () => {
    const { container } = setup();
    expect(container.querySelector(".donut")).not.toHaveAttribute("data-filtered");
    expect(container.querySelectorAll("circle[data-active]")).toHaveLength(1);
    expect(container.querySelector("circle[data-active]")).toHaveAttribute("data-slot", "3");
  });

  it("drops zero slices and folds slices beyond eight into Sonstige", () => {
    const many: DonutSlice[] = Array.from({ length: 10 }, (_, i) => ({
      key: `k${i}`,
      label: `Eintrag ${i}`,
      value: i === 9 ? 0 : 10 - i,
    }));
    const { container } = setup({ slices: many });
    expect(container.querySelectorAll("circle.donut-seg")).toHaveLength(8);
    const table = screen.getByRole("table");
    expect(within(table).getByRole("rowheader", { name: "Sonstige" })).toBeInTheDocument();
    expect(within(table).queryByRole("rowheader", { name: "Eintrag 8" })).not.toBeInTheDocument();
  });

  it("renders only the empty track without data", () => {
    const { container } = setup({ slices: [] });
    expect(container.querySelector("circle.donut-track")).not.toBeNull();
    expect(container.querySelectorAll("circle.donut-seg")).toHaveLength(0);
  });

  describe("with onSelectSlice", () => {
    it("selects a slice from the legend and clears on a second click", async () => {
      const onSelectSlice = vi.fn();
      const { rerender } = setup({ onSelectSlice });
      expect(screen.queryByRole("table")).not.toBeInTheDocument();
      await userEvent.click(screen.getByRole("button", { name: "H4744 – miqra" }));
      expect(onSelectSlice).toHaveBeenLastCalledWith("H4744");

      rerender(
        <DonutChart
          title="Versammlung im AT"
          slices={slices}
          onSelectSlice={onSelectSlice}
          activeKey="H4744"
        />,
      );
      await userEvent.click(screen.getByRole("button", { name: "H4744 – miqra" }));
      expect(onSelectSlice).toHaveBeenLastCalledWith(null);
    });

    it("dims other slices and shows the active value in the centre", () => {
      const { container } = setup({ onSelectSlice: () => {}, activeKey: "H4744" });
      expect(container.querySelector(".donut")).toHaveAttribute("data-filtered");
      expect(container.querySelector("circle[data-active]")).toHaveAttribute("data-slot", "2");
      expect(container.querySelector(".donut-center b")).toHaveTextContent("21");
      expect(screen.getByText("von 127")).toBeInTheDocument();
    });

    it("adds a filter select that mirrors the slices", async () => {
      const onSelectSlice = vi.fn();
      setup({ onSelectSlice, filterLabel: "Strong" });
      const select = screen.getByRole("combobox", { name: "Strong:" });
      expect(
        within(select)
          .getAllByRole("option")
          .map((o) => o.textContent),
      ).toEqual(["Alle anzeigen", "H6951 – 105×", "H4744 – 21×", "H3862 – 1×"]);
      await userEvent.selectOptions(select, "H4744");
      expect(onSelectSlice).toHaveBeenLastCalledWith("H4744");
      await userEvent.selectOptions(select, "");
      expect(onSelectSlice).toHaveBeenLastCalledWith(null);
    });

    it("disables the select for a single slice", () => {
      setup({
        slices: [{ key: "a", label: "A", value: 5 }],
        onSelectSlice: () => {},
        filterLabel: "Strong",
      });
      expect(screen.getByRole("combobox")).toBeDisabled();
    });
  });

  it("uses English strings", async () => {
    await i18n.changeLanguage("en");
    setup({ onSelectSlice: () => {}, filterLabel: "Strong", activeKey: "H4744" });
    expect(screen.getByText("of 127")).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Show all" })).toBeInTheDocument();
  });
});
