import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import "@/i18n";
import type { ChangeReviewItem } from "@/lib/vocab/changeReview";
import { ChangeReviewList } from "./ChangeReviewList";

const meanings: ChangeReviewItem = {
  entryId: 1,
  lemma: "μέσος",
  form: "μέσος, -η, -ον",
  lang: "grc",
  removed: false,
  changes: [
    { key: "a", kind: "Bedeutungen geändert", before: ["Mitte"], after: ["Mitte", "mitten"] },
  ],
};

const merged: ChangeReviewItem = {
  entryId: 2,
  lemma: "μεσόω",
  lang: "grc",
  removed: false,
  changes: [
    {
      key: "b",
      kind: "zusammengeführt",
      before: ["μεσόω", "μέσος, -η, -ον"],
      after: ["μεσόω"],
    },
  ],
};

const removed: ChangeReviewItem = {
  entryId: 3,
  lemma: "κράβαττος",
  lang: "grc",
  removed: true,
  changes: [{ key: "c", kind: "entfernt", note: "Zusammengeführt mit κράβατος." }],
};

function renderList(items: ChangeReviewItem[]) {
  const onKeep = vi.fn();
  const onReset = vi.fn();
  render(<ChangeReviewList items={items} onKeep={onKeep} onReset={onReset} />);
  return { onKeep, onReset };
}

describe("ChangeReviewList", () => {
  it("keeps a changed word and resets it", async () => {
    const user = userEvent.setup();
    const { onKeep, onReset } = renderList([meanings]);
    expect(screen.getByText(/Bedeutungen geändert/)).toBeInTheDocument();
    expect(screen.getByText(/Mitte, mitten/)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Weiterlernen" }));
    expect(onKeep).toHaveBeenCalledWith(1);

    await user.click(screen.getByRole("button", { name: "Zurücksetzen" }));
    expect(onReset).toHaveBeenCalledWith(1);
  });

  it("only resets a removed word", async () => {
    const user = userEvent.setup();
    const { onKeep, onReset } = renderList([removed]);
    expect(screen.queryByRole("button", { name: "Weiterlernen" })).toBeNull();
    expect(screen.getByText("Zusammengeführt mit κράβατος.")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Entfernen" }));
    expect(onReset).toHaveBeenCalledWith(3);
    expect(onKeep).not.toHaveBeenCalled();
  });

  it("shows both old forms of a merged word, the merged-away one struck out", () => {
    renderList([merged]);
    const item = screen.getByRole("listitem");
    expect(within(item).getByText(/zusammengeführt/)).toBeInTheDocument();
    expect(item.querySelector("s")).toHaveTextContent("μέσος, -η, -ον");
    expect(within(item).getAllByText("μεσόω")).not.toHaveLength(0);
  });

  it("says that everything is done when the list is empty", () => {
    renderList([]);
    expect(screen.getByText("Alles erledigt.")).toBeInTheDocument();
  });
});
