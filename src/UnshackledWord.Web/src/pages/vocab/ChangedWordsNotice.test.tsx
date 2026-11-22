import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import type { VocabChangesResponse } from "@/lib/vocab/changeApi";
import { json, stubApi } from "@/test/authHelpers";
import { ChangedWordsNotice } from "./ChangedWordsNotice";

const flagged: VocabChangesResponse = {
  count: 2,
  items: [
    {
      entryId: 7,
      lemma: "μέσος",
      dictionaryForm: "μέσος, -η, -ον",
      lang: "grc",
      status: "changed",
      changes: [
        {
          kind: "meanings",
          before: { meanings: ["Mitte"] },
          after: { meanings: ["Mitte", "mitten"] },
          changedAt: "2026-11-20T10:00:00Z",
        },
      ],
    },
    {
      entryId: 8,
      lemma: "κράβαττος",
      lang: "grc",
      status: "removed",
      changes: [
        {
          kind: "merge",
          after: { mergedInto: 9 },
          changedAt: "2026-11-20T10:00:00Z",
          targetEntryId: 9,
          targetLemma: "κράβατος",
        },
      ],
    },
  ],
};

afterEach(() => {
  vi.unstubAllGlobals();
});

function renderNotice(changedCount: number) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ChangedWordsNotice changedCount={changedCount} />
    </QueryClientProvider>,
  );
}

describe("ChangedWordsNotice", () => {
  it("shows the count and opens the review list", async () => {
    stubApi({ "GET /vocab/changes": () => json(flagged) });
    const user = userEvent.setup();
    renderNotice(2);

    expect(await screen.findByText("2 deiner Wörter wurden überarbeitet.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Ansehen" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Mitte, mitten")).toBeInTheDocument();
    expect(within(dialog).getByText("Zusammengeführt mit κράβατος.")).toBeInTheDocument();
  });

  it("keeps all changed words and hides the notice once none is left", async () => {
    let remaining: VocabChangesResponse = flagged;
    const keepAll = vi.fn(() => json({ kept: 1, remaining: 1 }));
    stubApi({
      "GET /vocab/changes": () => json(remaining),
      "POST /vocab/changes/keep-all": () => {
        remaining = { count: 1, items: flagged.items.slice(1) };
        return keepAll();
      },
    });
    const user = userEvent.setup();
    renderNotice(2);

    await user.click(await screen.findByRole("button", { name: "Ansehen" }));
    await user.click(await screen.findByRole("button", { name: "Alle weiterlernen" }));

    await waitFor(() => expect(keepAll).toHaveBeenCalledTimes(1));
    // Removed cards stay in the list: the notice shows the one left.
    expect(await screen.findByText("Ein Wort wurde überarbeitet.")).toBeInTheDocument();
  });
});
