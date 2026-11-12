import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

afterEach(cleanup);

// jsdom has no Popover API and hides every [popover] element, so emulate showPopover().
window.HTMLElement.prototype.showPopover = function showPopover(this: HTMLElement) {
  this.style.setProperty("display", "block");
};

// jsdom has no <dialog> modal API: emulate showModal() / close().
window.HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
  this.setAttribute("open", "");
};
window.HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
  this.removeAttribute("open");
  this.dispatchEvent(new Event("close"));
};
