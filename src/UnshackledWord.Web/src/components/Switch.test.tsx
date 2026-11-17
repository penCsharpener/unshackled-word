import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Switch } from "./Switch";

describe("Switch", () => {
  it("exposes its state and reports the flipped value on click", async () => {
    const onCheckedChange = vi.fn();
    render(
      <Switch checked={false} onCheckedChange={onCheckedChange}>
        Lines
      </Switch>,
    );
    const toggle = screen.getByRole("switch", { name: "Lines" });
    expect(toggle).toHaveAttribute("aria-checked", "false");
    await userEvent.click(toggle);
    expect(onCheckedChange).toHaveBeenCalledWith(true);
  });

  it("reports false when switched off", async () => {
    const onCheckedChange = vi.fn();
    render(
      <Switch checked onCheckedChange={onCheckedChange}>
        Lines
      </Switch>,
    );
    const toggle = screen.getByRole("switch", { name: "Lines" });
    expect(toggle).toHaveAttribute("aria-checked", "true");
    await userEvent.click(toggle);
    expect(onCheckedChange).toHaveBeenCalledWith(false);
  });

  it("does not toggle when disabled", async () => {
    const onCheckedChange = vi.fn();
    render(
      <Switch checked={false} disabled onCheckedChange={onCheckedChange}>
        Lines
      </Switch>,
    );
    const toggle = screen.getByRole("switch", { name: "Lines" });
    expect(toggle).toBeDisabled();
    await userEvent.click(toggle);
    expect(onCheckedChange).not.toHaveBeenCalled();
  });
});
