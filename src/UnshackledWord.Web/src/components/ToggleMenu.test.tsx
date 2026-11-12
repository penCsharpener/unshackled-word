import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ToggleMenu } from "./ToggleMenu";

const items = [
  { key: "a", label: "Alpha", checked: true },
  { key: "b", label: "Beta", checked: false },
];

describe("ToggleMenu", () => {
  it("opens, toggles and resets", async () => {
    const onChange = vi.fn();
    const onReset = vi.fn();
    render(
      <ToggleMenu
        label="Spalten"
        items={items}
        onChange={onChange}
        onReset={onReset}
        resetLabel="Zurück"
      />,
    );
    expect(screen.queryByRole("checkbox")).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: /Spalten/ }));
    await userEvent.click(screen.getByRole("checkbox", { name: "Beta" }));
    expect(onChange).toHaveBeenCalledWith("b", true);
    await userEvent.click(screen.getByRole("button", { name: "Zurück" }));
    expect(onReset).toHaveBeenCalled();
    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("checkbox")).toBeNull();
  });
});
