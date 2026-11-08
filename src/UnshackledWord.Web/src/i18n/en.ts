import type { Translation } from "./de";

export const en: Translation = {
  app: { name: "Unshackled Word" },
  nav: { home: "Home", bible: "Bible" },
  theme: { label: "Appearance", light: "Light", dark: "Dark", system: "System" },
  language: { label: "Language", de: "Deutsch", en: "English" },
  home: {
    title: "The free word",
    lead: "Bible study in German, Greek and Hebrew, free of copyright and licence strings.",
  },
  bible: {
    title: "Bible",
    lead: "Elberfelder translation 1871. Choose a book and a chapter.",
    structureError: "The list of books could not be loaded.",
    retry: "Try again",
    testament: { ot: "Old Testament", nt: "New Testament" },
    picker: {
      label: "Choose book and chapter",
      loading: "Loading books …",
      empty: "No books available.",
      showChapters: "Show chapters of {{book}}",
      chapterLink: "{{book}} {{chapter}}",
      psalmsSection: "{{book}}, book {{number}} ({{from}}–{{to}})",
    },
    chapter: {
      loading: "Loading chapter …",
      error: "The chapter could not be loaded.",
      empty: "There is no text for this chapter.",
      notFound: "This chapter does not exist.",
      toOverview: "To the book overview",
      previous: "Previous chapter: {{reference}}",
      next: "Next chapter: {{reference}}",
      noPrevious: "No previous chapter",
      noNext: "No next chapter",
    },
  },
  notFound: { title: "Page not found", back: "Back to start" },
};
