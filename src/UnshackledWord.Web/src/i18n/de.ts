export const de = {
  app: { name: "Unshackled Word" },
  nav: { home: "Start", bible: "Bibel" },
  theme: { label: "Darstellung", light: "Hell", dark: "Dunkel", system: "System" },
  language: { label: "Sprache", de: "Deutsch", en: "English" },
  home: {
    title: "Das freie Wort",
    lead: "Bibelstudium in Deutsch, Griechisch und Hebräisch, frei von Urheberrechten und Lizenzen.",
  },
  bible: {
    title: "Bibel",
    lead: "Elberfelder Übersetzung 1871. Wähle ein Buch und ein Kapitel.",
    structureError: "Die Bücherliste konnte nicht geladen werden.",
    retry: "Erneut versuchen",
    testament: { ot: "Altes Testament", nt: "Neues Testament" },
    picker: {
      label: "Buch und Kapitel wählen",
      loading: "Bücher werden geladen …",
      empty: "Keine Bücher verfügbar.",
      showChapters: "Kapitel von {{book}} anzeigen",
      chapterLink: "{{book}} {{chapter}}",
      psalmsSection: "{{book}}, Buch {{number}} ({{from}}–{{to}})",
    },
    chapter: {
      loading: "Kapitel wird geladen …",
      error: "Das Kapitel konnte nicht geladen werden.",
      empty: "Für dieses Kapitel ist kein Text vorhanden.",
      notFound: "Dieses Kapitel gibt es nicht.",
      toOverview: "Zur Bücherübersicht",
      previous: "Vorheriges Kapitel: {{reference}}",
      next: "Nächstes Kapitel: {{reference}}",
      noPrevious: "Kein vorheriges Kapitel",
      noNext: "Kein nächstes Kapitel",
    },
  },
  notFound: { title: "Seite nicht gefunden", back: "Zur Startseite" },
};

export type Translation = typeof de;
