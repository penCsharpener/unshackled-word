# 0013 · Risks worth naming before starting

- **Review fatigue.** Hundreds of `Weib` verses one by one invite blind "Weiter" clicks, and a wrong article then goes live. Quick mode only skips the thinking about verses without an article; do not add "confirm all without looking".
- **A half-finished verse is saved.** The text version is stored when moving to step 3. If the maintainer leaves, the verse sits in the revision with `MappingReviewedAt` empty. It shows in the revision overview's unreviewed list, but the reader already shows the new text with a carried mapping.
- **Mapping edits are not versioned.** Text history is complete; who changed which link is not (rows are rewritten in place). Accepted here; versioning the mapping would need its own history table.
- **Many "prüfen" marks.** `ein Weib` => `eine Frau` changes two words, so the carry-over marks both and verses with several adjacent changes can lose links (`lostOriginalWordIds`). Step 3 is where that is fixed; it is why step 3 exists.
- **Queue lives in one browser.** `localStorage` is lost with the browser or a cleared profile. Saved verses are safe (they are on the server); only the list of open verses is lost and a new search rebuilds it.
- **Revisions are still addressed by level.** The search resolves `code` to a level, unique only among non-base revisions. If that ever changes, the save and history queries must move to revision ids first.
- **Concurrent editing.** A verse saved by someone else between search and review is detected by `expectedVersion` on save and reloads the current text; the reviewer decides again.
- **Growth.** Every save writes a full set of words and mapping rows (about 46 rows); repeated edits multiply that. Pruning ended versions is deferred.
- **Search index is a guess.** `lower("PlainWord")` may not suffice for `sameLemma`; check the plan on the dev DB before 0013.2 closes.
