# Roadmap 0007: Smart search and seed (index)

The ideas collected here were split into three roadmaps:

| Roadmap | Covers |
|---|---|
| [0008 Mapping stats snapshot](0008-mapping-stats-snapshot.md) | Pre-aggregated German lemma ↔ Strong's frequencies, refresh, Strong's page and word stats on the snapshot, the per-word lookup endpoint `GET api/elberfelder/search/originals` (0008.5) and hook `useOriginalsLookup` |
| [0009 Smart search overlay](0009-smart-search.md) | `F` / magnifier overlay in the reader: full text search over `Elb1871Verses` (German config without stop words, lemma widening, ranking, marked hits, hits per book), originals per query word in the Strong's page look (reads 0008.5) |
| [0010 Repo seed export/import and STTS](0010-export-import-seed.md) | `Elb1871Morphology` table and import (0008.0), stable keys and re-anchoring for exporting morphology, mappings and later the revision into the repo, compose seed semantics, STTS tag table and tag validation |

Dependencies: 0010.1 + 0010.4 unblock 0008; 0009's verse search runs without the others, its lemma widening needs
0008.4 and 0010.4, its originals panel needs 0008.5f.
