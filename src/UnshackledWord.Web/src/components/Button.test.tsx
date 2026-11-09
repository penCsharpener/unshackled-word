import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Button, buttonClasses } from "./Button";

describe("Button", () => {
  it("defaults to type=button and fires onClick", async () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Save</Button>);

    const button = screen.getByRole("button", { name: "Save" });
    expect(button).toHaveAttribute("type", "button");

    await userEvent.click(button);
    expect(onClick).toHaveBeenCalledOnce();
  });

  it("does not fire onClick when disabled", async () => {
    const onClick = vi.fn();
    render(
      <Button disabled onClick={onClick}>
        Save
      </Button>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(onClick).not.toHaveBeenCalled();
  });
});

describe("buttonClasses", () => {
  it("returns the classes the Button uses", () => {
    render(
      <Button variant="primary" size="sm">
        Go
      </Button>,
    );
    const classes = buttonClasses({ variant: "primary", size: "sm" });
    expect(screen.getByRole("button", { name: "Go" }).className).toContain(classes);
  });

  it("defaults to secondary md", () => {
    expect(buttonClasses()).toBe(buttonClasses({ variant: "secondary", size: "md" }));
    expect(buttonClasses()).toContain("px-4 py-2");
  });

  it("inline variant keeps text-flow classes only", () => {
    const classes = buttonClasses({ variant: "inline", size: "sm" });
    expect(classes).not.toContain("inline-flex");
    expect(classes).not.toContain("font-medium");
    expect(classes).not.toContain("px-4");
    expect(classes).toBe(buttonClasses({ variant: "inline" }));
  });

  it("renders an inline Button with type=button", () => {
    render(<Button variant="inline">word</Button>);
    expect(screen.getByRole("button", { name: "word" })).toHaveAttribute("type", "button");
  });
});
