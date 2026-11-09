import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { WordDetailPopover } from "@/components/WordDetailPopover";
import i18n from "@/i18n";
import sample from "@/test/fixtures/wordDetail.sample.json";
import type { GetWordDetailResponse } from "./types";

// The C# test in tests/UnshackledWord.Domain.Tests reads the same file and round-trips it through the API DTOs.
// JSON imports widen literals to `string`, so a plain `satisfies` cannot check the unions; the cast still fails on shape drift.
const details = sample as GetWordDetailResponse[];

beforeEach(async () => {
  window.HTMLElement.prototype.showPopover ??= vi.fn();
  await i18n.changeLanguage("de");
});

describe("wordDetail.sample.json", () => {
  it("has the three documented shapes", () => {
    expect(details.map((d) => d.wordId)).toEqual([101, 202, 301]);
  });

  it.each(details.map((d) => [d.wordInContext, d] as const))("renders %s", (_name, detail) => {
    render(
      <WordDetailPopover
        id="pop"
        word={{ id: detail.wordId, text: detail.wordInContext, senses: [] }}
        status="success"
        detail={detail}
        onRetry={vi.fn()}
        onClose={vi.fn()}
      />,
    );
    const first = detail.readings[0];
    if (first) {
      expect(screen.getAllByText(first.original).length).toBeGreaterThan(0);
    } else {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    }
  });
});
