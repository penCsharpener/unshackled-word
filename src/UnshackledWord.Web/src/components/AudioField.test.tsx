import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { ApiError } from "@/lib/api";
import { AudioField } from "./AudioField";

const play = vi.fn(() => Promise.resolve());
beforeEach(async () => {
  await i18n.changeLanguage("de");
  play.mockClear();
  vi.stubGlobal(
    "Audio",
    class {
      onended = null;
      onerror = null;
      pause = vi.fn();
      play = play;
    },
  );
});

describe("AudioField", () => {
  it("renders nothing read-only without a source and a play button with one", () => {
    const { container, rerender } = render(<AudioField src={null} readOnly />);
    expect(container).toBeEmptyDOMElement();
    rerender(<AudioField src="https://x/a.mp3" readOnly />);
    expect(screen.getByRole("button", { name: "Aussprache abspielen" })).toBeInTheDocument();
  });

  it("plays the source on click", async () => {
    render(<AudioField src="https://x/a.mp3" readOnly />);
    await userEvent.click(screen.getByRole("button", { name: "Aussprache abspielen" }));
    expect(play).toHaveBeenCalled();
  });

  it("hands a picked MP3 to onUpload", async () => {
    const onUpload = vi.fn(() => Promise.resolve());
    const { container } = render(<AudioField src={null} onUpload={onUpload} />);
    const file = new File(["x"], "a.mp3", { type: "audio/mpeg" });
    await userEvent.upload(container.querySelector("input[type=file]") as HTMLInputElement, file);
    expect(onUpload).toHaveBeenCalledWith(file);
  });

  it("rejects a file above 1 MB without calling onUpload", async () => {
    const onUpload = vi.fn(() => Promise.resolve());
    const { container } = render(<AudioField src={null} onUpload={onUpload} />);
    const big = new File([new Uint8Array(1024 * 1024 + 1)], "a.mp3", { type: "audio/mpeg" });
    await userEvent.upload(container.querySelector("input[type=file]") as HTMLInputElement, big);
    expect(onUpload).not.toHaveBeenCalled();
    expect(screen.getAllByText("MP3, höchstens 1 MB.").length).toBeGreaterThan(1);
  });

  it("calls onDelete", async () => {
    const onDelete = vi.fn(() => Promise.resolve());
    render(<AudioField src="https://x/a.mp3" onDelete={onDelete} />);
    await userEvent.click(screen.getByRole("button", { name: "Löschen" }));
    expect(onDelete).toHaveBeenCalled();
  });

  it("shows the storage message on a 503", async () => {
    const onUpload = vi.fn(() => Promise.reject(new ApiError(503, "unavailable")));
    const { container } = render(<AudioField src={null} onUpload={onUpload} />);
    await userEvent.upload(
      container.querySelector("input[type=file]") as HTMLInputElement,
      new File(["x"], "a.mp3", { type: "audio/mpeg" }),
    );
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Audio-Speicher ist nicht eingerichtet.",
    );
  });
});
