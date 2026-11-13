# Roadmap 0006: More ideas (index)

Collected reader and mapping-editor ideas. None of them has its own roadmap yet; this file keeps them in one structured
place and points to the roadmaps they touch. Turn an idea into a numbered roadmap with `create-roadmap` when it is next.

## Ideas

### 1. Sticky top menu and chapter selector (reader)

- The top menu and the chapter selector stay visible when the user scrolls down.
- Because they are sticky, make them a bit thicker. Hovering a word shows the original word, its lemma and its
  morphology in the centre of the top menu.

### 2. Eye icon for the original verse (reader)

- The current "Zuordnung" button becomes an eye icon.
- First click: the current menu opens with only the original verse in it. A second click opens the Zuordnungs view/form.
- Ctrl + click on the eye icon after the verse opens the alignment menu directly.
- A casual reader can open the original verse (without transliteration and numbers above the words) with one click.
- Optional: a switch to add Strong's and morphology buttons to each original word.

### 3. Alignment menu: connecting lines (mapping editor)

- The connecting lines between an original and a German word are hidden behind the word cards. The blue line should be
  drawn above them.

## Related roadmaps

| Idea | Related roadmap |
|---|---|
| 1 (hover shows original word, lemma, morphology) | [0002 Word detail popup](0002-word-detail-popup.md): the same word data; reuse its source and decide whether the top-menu display replaces or complements the popup |
| 2, 3 (Zuordnung button, alignment menu) | [0003 Mapping editor](0003-mapping-editor.md): the editor these ideas change; [0004 Original word relations](0004-original-word-relations.md) also draws relations in it, so check line layering together |
| 1, 2 (Strong's / morphology per original word) | [0008 Mapping stats snapshot](0008-mapping-stats-snapshot.md) and [0009 Smart search](0009-smart-search.md) for the Strong's look and data |

## Open questions

- Should the hover display in the top menu replace the word detail popup (0002) or work next to it?
- Which mobile behaviour for the sticky bars (hover does not exist on touch)?
