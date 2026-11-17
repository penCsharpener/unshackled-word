import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { StatusBadge, type StatusTone } from "./StatusBadge";

describe("StatusBadge", () => {
  it.each<StatusTone>(["neutral", "info", "ok", "warning", "error"])(
    "shows the text for the %s tone and marks the tone",
    (tone) => {
      render(<StatusBadge tone={tone}>aktiv</StatusBadge>);
      const badge = screen.getByText("aktiv").closest("[data-tone]");
      expect(badge).toHaveAttribute("data-tone", tone);
    },
  );

  it("never relies on colour alone: an icon next to the text, hidden from assistive tech", () => {
    render(<StatusBadge tone="error">fehlgeschlagen</StatusBadge>);
    const badge = screen.getByText("fehlgeschlagen").closest("[data-tone]") as HTMLElement;
    expect(badge).toHaveTextContent("✕fehlgeschlagen");
    expect(badge.querySelector("[aria-hidden='true']")).toHaveTextContent("✕");
  });

  it("uses only token classes for the tone border", () => {
    render(<StatusBadge tone="warning">Budget</StatusBadge>);
    expect(screen.getByText("Budget").closest("[data-tone]")).toHaveClass("border-sev-warning");
  });
});
