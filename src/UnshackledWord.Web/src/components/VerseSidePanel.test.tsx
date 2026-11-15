import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";
import { VerseSidePanel } from "./VerseSidePanel";

describe("VerseSidePanel", () => {
  it("is a labelled group with its content", () => {
    render(
      <VerseSidePanel mode="original" label="Grundtext von Vers 3">
        <p>Ἦν</p>
      </VerseSidePanel>,
    );
    expect(screen.getByRole("group", { name: "Grundtext von Vers 3" })).toHaveTextContent("Ἦν");
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("offers +N for a clipped list, opens the overlay on click and closes it with Escape", async () => {
    const user = userEvent.setup();
    const onExpandedChange = vi.fn();
    // jsdom has no layout: every item is 20px tall and the panel only 20px, so most items do not fit
    vi.spyOn(HTMLElement.prototype, "offsetHeight", "get").mockReturnValue(20);
    vi.spyOn(HTMLElement.prototype, "clientHeight", "get").mockReturnValue(20);
    vi.spyOn(HTMLElement.prototype, "offsetTop", "get").mockImplementation(function (
      this: HTMLElement,
    ) {
      return this.tagName === "LI"
        ? 20 * Array.from(this.parentElement?.children ?? []).indexOf(this)
        : 0;
    });
    const panel = (expanded: boolean) => (
      <VerseSidePanel
        mode="tsk"
        label="Querverweise zu Vers 1"
        expanded={expanded}
        onExpandedChange={onExpandedChange}
      >
        <ul>
          <li>
            <a href="#a">A</a>
          </li>
          <li>
            <a href="#b">B</a>
          </li>
          <li>
            <a href="#c">C</a>
          </li>
        </ul>
      </VerseSidePanel>
    );
    const { rerender } = render(panel(false));
    const group = screen.getByRole("group", { name: "Querverweise zu Vers 1" });
    expect(group).not.toHaveAttribute("data-expanded");
    const more = screen.getByRole("button", { name: /weitere Querverweise|more cross references/ });
    expect(more).toHaveAttribute("aria-expanded", "false");
    await user.click(more);
    expect(onExpandedChange).toHaveBeenCalledWith(true);

    rerender(panel(true));
    expect(screen.getByRole("group", { name: "Querverweise zu Vers 1" })).toHaveAttribute(
      "data-expanded",
    );
    await user.keyboard("{Escape}");
    expect(onExpandedChange).toHaveBeenLastCalledWith(false);
    vi.restoreAllMocks();
  });
});
