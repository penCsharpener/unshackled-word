import { fireEvent, render, screen } from "@testing-library/svelte";
import { createRawSnippet } from "svelte";
import { expect, test, vi } from "vitest";
import Button from "./Button.svelte";

test("renders button with correct text and handles click", async () => {
  const handleClick = vi.fn();

  // Svelte 5 requires snippets for children content in tests
  render(Button, {
    onclick: handleClick,
    children: createRawSnippet(() => ({
      render: () => "Click Me",
      setup: () => {},
    })),
  });

  const btn = screen.getByRole("button", { name: /click me/i });
  await fireEvent.click(btn);

  expect(handleClick).toHaveBeenCalledTimes(1);
});
