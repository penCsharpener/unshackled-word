import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { Button } from "./Button";
import { ToastProvider, useToast } from "./Toast";

function Trigger() {
  const toast = useToast();
  return (
    <>
      <Button onClick={() => toast.show("Saved.")}>ok</Button>
      <Button onClick={() => toast.show("Broken.", "error")}>fail</Button>
    </>
  );
}

const setup = () =>
  render(
    <ToastProvider>
      <Trigger />
    </ToastProvider>,
  );

describe("Toast", () => {
  beforeEach(() => i18n.changeLanguage("en"));
  afterEach(() => vi.useRealTimers());

  it("shows a success toast in the polite status region", async () => {
    setup();
    await userEvent.click(screen.getByRole("button", { name: "ok" }));
    expect(screen.getByRole("status")).toHaveTextContent("Saved.");
  });

  it("puts errors in the assertive live region", async () => {
    setup();
    await userEvent.click(screen.getByRole("button", { name: "fail" }));
    const text = screen.getByText("Broken.");
    expect(text.closest("[aria-live]")).toHaveAttribute("aria-live", "assertive");
    expect(screen.getByRole("status")).not.toHaveTextContent("Broken.");
  });

  it("closes with the close button, labelled per language", async () => {
    await i18n.changeLanguage("de");
    setup();
    await userEvent.click(screen.getByRole("button", { name: "ok" }));
    await userEvent.click(screen.getByRole("button", { name: "Hinweis schließen" }));
    expect(screen.queryByText("Saved.")).not.toBeInTheDocument();
  });

  it("dismisses itself after a few seconds", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    setup();
    await userEvent.click(screen.getByRole("button", { name: "ok" }));
    expect(screen.getByText("Saved.")).toBeInTheDocument();
    act(() => {
      vi.advanceTimersByTime(5100);
    });
    expect(screen.queryByText("Saved.")).not.toBeInTheDocument();
  });

  it("throws outside the provider", () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => render(<Trigger />)).toThrow("ToastProvider");
  });
});
