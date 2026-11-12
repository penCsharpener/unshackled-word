import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { SaveBar } from "./SaveBar";

const base = {
  count: 3,
  status: "idle" as const,
  saveLabel: "Speichern",
  onSave: vi.fn(),
  onDiscard: vi.fn(),
};

describe("SaveBar", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("renders nothing without changes", () => {
    const { container } = render(<SaveBar {...base} count={0} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("shows the count and saves", async () => {
    const onSave = vi.fn();
    render(<SaveBar {...base} onSave={onSave} />);
    expect(screen.getByRole("region")).toHaveTextContent("3");
    await userEvent.click(screen.getByRole("button", { name: "Speichern" }));
    expect(onSave).toHaveBeenCalled();
  });

  it("asks before discarding and focuses Cancel", async () => {
    const onDiscard = vi.fn();
    render(<SaveBar {...base} onDiscard={onDiscard} />);
    await userEvent.click(screen.getByRole("button", { name: "Verwerfen" }));
    expect(onDiscard).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Abbrechen" })).toHaveFocus();
    await userEvent.click(screen.getByRole("button", { name: "Ja, verwerfen" }));
    expect(onDiscard).toHaveBeenCalled();
  });

  it("disables the button while saving and shows errors", () => {
    const { rerender } = render(<SaveBar {...base} status="saving" />);
    expect(screen.getByRole("button", { name: "Speichern …" })).toBeDisabled();
    rerender(<SaveBar {...base} status="error" message="kaputt" />);
    expect(screen.getByRole("alert")).toHaveTextContent("kaputt");
  });

  it("confirms a finished save", () => {
    render(<SaveBar {...base} count={0} status="saved" savedCount={2} />);
    expect(screen.getByText(/2 Änderungen gespeichert/)).toBeInTheDocument();
  });
});
