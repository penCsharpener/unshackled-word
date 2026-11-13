import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { TextField } from "./TextField";

describe("TextField", () => {
  it("is labelled and reports typed text", async () => {
    const onChange = vi.fn();
    render(<TextField label="Name" value="" onChange={onChange} />);
    await userEvent.type(screen.getByLabelText("Name"), "a");
    expect(onChange).toHaveBeenCalledWith("a");
  });

  it("shows a decorative magnifier for type=search and keeps the input searchable", () => {
    const { container } = render(
      <TextField type="search" label="Suche" value="" onChange={() => {}} required={false} />,
    );
    expect(screen.getByRole("searchbox", { name: "Suche" })).toBeInTheDocument();
    expect(container.querySelector("svg[aria-hidden='true']")).not.toBeNull();
  });

  it("has no icon for other types", () => {
    const { container } = render(<TextField label="Name" value="" onChange={() => {}} />);
    expect(container.querySelector("svg")).toBeNull();
  });
});
