import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { Button } from "./Button";
import { Dialog } from "./Dialog";

function Harness({
  dismissible = true,
  onClose = vi.fn(),
}: {
  dismissible?: boolean;
  onClose?: () => void;
}) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button onClick={() => setOpen(true)}>Öffnen</Button>
      <Dialog
        open={open}
        title="Titel"
        description="Beschreibung"
        dismissible={dismissible}
        footer={<span>Fuß</span>}
        onClose={() => {
          onClose();
          setOpen(false);
        }}
      >
        <input aria-label="Feld" />
      </Dialog>
    </>
  );
}

describe("Dialog", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("renders content only while open, labelled by its title", async () => {
    render(<Harness />);
    expect(screen.queryByLabelText("Feld")).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: "Öffnen" }));
    const dialog = screen.getByRole("dialog", { name: "Titel" });
    expect(dialog).toHaveAccessibleDescription("Beschreibung");
    expect(screen.getByLabelText("Feld")).toBeInTheDocument();
    expect(screen.getByText("Fuß")).toBeInTheDocument();
  });

  it("closes with Esc and the close button and returns focus to the opener", async () => {
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);
    const opener = screen.getByRole("button", { name: "Öffnen" });
    opener.focus();
    await userEvent.click(opener);
    await userEvent.keyboard("{Escape}");
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(opener).toHaveFocus();

    await userEvent.click(opener);
    await userEvent.click(screen.getByRole("button", { name: "Schließen" }));
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it("ignores Esc and the close button when not dismissible", async () => {
    const onClose = vi.fn();
    render(<Harness dismissible={false} onClose={onClose} />);
    await userEvent.click(screen.getByRole("button", { name: "Öffnen" }));
    await userEvent.keyboard("{Escape}");
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Schließen" })).toBeDisabled();
  });

  it("keeps the title as the accessible name when it is hidden and can dock at the top", () => {
    render(
      <Dialog open onClose={() => {}} title="Suche" titleHidden placement="top" size="lg">
        <input aria-label="Feld" />
      </Dialog>,
    );
    const dialog = screen.getByRole("dialog", { name: "Suche" });
    expect(screen.getByRole("heading", { name: "Suche" }).parentElement).toHaveClass("sr-only");
    expect(dialog).toHaveClass("md:mt-12");
    expect(dialog).not.toHaveClass("m-auto");
  });
});
