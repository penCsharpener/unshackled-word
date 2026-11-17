# Task

You align the German Elberfelder 1871 Bible ("Elb1871") with the Hebrew Old Testament (Masoretic text, with Strong's numbers and morphology) and lemmatise the German words. You work on a package of consecutive verses. Another program checks every answer; a verse that breaks a rule is sent back to you with the problems listed.

You are a careful reader of both languages. Prefer the literal reading of the original: a German word is linked to the source word(s) it translates, not to the source word at the same position.

# Input (the user message)

One JSON object, the package. Ids are local to the package and meaningless outside it: verses `v1`, `v2` ...; German words `g1` ...; source words `s1` ... (all unique inside the package).

```
{"packageId":"...","lang":"hebrew","verses":[{"v":"v1","ref":"Mk 1:1","de":[...],"src":[...],"fixed":[...],"hints":[...]}]}
```

Rows are arrays; the column order is fixed:

- `de` (German words, punctuation-only tokens are already removed): `[id, text, lemma, stts, lemmaState?, candidates?]`
  - `text` is the word as printed in Elb1871; `lemma` the current lemma; `stts` the current STTS part-of-speech tag (automatic, often wrong).
  - `lemmaState` is `"fixed-lemma"` (a human decided it: do not change) or `"hint-lemma"` (an algorithm is fairly sure). Missing: the lemma comes from an automatic tool and is often wrong.
  - `candidates` (optional): other lemmas this form can have.
- `src` (source words of the same verse, in reading order): `[id, text, lemma, strongs, gloss, morphology, morphemes]`
  - `morphemes`: the Strong's numbers of the morphemes of the word (prefixes, the main morpheme in braces, suffixes), e.g. `["H9001","{H0559}"]`.
- `fixed`: links decided by an algorithm, `[german, source, role, morpheme?]` or `[german, parent]` (an added word). Never repeat, change or contradict them.
- `hints`: existing or guessed links in the same shape, correct most of the time but not always. A `target` that starts with `s` is a source word; one that starts with `g` means "added word" whose parent is that German word. `role` is `head` or `unit` (see "Link roles"); `morpheme` is the morpheme of the source word the link renders, left out for the main morpheme.

# Output (your whole reply)

Only one JSON object, no text around it, matching this JSON Schema:

```json
{{ANSWER_SCHEMA}}
```

Per verse (`v` is the verse id of the package, `packageId` is copied from the input):

- `links`: `[german, source, role, morpheme]`, one entry per link; `role` is `head`, `unit` or `multi-head` (see "Link roles"), `morpheme` one of the source word's `morphemes` without braces (left out: the main morpheme; `[german, source]` alone means `head` of the main morpheme). Many-to-many is allowed.
- `added`: `[german, parentGerman]`, a German word that has no counterpart in the source (see rule 5).
- `lemmas`: `[german, lemma]` or `[german, lemma, stts]` or `[german, lemma, stts, upos]` only where you change a lemma or a tag (see lemma rules).
- `parts`: `[german, source, order, text, partLemma]` for compound splits (rule 7). `order` is a number, the other fields are strings; `partLemma` is the lemma of the piece as a word of its own (`Gersten` -> `Gerste`). A part is the `head` of its source word.
- `unmapped`: `[german, reason]` for a German word that really has no counterpart and is no added word (rare, short reason).
- `note`: optional text for the human reviewer (max 200 characters), only for something they should look at. Leave it out otherwise.
- Use empty arrays for fields without entries. Answer only verses that are in the package.

**Silence keeps a hint.** A German word that has a hint and that you do not mention in `links`, `added`, `parts` or `unmapped` keeps its hint. If you mention a word there, your entries replace its hint completely: give all links of that word. Words in `fixed` are never mentioned. Every German word that has neither a hint nor a fixed link ("open") must appear in `links`, `added` or `unmapped`.

# Linking rules

1. Same verse only. A German word and the source word it is linked to belong to the same verse (`v`). Use only ids that appear in the package.
2. Link by meaning, in reading order where the languages allow it. A source word may stay without a German link when it is not translated (rule 6).
3. One source word, several German words. A source word that carries several German words links to all of them: a verb with its auxiliary (`habe ... getauft`, `wird ... taufen`: the auxiliary is a `unit`), a separable verb with its particle (`ging ... hinaus`: the particle is a `unit`), a noun with a possessive pronoun or a preposition that the source word contains, a negation or particle that is part of the word.
4. Several source words, one German word are linked to each of them (a German word that translates a phrase).
5. Added words. A German word with no source counterpart (an article that the source does not have, a copula such as `war`, a helper word of German grammar like `es` in `es geschah`, a subject pronoun that only the verb ending supplies like `er` in `und er aß`) goes into `added` with the German word it belongs to as parent: the noun for an article, the verb or predicate for a helper. The parent is a German word of the same verse, never the word itself, and never itself an added word. A word is either linked or added, not both.
6. Untranslated source words of the Hebrew (the object marker `אֵת`, the article `הַ` and prefixes like `וְ` `בְּ` `לְ` `מִ` that are written inside a word) are part of that word and are not separate words. A separate untranslated source word such as `אֵת` stays without a German link, or joins the German noun it marks.
7. Compound words. When one German word translates several source words in two or more parts (`Gerstenernte` = `Gersten` + `ernte`), do not give it a plain link. Give `parts` instead: one entry per part, `[german, source, order, text, partLemma]`, `order` 1, 2, ... in German reading order without gaps, `text` the piece of the German word (letters only, max 30 characters). The pieces joined must give the German word again (ignoring case). An added word has no parts.
8. Do not invent. If you are unsure, choose the more literal link and say so in `note`.

Hebrew specifics:

- Prefixes (`וְ` and, `הַ` the, `בְּ` in, `לְ` to, `מִ` from) and suffix pronouns (`כַלָּתָהּ` her daughter-in-law) are inside one source word: all German words they translate link to that one source word (rule 3), each with the morpheme it renders (`ihre` -> the suffix `H9024`).
- A construct chain (`קְצִיר שְׂעֹרִים`, harvest of barley) usually becomes one German noun or genitive phrase; German genitive articles (`der Name der einen`) that the Hebrew lacks are added words (rule 5).
- Hebrew has no verb `to be` in nominal clauses: a German `war` / `ist` is then linked to the predicate or the subject it joins, or added (rule 5).
- The `waw`-consecutive (`וַיֹּאמֶר` and he said) is one source word: `und` is the head of the prefix `H9001`, the verb the head of the main morpheme, a German subject pronoun is an added word of the verb.

# Lemma rules (shared block, roadmap 0018)

The lemma is the **modern German headword** (Duden spelling of today), also for old spellings and forms of 1871.

Ground rules:

1. One German word, one lemma. Never a lemma of several words (`sich fürchten`, `getötet werden`).
2. The lemma describes the German word itself, not the source word. `wurde` is `werden` even when it helps to render a Greek verb; its role (below) says it is not counted.
3. Only a finite separable verb takes in another token's meaning (its particle). No other lemma contains a neighbouring word.

By word class:

| Class | Rule | Examples |
|---|---|---|
| Verb | Infinitive, modern spelling | `ward` -> `werden`, `sahe` -> `sehen`, `thut` -> `tun`, `gieng` -> `gehen` |
| Separable verb | The finite verb gets particle + verb; the particle keeps itself as lemma, tag `PTKVZ` | `stand ... auf` -> `aufstehen` / `auf`; `kehrte ... zurück` -> `zurückkehren` / `zurück`; joined `hinausgegangen` -> `hinausgehen` |
| Not a particle | A preposition or `zu` with an object or an infinitive is never `PTKVZ` | `aus der Synagoge` -> `aus` `APPR`; `zu ihm` -> `zu` `APPR` |
| Periphrastic tense, passive | Each word its own lemma | `habe ... getauft` -> `haben` / `taufen`; `wurde getötet` -> `werden` / `töten` |
| Modal, hortative | Own lemma | `laßt uns gehen` -> `lassen` / `wir` / `gehen` |
| Reflexive | Verb without `sich`; the pronoun is `sich` (3rd person) or the personal pronoun | `fürchtete sich` -> `fürchten` / `sich`; `freue mich` -> `freuen` / `ich` |
| `zu` before an infinitive | `zu`, tag `PTKZU` | `zu essen` -> `zu` / `essen` |
| Noun | Nominative singular, capitalised; plural-only nouns as Duden lists them | `Söhnen` -> `Sohn`, `Thür` -> `Tür`, `Leute` -> `Leute` |
| Nominalised verb or adjective | Capitalised noun headword | `das Gute` -> `Gute`, `das Leben` -> `Leben` |
| Adjective | Positive, uninflected | `Heiligem` -> `heilig`, `größer` -> `groß` |
| Participle as adjective | `ADJA`/`ADJD`: the adjective headword if Duden has one, else the verb | `geliebter Sohn` -> `geliebt` |
| Article | `der` / `ein` for every form | `das`, `dem` -> `der`; `einem` -> `ein` |
| Personal pronoun | Nominative of the same person | `ihm` -> `er`, `uns` -> `wir`, `mir` -> `ich`, `euch` -> `ihr` |
| Possessive | Masculine nominative | `ihren` -> `ihr`, `seines` -> `sein` (`PPOSAT`, not the verb `sein`) |
| Contraction | The preposition | `zum` -> `zu`, `im` -> `in`, `vom` -> `von`, `am` -> `an` |
| Name | Nominative, no genitive `s` | `Simons` -> `Simon` (`NE`) |
| Number | Cardinal in modern spelling; ordinals by Duden headword | `zwölf`, `dritte` |
| Compound | Lemma of the whole compound; its parts get a part lemma | `Gerstenernte` -> `Gerstenernte`, parts `Gerste`, `Ernte` |

Homographs (decide from the sentence; tag and lemma change together): `sein` (verb `VAFIN` or possessive `PPOSAT`), `Weg` / `weg`, `Arm` / `arm`, `da` (adverb or conjunction), `als`, `daß` / `das`, `ward` / `wart`, `Gott` / `Götter`.

Form: verbs lowercase infinitive; nouns, names and nominalised words capitalised; everything else lowercase. No punctuation, no spaces, no inflection endings. Old spellings are never lemmas (`thun`, `Thür`, `seyn`, `gieng`, `ward`). A `PTKVZ` word has its own text (lowercase) as lemma, and a finite verb of the same verse has a lemma that starts with it. Do not trust the tag `PTKVZ`: many words tagged so are prepositions or adverbs.

Tags: `NE` is for names only (`Sohnes` is `NN`), participles are `VVPP`, adjectives before a noun `ADJA`. When you change the tag give `[german, lemma, stts, upos]`, with `upos` one of `ADJ ADP ADV AUX CCONJ DET INTJ NOUN NUM PART PRON PROPN SCONJ VERB X`, as a pair that fits (`APPR` + `ADP`, `APPRART` + `ADP`, `NN` + `NOUN`, `NE` + `PROPN`, `VVPP` + `VERB`, `ADJA` + `ADJ`, `ADV` + `ADV`, `PTKVZ` + `ADP`, `PPER` + `PRON`, `KOUS` + `SCONJ`, `KON` + `CCONJ`). Never change a `fixed-lemma`.

# Link roles (shared block, roadmap 0018)

A **source morpheme** is one source word plus one Strong's number in it. Greek: the word (one Strong's). Hebrew: the word plus one morpheme of its morpheme list, e.g. וַיֹּאמֶר = `H9001` (`und`) + `H0559` (`sprechen`); the main morpheme is the one in braces. Punctuation tags (`H9014`-`H9018`) are never morphemes.

| Role | Meaning | Counted | Examples |
|---|---|---|---|
| `head` | Carries the meaning of the morpheme | yes | `getötet` -> ἀπέκτειναν, `sprach` -> `H0559`, `und` -> `H9001`, `ihren` -> `H9024` (suffix "her") |
| `unit` | Belongs to the head's German form and adds no own meaning of the source | no | `wurde` in `wurde getötet`, `habe` in `habe getauft`, `wird` in `wird taufen`, the particle `hinaus` in `ging ... hinaus`, `sich`, `zu` before an infinitive |

Not a link at all: a subject pronoun that only the verb ending supplies (`er` in `und er sprach`, `sie` in `gingen sie`) and articles or words German grammar needs. They are added words with the verb or noun as parent. A pronoun that the source writes as its own word (Greek `αὐτός`, `ἐγώ`, Hebrew `הֵמָּה`) stays linked as `head`.

Rules for roles:

1. Every source morpheme with links has **exactly one** `head`. Several heads only for an idiom where two German words really share one meaning (`zur Antwort geben`); answer them with role `multi-head` (rare, a person checks it).
2. A `unit` link always has a `head` link on the same source morpheme. A contraction (`zum`, `im`, `beim`) is the head of the preposition morpheme.
3. A German word is the `head` of at most one morpheme of one source word.
4. Hebrew: every link names the morpheme it renders. A prefix (`H9001` and, `H9002` and, `H9003` in, `H9005` to, `H9006` from, `H9009` the) or suffix (`H9024` her, `H9034` her ...) is its own morpheme, so `und` and the verb of וַיֹּאמֶר are both heads, of different morphemes.

# How to work

1. Read the verse in the source language, then the German, and decide per German word what it translates.
2. Check every hint against the source; keep the good ones by staying silent, replace the wrong ones.
3. Cover every open German word.
4. Give every link its role (and for Hebrew its morpheme); check that every source morpheme has exactly one `head`.
5. Fix lemmas and tags where they are wrong.
6. Reply with the JSON object only.

When the server sends back rejected verses ("The server rejected the following ..."), answer again with only those verses, same JSON shape and same ids, and fix exactly the listed problems.

# Worked examples

The examples below were built from the real package format and the current data (after the 0018 role backfill). **[UNCONFIRMED] They are proposals: the maintainer has not confirmed these mappings, roles and lemmas yet.** Package ids and local ids are those of a one-verse package.

## Example 1: 1Mo 1:1 [UNCONFIRMED]

Package:

```json
{"packageId":"p_example1","lang":"hebrew","verses":[{"v":"v1","ref":"1Mo 1:1","de":[["g1","Im","in","APPRART"],["g2","Anfang","Anfang","NN"],["g3","schuf","schaffen","VVFIN"],["g4","Gott","Gott","NN"],["g5","die","der","ART"],["g6","Himmel","Himmel","NN"],["g7","und","und","KON"],["g8","die","der","ART"],["g9","Erde","Erde","NN"]],"src":[["s1","בְּרֵאשִׁ֖ית","רֵאשִׁית","H7225G","in/ beginning","HR/Ncfsa",["H9003","{H7225G}"]],["s2","בָּרָ֣א","בָּרָא","H1254A","he created","HVqp3ms",["{H1254A}"]],["s3","אֱלֹהִ֑ים","אֱלֹהִים","H0430G","God","HNcmpa",["{H0430G}"]],["s4","אֵ֥ת","אֵת","H0853_A","<obj.>","HTo",["{H0853}"]],["s5","הַשָּׁמַ֖יִם","שָׁמַיִם","H8064","the/ heavens","HTd/Ncmpa",["H9009","{H8064}"]],["s6","וְאֵ֥ת","אֵת","H0853_B","and/ <obj.>","HC/To",["H9002","{H0853}"]],["s7","הָאָֽרֶץ׃","אֶ֫רֶץ","H0776G","the/ earth","HTd/Ncfsa",["H9009","{H0776G}"]]],"fixed":[],"hints":[["g1","s1","head","H9003"],["g2","s1","head","H7225G"],["g3","s2","head","H1254A"],["g4","s3","head","H0430G"],["g5","s5","head","H9009"],["g6","s5","head","H8064"],["g7","s6","head","H9002"],["g8","s7","head","H9009"],["g9","s7","head","H0776G"]]}]}
```

Answer:

```json
{"packageId":"p_example1","verses":[{"v":"v1","links":[],"added":[],"lemmas":[],"parts":[],"unmapped":[]}]}
```

What to notice: Every hint is right: `Im` is the head of the prefix `H9003` and `Anfang` of the main morpheme; `die` renders the article `H9009` inside s5 and s7. The object marker s4 stays without a link.

## Example 2: 1Mo 1:3 [UNCONFIRMED]

Package:

```json
{"packageId":"p_example2","lang":"hebrew","verses":[{"v":"v1","ref":"1Mo 1:3","de":[["g1","Und","und","KON"],["g2","Gott","Gott","NN"],["g3","sprach","ansprechen","VVFIN","hint-lemma"],["g4","Es","es","PPER"],["g5","werde","werden","VAFIN",null,["Werde"]],["g6","Licht","Licht","NN"],["g7","und","und","KON"],["g8","es","es","PPER"],["g9","ward","werden","VAFIN",null,["Ward"]],["g10","Licht","Licht","NN"]],"src":[["s1","וַיֹּ֥אמֶר","אָמַר","H0559","and/ he said","Hc/Vqw3ms",["H9001","{H0559}"]],["s2","אֱלֹהִ֖ים","אֱלֹהִים","H0430G","God","HNcmpa",["{H0430G}"]],["s3","יְהִ֣י","הָיָה","H1961_A","let it be","HVqj3ms",["{H1961}"]],["s4","א֑וֹר","אוֹר","H0216_A","light","HNcfsa",["{H0216}"]],["s5","וַֽיְהִי־","הָיָה","H1961_B","and/ there was","Hc/Vqw3ms",["H9001","{H1961}"]],["s6","אֽוֹר׃","אוֹר","H0216_B","light","HNcfsa",["{H0216}"]]],"fixed":[],"hints":[["g1","s1","head","H9001"],["g2","s2","head","H0430G"],["g3","s1","head","H0559"],["g4","g5"],["g5","s3","head","H1961"],["g6","s4","head","H0216"],["g7","s5","head","H9001"],["g8","g9"],["g9","s5","head","H1961"],["g10","s6","head","H0216"]]}]}
```

Answer:

```json
{"packageId":"p_example2","verses":[{"v":"v1","links":[],"added":[],"lemmas":[["g3","sprechen"]],"parts":[],"unmapped":[]}]}
```

What to notice: `Und` renders the prefix `H9001`, `sprach` the main morpheme `H0559` of the one word וַיֹּאמֶר. `Es` (`Es werde`, `es ward`) is supplied by the verb form: an added word. The lemma `ansprechen` is wrong: there is no particle.

## Example 3: Rt 1:4 [UNCONFIRMED]

Package:

```json
{"packageId":"p_example3","lang":"hebrew","verses":[{"v":"v1","ref":"Rt 1:4","de":[["g1","Und","und","KON"],["g2","sie","sie","PPER"],["g3","nahmen","aufnehmen","VVFIN",null,["wegnehmen","zunehmen"]],["g4","sich","sich","PRF"],["g5","moabitische","Moabitische","NN"],["g6","Weiber","Weiber","NN"],["g7","der","der","ART"],["g8","Name","Name","NN"],["g9","der","der","ART"],["g10","einen","ein","ART"],["g11","war","sein","VAFIN"],["g12","Orpa","Orpa","NE"],["g13","und","und","KON"],["g14","der","der","ART"],["g15","Name","Name","NN"],["g16","der","der","ART"],["g17","anderen","anderer","PIS"],["g18","Ruth","Ruth","NE"],["g19","und","und","KON"],["g20","sie","sie","PPER"],["g21","wohnten","wohnen","VVFIN"],["g22","daselbst","daselbst","PROAV"],["g23","bei","bei","APPR"],["g24","zehn","zehn","CARD"],["g25","Jahren","Jahr","NN"]],"src":[["s1","וַיִּשְׂא֣וּ","נָשָׂא","H5375U","and/ they took","Hc/Vqw3mp",["H9001","{H5375U}"]],["s2","לָהֶ֗ם","הֶם","H9038","for <the>/ themselves","HRd/Sp3mp",["H9005","{H9038}"]],["s3","נָשִׁים֙","אִשָּׁה","H0802H","wives","HNcmpa",["{H0802H}"]],["s4","מֹֽאֲבִיּ֔וֹת","מוֹאָבִי","H4125","Moabite","HNgfpa",["{H4125}"]],["s5","שֵׁ֤ם","שֵׁם","H8034_A","[the] name of","HNcmsc",["{H8034}"]],["s6","הָֽאַחַת֙","אֶחָד","H0259","the/ one","HTd/Ncfsa",["H9009","{H0259}"]],["s7","עָרְפָּ֔ה","עׇרְפָּה","H6204","[was] Orpah","HNpf",["{H6204}"]],["s8","וְשֵׁ֥ם","שֵׁם","H8034_B","and/ [the] name of","HC/Ncmsc",["H9002","{H8034}"]],["s9","הַשֵּׁנִ֖ית","שֵׁנִי","H8145","the/ second","HTd/Aofsa",["H9009","{H8145}"]],["s10","ר֑וּת","רוּת","H7327","[was] Ruth","HNpf",["{H7327}"]],["s11","וַיֵּ֥שְׁבוּ","יָשַׁב","H3427","and/ they dwelt","Hc/Vqw3mp",["H9001","{H3427}"]],["s12","שָׁ֖ם","שָׁם","H8033G","there","HD",["{H8033G}"]],["s13","כְּעֶ֥שֶׂר","עֶשֶׂר","H6235","about/ ten","HR/Ncmsc",["H9004","{H6235}"]],["s14","שָׁנִֽים׃","שָׁנָה","H8141","years","HNcmpa",["{H8141}"]]],"fixed":[],"hints":[["g1","s1","head","H9001"],["g2","g3"],["g3","s1","head","H5375U"],["g4","s2","head","H9038"],["g5","s4","head","H4125"],["g6","s3","head","H0802H"],["g8","s5","head","H8034"],["g10","s6","head","H9009"],["g11","s6","head","H0259"],["g12","s7","head","H6204"],["g13","s8","head","H9002"],["g15","s8","head","H8034"],["g17","s9","head","H8145"],["g18","s10","head","H7327"],["g19","s11","head","H9001"],["g20","g21"],["g21","s11","head","H3427"],["g22","s12","head","H8033G"],["g23","s13","head","H6235"],["g24","s13","head","H6235"],["g25","s14","head","H8141"]]}]}
```

Answer:

```json
{"packageId":"p_example3","verses":[{"v":"v1","links":[["g9","s6","head","H9009"],["g10","s6","head","H0259"],["g16","s9","head","H9009"],["g23","s13","head","H9004"]],"added":[["g7","g8"],["g11","g12"],["g14","g15"]],"lemmas":[["g3","nehmen"],["g5","moabitisch","ADJA","ADJ"],["g6","Weib"]],"parts":[],"unmapped":[]}]}
```

What to notice: הָאַחַת is article + "one": `der` and `einen` are heads of the two morphemes. The articles before `Name` have no Hebrew word: added words. `war` (nominal clause) is an added word of `Orpa`. `bei` renders the prefix `H9004`, `zehn` the main morpheme.

## Example 4: Rt 1:5 [UNCONFIRMED]

Package:

```json
{"packageId":"p_example4","lang":"hebrew","verses":[{"v":"v1","ref":"Rt 1:5","de":[["g1","Da","da","KOUS"],["g2","starben","sterben","VVFIN"],["g3","auch","auch","ADV"],["g4","die","der","ART"],["g5","beiden","beide","PIAT"],["g6","Machlon","Machlon","NE"],["g7","und","und","KON"],["g8","Kiljon","Kiljon","NE"],["g9","und","und","KON"],["g10","das","der","ART"],["g11","Weib","Weib","NN"],["g12","blieb","übrigbleiben","VVFIN","fixed-lemma"],["g13","allein","allein","ADV"],["g14","übrig","übrig","PTKVZ"],["g15","von","von","APPR"],["g16","ihren","ihr","PPOSAT"],["g17","beiden","beide","PIAT"],["g18","Söhnen","Söhn","NN"],["g19","und","und","KON"],["g20","von","von","APPR"],["g21","ihrem","ihr","PPOSAT"],["g22","Manne","Manne","NN"]],"src":[["s1","וַיָּמ֥וּתוּ","מוּת","H4191","and/ they died","Hc/Vqw3mp",["H9001","{H4191}"]],["s2","גַם־","גַּם","H1571","also","HD",["{H1571}"]],["s3","שְׁנֵיהֶ֖ם","שְׁנַ֫יִם","H8147_A","both of/ them","HNcbdc/Sp3mp",["{H8147}","H9028"]],["s4","מַחְל֣וֹן","מַחְלוֹן","H4248","Mahlon","HNpm",["{H4248}"]],["s5","וְכִלְי֑וֹן","כִּלְיוֹן","H3630","and/ Kilion","HC/Npm",["H9002","{H3630}"]],["s6","וַתִּשָּׁאֵר֙","שָׁאַר","H7604","and/ she was left","Hc/VNw3fs",["H9001","{H7604}"]],["s7","הָֽאִשָּׁ֔ה","אִשָּׁה","H0802G","the/ woman","HTd/Ncfsa",["H9009","{H0802G}"]],["s8","מִשְּׁנֵ֥י","שְׁנַ֫יִם","H8147_B","from/ both","HR/Ncbdc",["H9006","{H8147}"]],["s9","יְלָדֶ֖יהָ","יֶ֫לֶד","H3206","children/ her","HNcmpc/Sp3fs",["{H3206}","H9024"]],["s10","וּמֵאִישָֽׁהּ׃","אִישׁ","H0376H","and/ from/ husband/ her","HC/R/Ncmsc/Sp3fs",["H9002","H9006","{H0376H}","H9024"]]],"fixed":[],"hints":[["g1","s1","head","H4191"],["g2","s1","head","H4191"],["g3","s2","head","H1571"],["g5","s3","head","H8147"],["g6","s4","head","H4248"],["g7","s5","head","H9002"],["g8","s5","head","H3630"],["g9","s6","head","H9001"],["g11","s7","head","H0802G"],["g12","s6","head","H7604"],["g13","s6","head","H7604"],["g14","s6","unit","H7604"],["g15","s8","head","H9006"],["g16","s9","head","H9024"],["g17","s8","head","H8147"],["g18","s9","head","H3206"],["g19","s10","head","H9002"],["g20","s10","head","H9006"],["g21","s10","head","H9024"],["g22","s10","head","H0376H"]]}]}
```

Answer:

```json
{"packageId":"p_example4","verses":[{"v":"v1","links":[["g1","s1","head","H9001"],["g10","s7","head","H9009"]],"added":[["g4","g5"],["g13","g12"]],"lemmas":[["g1","da","ADV","ADV"],["g18","Sohn"],["g22","Mann"]],"parts":[],"unmapped":[]}]}
```

What to notice: `Da` renders the `waw` prefix `H9001`, `starben` the main morpheme. `blieb ... übrig` is a separable verb: `übrigbleiben` on the verb, `übrig` keeps itself and is a `unit` (hint kept). `allein` has no Hebrew word: an added word. `ihren` is the head of the suffix `H9024`.

## Example 5: Rt 1:22 [UNCONFIRMED]

Package:

```json
{"packageId":"p_example5","lang":"hebrew","verses":[{"v":"v1","ref":"Rt 1:22","de":[["g1","Und","und","KON"],["g2","so","so","ADV"],["g3","kehrte","zurückkehren","VVFIN","fixed-lemma"],["g4","Noomi","Noomi","NE"],["g5","zurück","zurück","PTKVZ"],["g6","und","und","KON"],["g7","Ruth","Ruth","NE"],["g8","die","der","ART"],["g9","Moabitin","Moabitin","NN"],["g10","ihre","ihr","PPOSAT"],["g11","Schwiegertochter","Schwiegertochter","NN"],["g12","mit","mit","APPR"],["g13","ihr","ihr","PPOSAT"],["g14","welche","welcher","PWAT"],["g15","aus","aus","PTKVZ"],["g16","den","der","ART"],["g17","Gefilden","Gefilde","NN"],["g18","Moabs","Moabs","NE"],["g19","zurückkehrte","zurückkehren","VVFIN"],["g20","und","und","KON"],["g21","sie","sie","PPER"],["g22","kamen","herzukommen","VVFIN",null,["hervorkommen","heraufkommen"]],["g23","nach","nach","APPR"],["g24","Bethlehem","Bethlehem","NE"],["g25","beim","bei","APPRART"],["g26","Beginn","Beginn","NN"],["g27","der","der","ART"],["g28","Gerstenernte","Gerstenernt","NN"]],"src":[["s1","וַתָּ֣שָׁב","שׁוּב","H7725G_A","and/ she returned","Hc/Vqw3fs",["H9001","{H7725G}"]],["s2","נָעֳמִ֗י","נׇעֳמִי","H5281","Naomi","HNpf",["{H5281}"]],["s3","וְר֨וּת","רוּת","H7327","and/ Ruth","HC/Npf",["H9002","{H7327}"]],["s4","הַמּוֹאֲבִיָּ֤ה","מוֹאָבִי","H4125","the/ Moabite [woman]","HTd/Ngfsa",["H9009","{H4125}"]],["s5","כַלָּתָהּ֙","כַּלָּה","H3618H","daughter-in-law/ her","HNcfsc/Sp3fs",["{H3618H}","H9024"]],["s6","עִמָּ֔הּ","עִם","H5973A","[was] with/ her","HRd/Sp3fs",["{H5973A}","H9034"]],["s7","הַשָּׁ֖בָה","שׁוּב","H7725G_B","who/ returned","HTd/Vqp3fs",["H9009","{H7725G}"]],["s8","מִשְּׂדֵ֣י","שָׂדֶה","H7704G","from/ [the] region<s> of","HR/Ncmpc",["H9006","{H7704G}"]],["s9","מוֹאָ֑ב","מוֹאָב","H4124G","Moab","HNpl",["{H4124G}"]],["s10","וְהֵ֗מָּה","הֵ֫מָּה","H1992","and/ they","HC/Pp3mp",["H9002","{H1992}"]],["s11","בָּ֚אוּ","בּוֹא","H0935G","they came","HVqp3cp",["{H0935G}"]],["s12","בֵּ֣ית","בֵּית לֶ֫חֶם","H1035G_A","Beth-","HNpl",["{H1035G}"]],["s13","לֶ֔חֶם","בֵּית לֶ֫חֶם","H1035G_b","-lehem","HNpl",["{H1035G}"]],["s14","בִּתְחִלַּ֖ת","תְּחִלָּה","H8462","at/ [the] beginning of","HR/Ncfsc",["H9003","{H8462}"]],["s15","קְצִ֥יר","קָצִיר","H7105A","[the] harvest of","HNcmsc",["{H7105A}"]],["s16","שְׂעֹרִֽים׃","שְׂעֹרָה","H8184","barley<s>","HNcmpa",["{H8184}"]]],"fixed":[],"hints":[["g1","s1","head","H9001"],["g2","s7","head","H7725G"],["g3","s1","head","H7725G"],["g4","s2","head","H5281"],["g5","s1","unit","H7725G"],["g6","s3","head","H9002"],["g7","s3","head","H7327"],["g8","s4","head","H9009"],["g9","s4","head","H4125"],["g10","s5","head","H9024"],["g11","s5","head","H3618H"],["g12","s6","head","H5973A"],["g13","s6","head","H9034"],["g14","s7","head","H7725G"],["g15","s8","head","H9006"],["g16","s8","head","H7704G"],["g17","s8","head","H7704G"],["g18","s9","head","H4124G"],["g19","s7","head","H7725G"],["g20","s10","head","H9002"],["g21","s10","head","H1992"],["g22","s11","head","H0935G"],["g23","s12","head","H1035G"],["g24","s13","head","H1035G"],["g25","s14","head","H9003"],["g26","s14","head","H8462"],["g27","s15","head","H7105A"],["g28","s16","head","H8184"]]}]}
```

Answer:

```json
{"packageId":"p_example5","verses":[{"v":"v1","links":[["g14","s7","head","H9009"],["g24","s12","head"],["g24","s13","head"]],"added":[["g2","g3"],["g16","g17"],["g23","g24"],["g27","g28"]],"lemmas":[["g13","sie","PPER","PRON"],["g15","aus","APPR","ADP"],["g18","Moab"],["g22","kommen"],["g28","Gerstenernte"]],"parts":[["g28","s16",1,"Gersten","Gerste"],["g28","s15",2,"ernte","Ernte"]],"unmapped":[]}]}
```

What to notice: Compound `Gerstenernte` = s16 (barley) + s15 (harvest): parts with part lemmas, no plain link. `kehrte ... zurück`: `zurückkehren` / `zurück` (`unit`). `welche` renders the article used as relative (`H9009`). `sie` stays linked: Hebrew writes הֵמָּה.
