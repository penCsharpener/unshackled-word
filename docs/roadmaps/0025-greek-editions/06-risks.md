# 0025 · Risks worth naming before starting

- **"Edition" read as "manuscript" teaches something wrong.** The request spoke of manuscripts; the data is printed critical and traditional editions, built from many manuscripts each. A reader who sees "fehlt in WH" and thinks "missing in a manuscript" draws wrong conclusions about the text's history. Mitigation: labels say "Textausgabe", the info line explains it, and only catalog rows of `Kind = 'manuscript'` are called manuscripts.

- **"Only in TR" can read as a verdict.** Showing TR/Byz words as marked and hiding them under the NA28 filter can look like the app declares them spurious. The underline must stay neutral (no warning colour), the filter default is "Alle", and the tooltip states facts ("nur in TR, Byz"), not judgements.

- **The parser lives only in SQL, and a STEP release can change the grammar.** New codes are caught (unknown catalog rows plus the 0025.3 check), but a new suffix form would land in `ShiftRaw` and a new separator would make whole strings one unknown code. Signal: the data-quality check reports unknown codes after an import. The raw `Editions` column is kept, so a fix is a function change and a re-run, never a re-download.

- **Fresh database order.** Migrations run before SeedDb, so on a fresh DB the migration's refresh writes zero rows; without 0025.2 the feature is silently empty for every new self-hoster. 0025.2's "Done when" is a fresh-stack check for that reason, and 0025.7 repeats it.

- **Re-importing STEP rewrites `StepGreekWords` ids.** `StepGreekWordEditions` cascades on delete and is rebuilt by the refresh, so it cannot go stale on its own; but any later feature that stores edition facts keyed by `StepWordId` outside this table (notes, curation) would. Not solved here; the signal is the first such feature request.

- **The filter hides words, it does not reconstruct an edition.** Order shifts and spelling differences are not applied, so "NA28" in the panel is the TAGNT text minus non-NA28 words, not the NA28 text. The hidden-words line and the info icon must say so; a real per-edition text is deferred.

- **About 1.1M rows for a fact that is 92 % "all eight".** Storing only exceptions would be ~10× smaller, but the maintainer chose full n:m for simple queries. At this project's scale (one Postgres, small user base) the size is acceptable; revisit if the dev DB backup time noticeably grows.
