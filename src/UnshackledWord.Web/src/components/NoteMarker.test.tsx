import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { NoteMarker } from "./NoteMarker";

describe("NoteMarker", () => {
  it("is a labelled button without a count for a single note", async () => {
    const onClick = vi.fn();
    render(<NoteMarker label="Notiz zu Vers 28 öffnen" onClick={onClick} />);
    const marker = screen.getByRole("button", { name: "Notiz zu Vers 28 öffnen" });
    expect(marker).toHaveTextContent("");
    expect(marker.className).toContain("text-fg-muted");
    await userEvent.click(marker);
    expect(onClick).toHaveBeenCalledOnce();
  });

  it("shows the count from two notes on and the own colour", () => {
    render(<NoteMarker label="2 Notizen" count={2} own onClick={() => {}} />);
    const marker = screen.getByRole("button", { name: "2 Notizen" });
    expect(marker).toHaveTextContent("2");
    expect(marker.className).toContain("text-accent");
  });
});
