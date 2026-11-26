import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
import {
  resetSearchState,
  SEARCH_MARK_KEY,
  SEARCH_QUERY_KEY,
  SEARCH_TAB_KEY,
} from "@/lib/search/searchState";

// The search state is a module store kept in localStorage: every test starts with an empty search.
afterEach(() => {
  cleanup();
  try {
    for (const key of [SEARCH_QUERY_KEY, SEARCH_TAB_KEY, SEARCH_MARK_KEY]) {
      localStorage.removeItem(key);
    }
  } catch {
    // a test replaced the storage
  }
  resetSearchState();
});

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
