import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";

// The real MDXEditor (Lexical) in jsdom: enough to prove the lazy chunk loads and mounts with the toolbar.
beforeEach(async () => {
  await i18n.changeLanguage("de");
});

describe("NoteEditor (real MDXEditor)", () => {
  it("loads as a separate module and renders the toolbar and the body", async () => {
    const module = await import("./NoteEditor");
    expect(module.default).toBe(module.NoteEditor);
    const onSave = vi.fn();
    const { container } = render(
      <module.NoteEditor
        label="Neue Notiz zu Römer 8,28"
        initialMarkdown="**fett** und mehr"
        onSave={onSave}
        onCancel={() => {}}
      />,
    );
    expect(screen.getByText("Neue Notiz zu Römer 8,28")).toBeInTheDocument();
    await waitFor(() => expect(container.querySelector(".note-md-content strong")).not.toBeNull());
    expect(container.querySelector(".note-md-content strong")?.textContent).toBe("fett");
    expect(screen.getByLabelText("Fett")).toBeInTheDocument();
    expect(screen.getByLabelText("Aufzählung")).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "s", ctrlKey: true });
    expect(onSave).toHaveBeenCalledWith(expect.stringContaining("**fett**"));
  });
});
