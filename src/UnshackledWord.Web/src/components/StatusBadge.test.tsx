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

  it("draws the dot variant as a token-coloured mark with screen-reader-only text", () => {
    render(
      <StatusBadge tone="ok" variant="dot">
        fertig
      </StatusBadge>,
    );
    const dot = screen.getByText("fertig").closest("[data-tone]") as HTMLElement;
    expect(dot).toHaveClass("bg-sev-ok", "rounded-full");
    expect(dot).not.toHaveClass("motion-safe:animate-pulse");
    expect(screen.getByText("fertig")).toHaveClass("sr-only");
  });

  it("pulses a busy dot and uses the muted token for neutral", () => {
    render(
      <StatusBadge tone="neutral" variant="dot" busy>
        wartet
      </StatusBadge>,
    );
    const dot = screen.getByText("wartet").closest("[data-tone]");
    expect(dot).toHaveClass("bg-fg-muted", "motion-safe:animate-pulse");
  });
});
