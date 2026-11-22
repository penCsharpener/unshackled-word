import { describe, expect, it } from "vitest";
import { type Note, noteHref } from "./api";

const at = (anchor: Note["anchor"]) => ({ id: "n1", anchor }) as Note;

describe("noteHref", () => {
  it("marks the verse, opens the note and hashes the verse", () => {
    expect(noteHref(at({ kind: "verse", bookId: 45, chapter: 8, startHebRefId: 45008028 }))).toBe(
      "/bible/45/8?v=28&note=n1#v28",
    );
  });

  it("marks a verse range and a words note's verse", () => {
    expect(
      noteHref(
        at({
          kind: "verses",
          bookId: 45,
          chapter: 8,
          startHebRefId: 45008028,
          endHebRefId: 45008030,
        }),
      ),
    ).toBe("/bible/45/8?v=28-30&note=n1#v28");
    expect(
      noteHref(
        at({
          kind: "words",
          bookId: 45,
          chapter: 8,
          startHebRefId: 45008028,
          startPosition: 1,
          endPosition: 2,
        }),
      ),
    ).toBe("/bible/45/8?v=28&note=n1#v28");
  });

  it("links a chapter note to the chapter", () => {
    expect(noteHref(at({ kind: "chapter", bookId: 45, chapter: 8 }))).toBe("/bible/45/8?note=n1");
  });
});
