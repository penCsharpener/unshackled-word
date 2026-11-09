import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

afterEach(cleanup);

// jsdom has no Popover API and hides every [popover] element, so emulate showPopover().
window.HTMLElement.prototype.showPopover = function showPopover(this: HTMLElement) {
  this.style.setProperty("display", "block");
};
