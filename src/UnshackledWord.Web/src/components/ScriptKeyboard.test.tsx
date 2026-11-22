import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";
import { ScriptKeyboard } from "./ScriptKeyboard";

describe("ScriptKeyboard", () => {
  it("emits the letter of a clicked Greek key", async () => {
    const onKey = vi.fn();
    render(<ScriptKeyboard layout="greek" onKey={onKey} />);
    await userEvent.click(screen.getByRole("button", { name: "μ" }));
    expect(onKey).toHaveBeenCalledWith("μ");
  });

  it("emits a Hebrew letter and stays left to right", async () => {
    const onKey = vi.fn();
    render(<ScriptKeyboard layout="hebrew" onKey={onKey} />);
    await userEvent.click(screen.getByRole("button", { name: "א" }));
    expect(onKey).toHaveBeenCalledWith("א");
    const panel = screen.getByRole("group", { name: "Bildschirmtastatur" });
    expect(panel).toHaveAttribute("dir", "ltr");
    expect(panel).toHaveAttribute("lang", "he");
  });

  it("emits ⌫ for backspace", async () => {
    const onKey = vi.fn();
    render(<ScriptKeyboard layout="greek" onKey={onKey} />);
    await userEvent.click(screen.getByRole("button", { name: "Löschen" }));
    expect(onKey).toHaveBeenCalledWith("⌫");
  });
});
