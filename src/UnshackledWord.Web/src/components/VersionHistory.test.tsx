import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n";
import { VersionHistory } from "./VersionHistory";

const versions = [
  {
    version: 2,
    text: "Gott liebte die Welt",
    createdAt: "2026-01-02T10:00:00Z",
    createdBy: "anna",
  },
  {
    version: 1,
    text: "Gott hat die Welt geliebt",
    createdAt: "2026-01-01T10:00:00Z",
    note: "Start",
  },
];

describe("VersionHistory", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("de");
  });

  it("is collapsed by default and lists versions newest first with a diff", () => {
    const { container } = render(
      <VersionHistory versions={versions} onRestore={() => {}} onRemove={() => {}} canRemove />,
    );
    expect(container.querySelector("details")).not.toHaveAttribute("open");
    const items = screen.getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent("v2");
    expect(items[1]).toHaveTextContent("v1");
    expect(items[0]?.querySelector("ins")).toHaveTextContent("liebte");
    expect(items[0]?.querySelector("del")).toHaveTextContent("hat");
    expect(items[1]).toHaveTextContent("Start");
  });

  it("restores the text of a version", async () => {
    const onRestore = vi.fn();
    render(
      <VersionHistory versions={versions} onRestore={onRestore} onRemove={() => {}} canRemove />,
    );
    const items = screen.getAllByRole("listitem");
    await userEvent.click(
      within(items[1] as HTMLElement).getByRole("button", { name: "wiederherstellen" }),
    );
    expect(onRestore).toHaveBeenCalledWith("Gott hat die Welt geliebt");
  });

  it("needs a confirmation before removing", async () => {
    const onRemove = vi.fn();
    render(
      <VersionHistory versions={versions} onRestore={() => {}} onRemove={onRemove} canRemove />,
    );
    await userEvent.click(screen.getByText("Revision dieser Stufe entfernen"));
    expect(onRemove).not.toHaveBeenCalled();
    const dialog = screen.getByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Entfernen" }));
    expect(onRemove).toHaveBeenCalledTimes(1);
  });

  it("hides the remove button when not allowed", () => {
    render(
      <VersionHistory
        versions={versions}
        onRestore={() => {}}
        onRemove={() => {}}
        canRemove={false}
      />,
    );
    expect(screen.queryByText("Revision dieser Stufe entfernen")).toBeNull();
  });
});
