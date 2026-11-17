import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { WordSplitter, type WordSplitterProps } from "./WordSplitter";

function Harness(props: Partial<WordSplitterProps> & { initial?: number[] }) {
  const [cuts, setCuts] = useState<number[]>(props.initial ?? []);
  return (
    <WordSplitter
      word="Weizenernte"
      onConfirm={() => {}}
      onCancel={() => {}}
      {...props}
      cuts={cuts}
      onChange={(next) => {
        setCuts(next);
        props.onChange?.(next);
      }}
    />
  );
}

const gap = (n: number) => document.querySelector(`[data-gap="${n}"]`) as HTMLElement;

beforeEach(async () => {
  await i18n.changeLanguage("de");
});

describe("WordSplitter", () => {
  it("draws the letters with a toggle in every gap and focuses the first", () => {
    render(<Harness />);
    expect(document.querySelectorAll("[data-gap]")).toHaveLength(10);
    expect(gap(1)).toHaveFocus();
    expect(gap(6)).toHaveAccessibleName("Schnitt zwischen „Weizen“ und „ernte“");
    expect(screen.getByRole("button", { name: "Teilen" })).toBeDisabled();
    expect(screen.getByText("Setze mindestens einen Schnitt.")).toBeInTheDocument();
  });

  it("toggles a cut by click and shows the parts as a live preview", async () => {
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    await userEvent.click(gap(6));
    expect(onChange).toHaveBeenLastCalledWith([6]);
    expect(gap(6)).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByText("Weizen | ernte")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Teilen" })).toBeEnabled();
    await userEvent.click(gap(6));
    expect(gap(6)).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: "Teilen" })).toBeDisabled();
  });

  it("moves between the gaps with the arrow keys and toggles with Space", async () => {
    render(<Harness />);
    await userEvent.keyboard("{ArrowRight}{ArrowRight}");
    expect(gap(3)).toHaveFocus();
    expect(gap(3)).toHaveAttribute("tabindex", "0");
    expect(gap(1)).toHaveAttribute("tabindex", "-1");
    await userEvent.keyboard(" ");
    expect(screen.getByText("Wei | zenernte")).toBeInTheDocument();
    await userEvent.keyboard("{ArrowLeft}{ArrowLeft}{ArrowLeft}");
    expect(gap(1)).toHaveFocus();
    await userEvent.keyboard("{End}");
    expect(gap(10)).toHaveFocus();
    await userEvent.keyboard("{Home}");
    expect(gap(1)).toHaveFocus();
  });

  it("allows any number of parts", async () => {
    render(<Harness />);
    await userEvent.click(gap(2));
    await userEvent.click(gap(6));
    await userEvent.click(gap(8));
    expect(screen.getByText("We | izen | er | nte")).toBeInTheDocument();
  });

  it("confirms, and cancels with Escape", async () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(<Harness initial={[6]} onConfirm={onConfirm} onCancel={onCancel} />);
    await userEvent.click(screen.getByRole("button", { name: "Teilen" }));
    expect(onConfirm).toHaveBeenCalledOnce();
    await userEvent.keyboard("{Escape}");
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it("blocks parts longer than maxPartLength", () => {
    render(<Harness initial={[6]} maxPartLength={5} />);
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Der Teil „Weizen“ ist länger als 5 Zeichen.",
    );
    expect(screen.getByRole("button", { name: "Teilen" })).toBeDisabled();
  });

  it("counts letters, not UTF-16 units", () => {
    render(<Harness word="Öl" />);
    expect(document.querySelectorAll("[data-gap]")).toHaveLength(1);
  });

  it("shows a lemma field per part only with onLemmasChange and reports the lemmas in order", async () => {
    const { rerender } = render(
      <WordSplitter
        word="Weizenernte"
        cuts={[6]}
        onChange={() => {}}
        onConfirm={() => {}}
        onCancel={() => {}}
      />,
    );
    expect(screen.queryByRole("textbox")).toBeNull();
    const onLemmasChange = vi.fn();
    rerender(
      <WordSplitter
        word="Weizenernte"
        cuts={[6]}
        lemmas={["Weizen"]}
        onLemmasChange={onLemmasChange}
        onChange={() => {}}
        onConfirm={() => {}}
        onCancel={() => {}}
      />,
    );
    expect(screen.getByRole("textbox", { name: "Lemma des Teils „Weizen“" })).toHaveValue("Weizen");
    await userEvent.type(screen.getByRole("textbox", { name: "Lemma des Teils „ernte“" }), "E");
    expect(onLemmasChange).toHaveBeenLastCalledWith(["Weizen", "E"]);
  });
});
