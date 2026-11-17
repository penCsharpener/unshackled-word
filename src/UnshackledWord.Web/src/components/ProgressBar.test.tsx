import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ProgressBar } from "./ProgressBar";

describe("ProgressBar", () => {
  it("is a progressbar with value, max and name", () => {
    render(<ProgressBar value={2} max={5} label="Pakete" />);
    const bar = screen.getByRole("progressbar", { name: "Pakete" });
    expect(bar).toHaveAttribute("value", "2");
    expect(bar).toHaveAttribute("max", "5");
    expect(screen.getByText("2 / 5")).toBeInTheDocument();
  });

  it("clamps the value to max", () => {
    render(<ProgressBar value={9} max={5} label="Pakete" />);
    expect(screen.getByRole("progressbar")).toHaveAttribute("value", "5");
    expect(screen.getByText("5 / 5")).toBeInTheDocument();
  });

  it("is indeterminate while max is 0", () => {
    render(<ProgressBar value={0} max={0} label="Pakete" />);
    const bar = screen.getByRole("progressbar");
    expect(bar).not.toHaveAttribute("value");
    expect(screen.queryByText(/\//)).not.toBeInTheDocument();
  });

  it("passes the tone on for styling", () => {
    render(<ProgressBar value={1} max={2} label="Pakete" tone="warning" />);
    expect(screen.getByRole("progressbar")).toHaveAttribute("data-tone", "warning");
  });
});
