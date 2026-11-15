import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import "@/i18n";
import { OriginalVerse } from "./OriginalVerse";

/** Longer than the card's hide delay (500 ms). */
const pastHideDelay = () => new Promise((resolve) => setTimeout(resolve, 650));

function renderMargin() {
  return render(
    <MemoryRouter>
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
    </MemoryRouter>,
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
});
