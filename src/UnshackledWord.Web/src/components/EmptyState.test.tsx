import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { EmptyState } from "./EmptyState";

describe("EmptyState", () => {
  it("renders title and message", () => {
    render(<EmptyState title="Nothing here">No results.</EmptyState>);
    expect(screen.getByText("Nothing here")).toBeInTheDocument();
    expect(screen.getByText("No results.")).toBeInTheDocument();
  });

  it("has no skeleton by default", () => {
    const { container } = render(<EmptyState>No results.</EmptyState>);
    expect(container.querySelectorAll("[aria-hidden='true']")).toHaveLength(0);
  });

  it("adds hidden skeleton rows for comingSoon", () => {
    const { container } = render(
      <EmptyState variant="comingSoon" skeletonRows={3}>
        Later.
      </EmptyState>,
    );
    expect(container.querySelectorAll("[aria-hidden='true']")).toHaveLength(3);
    expect(screen.getByText("Later.")).toBeVisible();
  });
});
