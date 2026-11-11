import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Select } from "./Select";

const options = [
  { value: "", label: "All" },
  { value: "a", label: "Alpha" },
  { value: "b", label: "Beta" },
];

describe("Select", () => {
  it("is labelled and shows the selected option", () => {
    render(<Select label="Kind" value="a" onChange={() => {}} options={options} />);
    expect(screen.getByRole("combobox", { name: "Kind:" })).toHaveValue("a");
  });

  it("reports the chosen value", async () => {
    const onChange = vi.fn();
    render(<Select label="Kind" value="" onChange={onChange} options={options} />);
    await userEvent.selectOptions(screen.getByRole("combobox"), "b");
    expect(onChange).toHaveBeenCalledWith("b");
  });

  it("can be disabled", () => {
    render(<Select label="Kind" value="" onChange={() => {}} options={options} disabled />);
    expect(screen.getByRole("combobox")).toBeDisabled();
  });
});
