import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";
import { ChoiceList } from "./ChoiceList";

const options = ["Gesetz", "Wort", "Geist", "Tag"];

describe("ChoiceList", () => {
  it("calls onPick with the index of the chosen option", async () => {
    const onPick = vi.fn();
    render(
      <ChoiceList options={options} state={{ picked: null, correct: null }} onPick={onPick} />,
    );
    await userEvent.click(screen.getByRole("button", { name: /Wort/ }));
    expect(onPick).toHaveBeenCalledWith(1);
  });

  it("locks after a pick and marks the correct and the wrong option", async () => {
    const onPick = vi.fn();
    render(<ChoiceList options={options} state={{ picked: 0, correct: 1 }} onPick={onPick} />);
    const wrong = screen.getByRole("button", { name: /Gesetz/ });
    const right = screen.getByRole("button", { name: /Wort/ });
    expect(wrong).toHaveClass("border-sev-error");
    expect(right).toHaveClass("border-sev-ok");
    await userEvent.click(screen.getByRole("button", { name: /Tag/ }));
    expect(onPick).not.toHaveBeenCalled();
  });

  it("numbers the options 1 to 4", () => {
    render(
      <ChoiceList options={options} state={{ picked: null, correct: null }} onPick={vi.fn()} />,
    );
    expect(screen.getByRole("button", { name: "Gesetz" })).toHaveTextContent("1");
    expect(screen.getByRole("button", { name: "Tag" })).toHaveTextContent("4");
  });
});
