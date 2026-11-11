import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { ThemeProvider } from "@/lib/theme";
import { ThemeToggle } from "./ThemeToggle";

describe("ThemeToggle", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("matchMedia", () => ({
      matches: false,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
  });

  it("cycles system, light, dark and back with one button", async () => {
    render(
      <ThemeProvider>
        <ThemeToggle />
      </ThemeProvider>,
    );
    const button = screen.getByRole("button");
    const seen = [button.getAttribute("aria-label")];
    for (let i = 0; i < 3; i++) {
      await userEvent.click(button);
      seen.push(button.getAttribute("aria-label"));
    }
    expect(new Set(seen).size).toBe(3);
    expect(seen[0]).toBe(seen[3]);
  });
});
