# 0013 · Goal

Modernising Elb1871 mostly means swapping single words (Weib => Frau, Oheim => Onkel, Muhme => Tante). Today that is one
verse at a time in the revision editor. A variant is already a full copy of the verse and the save path already carries the
mapping of unchanged words (`RevisionCarry`), so storage and carry-over need no new model; what is missing is a way to find
all occurrences and change them in one pass.

End state: a maintainer opens `/edit/revision/replace`, picks a source and a target revision, searches a German word,
optionally narrows by the mapped Strong's numbers, types a replacement, unchecks rows or overrides the replacement per row
(inflections, articles) and applies. Every touched verse gets a new version of the target revision with the mapping carried
over; the replaced word is flagged "changed" and shows up for review in the mapping editor.

Out of scope: synonym lists or AI suggestions, grammar repair (article agreement is fixed by hand per row), undo as a feature
(each apply creates versions, history already exists), thousands of rows or a background job, changing the model of
`RevisionWords`/`RevisionMapping`.
