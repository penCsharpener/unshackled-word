import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { WordDetailPopover } from "@/components/WordDetailPopover";
import i18n from "@/i18n";
import sample from "@/test/fixtures/wordDetail.sample.json";
import { bibleKeys } from "./queries";
import type { GetGreekEditionsResponse, GetWordDetailResponse } from "./types";

// The C# test in tests/UnshackledWord.Domain.Tests reads the same file and round-trips it through the API DTOs.
// JSON imports widen literals to `string`, so a plain `satisfies` cannot check the unions; the cast still fails on shape drift.
const details = sample as GetWordDetailResponse[];

const catalog: GetGreekEditionsResponse = {
  editions: [{ code: "WH", name: "Westcott-Hort", year: 1881, kind: "edition", isMain: true }],
};

function renderDetail(detail: GetWordDetailResponse) {
  // The edition section loads the catalog through TanStack Query; the cache is filled, so nothing is fetched.
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(bibleKeys.greekEditions, catalog);
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <WordDetailPopover
          id="pop"
          word={{ id: detail.wordId, text: detail.wordInContext, senses: [] }}
          status="success"
          detail={detail}
          onRetry={vi.fn()}
          onClose={vi.fn()}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(async () => {
  window.HTMLElement.prototype.showPopover ??= vi.fn();
  await i18n.changeLanguage("de");
});

describe("wordDetail.sample.json", () => {
  it("has the three documented shapes", () => {
    expect(details.map((d) => d.wordId)).toEqual([101, 202, 301]);
  });

  it.each(details.map((d) => [d.wordInContext, d] as const))("renders %s", (_name, detail) => {
    renderDetail(detail);
    const first = detail.readings[0];
    if (first) {
      expect(screen.getAllByText(first.original).length).toBeGreaterThan(0);
    } else {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    }
  });

  it("renders the editions of a Greek reading and none for readings without them", () => {
    const [greek, hebrew] = details;
    if (!greek || !hebrew) throw new Error("fixture shape");
    expect(greek.readings[0]?.editions?.missing.map((e) => e.code)).toEqual(["WH", "Treg"]);
    expect(hebrew.readings.every((r) => r.editions === undefined)).toBe(true);

    renderDetail(greek);
    const section = screen.getByRole("region", { name: "Textausgaben" });
    const missing = within(section).getByRole("list", { name: "Ausgaben ohne dieses Wort" });
    expect(
      within(missing)
        .getAllByRole("button")
        .map((b) => b.textContent),
    ).toEqual(["•WH (fehlt)", "•Treg (fehlt)"]);
    expect(within(section).getByText("1 Wort später")).toBeInTheDocument();
    expect(within(section).getByText("2 Wörter früher")).toBeInTheDocument();
    expect(within(section).getByText("andere Stelle (14,24)")).toBeInTheDocument();
  });

  it("shows no edition section without editions", () => {
    const hebrew = details[1];
    if (!hebrew) throw new Error("fixture shape");
    renderDetail(hebrew);
    expect(screen.queryByRole("region", { name: "Textausgaben" })).not.toBeInTheDocument();
  });
});
