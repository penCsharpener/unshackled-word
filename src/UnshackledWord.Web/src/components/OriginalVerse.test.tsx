import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import type { VerseMappingOriginalWord } from "@/lib/bible/types";
import { OriginalVerse } from "./OriginalVerse";

// the edition catalog (`api/greekEditions`) only loads for words with `editionCodes`; without a mocked fetch the fallback order applies
function Providers({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={client}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
}

/** Longer than the card's hide delay (500 ms). */
const pastHideDelay = () => new Promise((resolve) => setTimeout(resolve, 650));

function renderMargin() {
  return render(
    <Providers>
      <OriginalVerse
        variant="margin"
        reference="Johannes 3,1"
        data={{
          language: "greek",
          originalWords: [
            {
              id: 1,
              position: 1,
              original: "λόγος",
              lemma: "λόγος",
              strongs: "G3056",
              morphologyCode: "N-NSM",
              morphology: {
                code: "N-NSM",
                partOfSpeech: "noun",
                case: "nominative",
                number: "singular",
                gender: "masculine",
              },
            },
          ],
        }}
      />
    </Providers>,
  );
}

const word = () => screen.getAllByRole("link")[0] as HTMLElement;

describe("OriginalVerse margin", () => {
  afterEach(() => vi.restoreAllMocks());

  it("shows lemma, Strong's and decoded morphology above the hovered word", async () => {
    const user = userEvent.setup();
    renderMargin();
    expect(screen.queryByTestId("original-word-info")).not.toBeInTheDocument();
    await user.hover(word());
    const tip = await screen.findByTestId("original-word-info");
    expect(tip).toHaveTextContent("G3056");
    expect(tip).not.toHaveAttribute("data-below");
  });

  it("keeps the card open while the pointer moves from the word onto it", async () => {
    const user = userEvent.setup();
    renderMargin();
    await user.hover(word());
    const tip = await screen.findByTestId("original-word-info");
    // leaving the word must not close the card at once, or the pointer can never reach it
    await user.unhover(word());
    expect(tip).toBeInTheDocument();
    await user.hover(tip);
    await pastHideDelay();
    expect(tip).toBeInTheDocument();
    expect(within(tip).getByRole("link", { name: "G3056" })).toHaveAttribute(
      "href",
      "/bible/strongs/G3056",
    );
    await user.unhover(tip);
    await waitFor(() => expect(screen.queryByTestId("original-word-info")).not.toBeInTheDocument());
  });

  it("closes the card after the delay when the pointer leaves the word elsewhere", async () => {
    const user = userEvent.setup();
    renderMargin();
    await user.hover(word());
    await screen.findByTestId("original-word-info");
    await user.unhover(word());
    await waitFor(() => expect(screen.queryByTestId("original-word-info")).not.toBeInTheDocument());
  });

  it("keeps the card while Ctrl is held", async () => {
    const user = userEvent.setup();
    renderMargin();
    await user.hover(word());
    const tip = await screen.findByTestId("original-word-info");
    await user.keyboard("{Control>}");
    await user.unhover(word());
    await pastHideDelay();
    expect(tip).toBeInTheDocument();
    await user.keyboard("{/Control}");
    await waitFor(() => expect(screen.queryByTestId("original-word-info")).not.toBeInTheDocument());
  });

  it("opens the card below the word when it does not fit above", async () => {
    vi.spyOn(HTMLElement.prototype, "offsetHeight", "get").mockReturnValue(200);
    const user = userEvent.setup();
    renderMargin();
    await user.hover(word());
    expect(await screen.findByTestId("original-word-info")).toHaveAttribute("data-below");
  });

  it("shows only one card when the pointer moves over several verses", async () => {
    const user = userEvent.setup();
    const word = (id: number, original: string) => ({
      id,
      position: 1,
      original,
      strongs: `G${id}`,
      lemma: original,
    });
    render(
      <Providers>
        <OriginalVerse
          variant="margin"
          reference="a"
          data={{ language: "greek", originalWords: [word(1, "α")] }}
        />
        <OriginalVerse
          variant="margin"
          reference="b"
          data={{ language: "greek", originalWords: [word(2, "β")] }}
        />
      </Providers>,
    );
    await user.hover(screen.getByRole("link", { name: /α/ }));
    await user.hover(screen.getByRole("link", { name: /β/ }));
    expect(screen.getAllByTestId("original-word-info")).toHaveLength(1);
  });
});

describe("OriginalVerse editions", () => {
  afterEach(() => vi.restoreAllMocks());

  const greek = (id: number, original: string, extra: Partial<VerseMappingOriginalWord> = {}) => ({
    id,
    position: id,
    original,
    strongs: `G${id}`,
    ...extra,
  });

  function renderWords(words: VerseMappingOriginalWord[], variant: "panel" | "margin" = "margin") {
    // no catalog: the fallback order NA28, NA27, Tyn, SBL, WH, Treg, TR, Byz applies
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("offline"));
    return render(
      <Providers>
        <OriginalVerse
          variant={variant}
          reference="Matthäus 6,13"
          data={{ language: "greek", originalWords: words }}
        />
      </Providers>,
    );
  }

  it("marks a word in fewer than half of the main editions with 'nur in'", async () => {
    const user = userEvent.setup();
    renderWords([greek(1, "ὅτι", { editionCodes: ["TR", "Byz"] }), greek(2, "ἀμήν")]);
    const marked = screen.getByRole("link", { name: /ὅτι/ });
    expect(marked.className).toContain("decoration-mark-variant");
    expect(marked.className).toContain("decoration-dotted");
    expect(marked).toHaveAccessibleDescription("nur in TR, Byz");
    await user.hover(marked);
    expect(await screen.findByTestId("original-word-info")).toHaveTextContent("nur in TR, Byz");
  });

  it("names the missing editions when the word is in at least half of them", () => {
    renderWords([greek(1, "λόγος", { editionCodes: ["NA28", "NA27", "Tyn", "SBL", "TR", "Byz"] })]);
    expect(screen.getByRole("link", { name: /λόγος/ })).toHaveAccessibleDescription(
      "fehlt in WH, Treg",
    );
  });

  it("leaves words in all main editions and shift-only words unmarked", () => {
    renderWords([greek(1, "πάτερ"), greek(2, "ἡμῶν", { hasShift: true })]);
    for (const name of [/πάτερ/, /ἡμῶν/]) {
      const link = screen.getByRole("link", { name });
      expect(link.className).not.toContain("decoration-mark-variant");
      expect(link).not.toHaveAccessibleDescription();
    }
  });

  it("marks the word in the eye panel as well", () => {
    renderWords([greek(1, "ὅτι", { editionCodes: ["TR", "Byz"] }), greek(2, "ἀμήν")], "panel");
    const marked = screen.getByText("ὅτι");
    expect(marked.className).toContain("decoration-mark-variant");
    expect(marked).toHaveAccessibleDescription("nur in TR, Byz");
    expect(screen.getByText("ἀμήν").className).not.toContain("decoration-mark-variant");
  });
});
