import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";
import type { TypingLayout } from "@/lib/vocab/typing";
import { LetterBoxInput } from "./LetterBoxInput";

const layout: TypingLayout = [
  { group: 3 },
  { sep: ", -" },
  { group: 1 },
  { sep: ", -" },
  { group: 2 },
];

function Harness({
  onValue,
  lang = "grc",
}: {
  onValue: (v: string[]) => void;
  lang?: "grc" | "he";
}) {
  const [value, setValue] = useState(["", "", ""]);
  return (
    <LetterBoxInput
      layout={layout}
      lang={lang}
      value={value}
      onChange={(next) => {
        setValue(next);
        onValue(next);
      }}
    />
  );
}

describe("LetterBoxInput", () => {
  it("renders one box per letter and the separators between the groups", () => {
    render(<Harness onValue={vi.fn()} />);
    expect(screen.getAllByLabelText("Buchstabe")).toHaveLength(6);
    const text = screen.getByRole("group", { name: "Lernform eingeben" }).textContent ?? "";
    expect(text.replace(/\u00a0/g, " ")).toBe(", -, -");
  });

  it("fills a group, jumps to the next one and hands the groups out", async () => {
    const onValue = vi.fn();
    const user = userEvent.setup();
    render(<Harness onValue={onValue} />);
    const boxes = screen.getAllByLabelText("Buchstabe");
    expect(boxes[0]).toHaveFocus();
    await user.keyboard("μεσ");
    expect(boxes[3]).toHaveFocus();
    await user.keyboard("ηον");
    expect(onValue).toHaveBeenLastCalledWith(["μεσ", "η", "ον"]);
    expect(boxes[5]).toHaveValue("ν");
  });

  it("types Greek letters from Latin keys", async () => {
    const onValue = vi.fn();
    const user = userEvent.setup();
    render(<Harness onValue={onValue} />);
    await user.keyboard("mes");
    expect(onValue).toHaveBeenLastCalledWith(["μεσ", "", ""]);
  });

  it("types Hebrew letters from Latin keys and ignores punctuation", async () => {
    const onValue = vi.fn();
    const user = userEvent.setup();
    render(<Harness onValue={onValue} lang="he" />);
    await user.keyboard("ab,-g");
    expect(onValue).toHaveBeenLastCalledWith(["אבג", "", ""]);
  });

  it("goes back with Backspace, also into the previous group", async () => {
    const onValue = vi.fn();
    const user = userEvent.setup();
    render(<Harness onValue={onValue} />);
    const boxes = screen.getAllByLabelText("Buchstabe");
    await user.keyboard("μεσο");
    expect(boxes[4]).toHaveFocus();
    await user.keyboard("{Backspace}");
    expect(onValue).toHaveBeenLastCalledWith(["μεσ", "", ""]);
    expect(boxes[3]).toHaveFocus();
    await user.keyboard("{Backspace}");
    expect(onValue).toHaveBeenLastCalledWith(["με", "", ""]);
    expect(boxes[2]).toHaveFocus();
  });

  it("marks the result per group and locks the boxes when read only", () => {
    render(
      <LetterBoxInput
        layout={layout}
        lang="grc"
        value={["μεσ", "η", "ον"]}
        onChange={vi.fn()}
        readOnly
        results={[true, false, true]}
      />,
    );
    const boxes = screen.getAllByLabelText("Buchstabe");
    expect(boxes[0]).toHaveClass("border-sev-ok");
    expect(boxes[3]).toHaveClass("border-sev-error");
    expect(boxes[0]).toBeDisabled();
  });

  it("is right to left for Hebrew", () => {
    render(
      <LetterBoxInput
        layout={[{ group: 3 }]}
        lang="he"
        dir="rtl"
        value={[""]}
        onChange={vi.fn()}
      />,
    );
    expect(screen.getByRole("group", { name: "Lernform eingeben" })).toHaveAttribute("dir", "rtl");
  });
});
