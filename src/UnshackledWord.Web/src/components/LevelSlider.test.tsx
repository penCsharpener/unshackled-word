import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { LevelSlider } from "./LevelSlider";

const levels = [
  { level: 0, name: "Elberfelder 1871" },
  { level: 1000, name: "Revision" },
  { level: 5000, name: "Einfach" },
];

describe("LevelSlider", () => {
  it("renders nothing with fewer than two levels", () => {
    const { container } = render(
      <LevelSlider levels={levels.slice(0, 1)} value={0} onChange={() => {}} label="Stufe" />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("exposes the level name as value text and tooltip", () => {
    render(<LevelSlider levels={levels} value={1000} onChange={() => {}} label="Stufe" />);
    const slider = screen.getByRole("slider", { name: "Stufe" });
    expect(slider).toHaveAttribute("aria-valuetext", "Revision");
    expect(slider).toHaveAttribute("title", "Revision");
  });

  it("calls onChange with the level value, not the index", () => {
    const onChange = vi.fn();
    render(<LevelSlider levels={levels} value={1000} onChange={onChange} label="Stufe" />);
    const slider = screen.getByRole("slider");
    // jsdom has no native arrow-key handling for ranges; the browser fires this same change event.
    fireEvent.change(slider, { target: { value: "2" } });
    expect(onChange).toHaveBeenLastCalledWith(5000);
  });

  it("flashes the selected translation name and hides it again", () => {
    vi.useFakeTimers();
    render(<LevelSlider levels={levels} value={0} onChange={() => {}} label="Stufe" />);
    fireEvent.change(screen.getByRole("slider"), { target: { value: "1" } });
    expect(screen.getByText("Revision")).toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(2000);
    });
    expect(screen.queryByText("Revision")).not.toBeInTheDocument();
  });

  afterEach(() => {
    vi.useRealTimers();
  });
});
