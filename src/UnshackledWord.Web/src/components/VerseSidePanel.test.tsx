import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import "@/i18n";
import { VerseSidePanel } from "./VerseSidePanel";

describe("VerseSidePanel", () => {
  it("is a labelled group with its content", () => {
    render(
      <VerseSidePanel mode="original" label="Grundtext von Vers 3">
        <p>Ἦν</p>
      </VerseSidePanel>,
    );
    expect(screen.getByRole("group", { name: "Grundtext von Vers 3" })).toHaveTextContent("Ἦν");
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("lists every reference of a tsk panel without a +N button", () => {
    render(
      <VerseSidePanel mode="tsk" label="Querverweise zu Vers 1">
        <ul>
          <li>A</li>
          <li>B</li>
          <li>C</li>
        </ul>
      </VerseSidePanel>,
    );
    expect(screen.getAllByRole("listitem")).toHaveLength(3);
    expect(screen.queryByRole("button")).toBeNull();
  });
});
