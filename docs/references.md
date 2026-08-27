# Resolving a reference the other server sent

Most fields carry a value. A few carry the name of something the server holds
separately, and writing one of those means finding that thing here or making it.
A film arriving from the peer with three actors and a studio this server has
never recorded is the ordinary case, not the awkward one.

The field register names which fields are like this: genres, studios and people.
Tags and production locations are not, and that is a decision rather than an
omission - the server builds no entity from either, so writing one creates
nothing here and there is nothing to resolve.

This document is a rendering of
`Jellyfin.Plugin.MetadataSync/References/reference-comparison.json`, which ships
inside the assembly and is the only place a rule is declared. The suite holds
the two against each other, so neither is quietly right when they disagree. The
prose between the tables is written by hand and nothing derives it, which is the
bound on that check: a paragraph here that contradicts a table below it is
caught by a reader and by nothing else.

## What the comparison is for

The prior art matches people by name across servers, read at
https://github.com/JPKribs/jellyfin-plugin-serversync on 2026-08-05. That is the
join which produces two records for one person when one server spells the name
with an accent and the other does not, and it is the failure this table exists
to answer rather than repeat.

The answer is not the opposite failure. Folding the accent silently would
attribute a film to whichever spelling this server happens to hold and discard
the peer's spelling of somebody's name, and nobody would ever see that it
happened.

## One row per kind and per difference

Two spellings of one reference can differ in four ways, and the table answers
all four for every kind. There is no default: a pair with no row is a table that
does not load, because whichever default was chosen would decide whether two
spellings of a person are one person with nothing written down to disagree with.

The answer set is closed at two. There is no answer meaning "different", because
a difference the table does not fold already makes two values two values.

<!-- rendered from reference-comparison.json: rules -->
| Reference | A difference in | Answer | Reason |
| --- | --- | --- | --- |
| `Genre` | `Whitespace` | `Same` | A leading space, a trailing space and a doubled space inside a genre are artefacts of whichever hand or provider typed the entry. No server displays them and no operator chose them, so two spellings that differ only there are one genre. |
| `Genre` | `Case` | `Same` | A genre list is a short controlled vocabulary an operator browses by. A library holding both 'Sci-Fi' and 'sci-fi' shows one shelf twice, which is a defect rather than a distinction, so capitalisation does not separate two genres. |
| `Genre` | `Accents` | `Undecided` | Folding the accent picks which of two spellings the genre list keeps, and the accented one is usually the correct one while the unaccented one is usually the one already here. Creating a second entry instead grows the list silently. Neither is this plugin's choice to make on an operator's shelf, so the pair is reported and left alone. |
| `Genre` | `Punctuation` | `Undecided` | 'Sci-Fi', 'Sci Fi' and 'SciFi' are one genre in most vocabularies and three entries in a library that received all three. Which spelling survives is a decision about how a shelf is labelled, and this plugin reports the pair rather than settling it. |
| `Studio` | `Whitespace` | `Same` | Same artefact as a genre and the same answer. A doubled space inside a studio name is a typing slip on one of the two servers, never a second company. |
| `Studio` | `Case` | `Same` | A studio written in capitals on one server is the same company as the one written in title case on the other. Capitalisation of a company name is a house style of whichever provider wrote it. |
| `Studio` | `Accents` | `Undecided` | A studio name outside English carries accents that the provider on the other server may have dropped. Joining the two picks a spelling for somebody else's company and discards the peer's, and creating a second entry puts the same company on the list twice. The pair is reported. |
| `Studio` | `Punctuation` | `Undecided` | 'Warner Bros.' and 'Warner Bros' are one company, and a punctuation difference elsewhere in the same list can separate two real ones. Nothing in a name says which case this is, so the pair is reported rather than guessed at in either direction. |
| `Person` | `Whitespace` | `Same` | A trailing space on a cast entry comes from a form somebody typed into. It is not part of anybody's name. |
| `Person` | `Case` | `Same` | A cast list imported in capitals is the same person as the one in title case. Case has never been what separates two people, and a library holding one name twice in two capitalisations holds one person twice. |
| `Person` | `Accents` | `Undecided` | This is the row that decides whether one actor becomes one record or two on every library this runs against. Folding the accent attributes a film to the local unaccented record and drops the peer's spelling of a person's name, which nobody sees afterwards. Not folding it puts one human on the list twice. The first is silent and the second is visible, and this plugin does neither: the pair is reported and an operator decides which spelling their library keeps. |
| `Person` | `Punctuation` | `Undecided` | 'Robert Downey Jr.' and 'Robert Downey Jr' are one person, and a hyphen or an apostrophe elsewhere in a name is load-bearing. A rule that folded punctuation for people would be right on the suffix and wrong on the surname, so the pair is reported instead. |
<!-- end rendered -->

The three kinds answer alike on all four rows today. The table is still per kind
rather than one row per difference, because the reasons are three arguments that
agree rather than one argument applied three times, and changing a kind's answer
should be an edit to that kind's row instead of a special case inside a
comparison.

## What a resolution answers

Four outcomes, and every one of them carries a sentence saying what happened.
There is no outcome meaning the reference was passed over: an operator reading a
sync that reported success beside a cast list missing two names is the failure
this whole resolution exists against.

`Resolved` means exactly one entry here is the same reference, and the outcome
names it as this server spells it.

`Create` means nothing here is the same and nothing here is close, so the
reference would be created. Would, not is - see the bounds at the end.

`Undecided` means either something here is close in a way the table refuses to
decide, or more than one entry here is the same reference. Both carry the
entries that caused them.

`Refused` means the incoming value is empty or is nothing but space, so there is
no reference to resolve at all.

## How two values are compared

Both sides are put into one composed Unicode form first. That is not one of the
four rules: two encodings of one character are one character, and folding them
is not a decision about spelling.

Then the properties the row answers `Same` are folded away, and what is left is
compared exactly. If that matches, the reference resolves.

If it does not, the properties answered `Undecided` are folded away as well. If
the values match only then, they differ by something nobody here may decide, and
the pair is reported.

The punctuation fold replaces a punctuation mark with a space and collapses what
is left, because a hyphen between two words stands in for one. Removing it
instead would leave `Sci-Fi` and `Sci Fi` differing by the space that the
punctuation rule is the reason for.

An exact match settles the outcome even when something else here is also close.
The near miss in that case is this server's own duplicate, and one incoming
reference is not where it gets discovered.

## The fixture table

Every row here is a test. The expected outcome is read out of this document by
the suite rather than restated in code, so a row changed here and nowhere else
turns the suite red.

The last column names which of the four differences the case is about, and the
suite holds that against the table: a case about a difference the table answers
`Same` has to expect `Resolved`, and one about a difference answered `Undecided`
has to expect `Undecided`. A rule with no case at all is refused as well, so a
table that grew a row without a fixture does not pass quietly.

Entries already here are separated by `;`, and an empty cell means this server
holds none. Values are written inside backticks so a case about a space is
readable.

| Case | Reference | Incoming | Already here | Outcome | The difference it is about |
| --- | --- | --- | --- | --- | --- |
| a genre spelled exactly as this server spells it | `Genre` | `Comedy` | `Comedy;Drama` | `Resolved` | `-` |
| a genre differing only in case, which is one shelf shown twice | `Genre` | `sci-fi` | `Sci-Fi` | `Resolved` | `Case` |
| a genre differing only in a doubled space nobody typed on purpose | `Genre` | `Film  Noir` | `Film Noir` | `Resolved` | `Whitespace` |
| a genre differing only in an accent, where one list keeps the mark and the other dropped it | `Genre` | `Komodie` | `Komödie` | `Undecided` | `Accents` |
| a genre differing only in the separator between two words | `Genre` | `Sci Fi` | `Sci-Fi` | `Undecided` | `Punctuation` |
| a genre nothing here resembles | `Genre` | `Documentary` | `Comedy;Drama` | `Create` | `-` |
| a genre this server already holds twice under one spelling rule | `Genre` | `Sci-Fi` | `sci-fi;SCI-FI` | `Undecided` | `-` |
| an incoming genre that is nothing but space | `Genre` | `   ` | `Comedy` | `Refused` | `-` |
| a studio spelled exactly as this server spells it | `Studio` | `A24` | `A24;Blumhouse` | `Resolved` | `-` |
| a studio differing only in case | `Studio` | `a24` | `A24` | `Resolved` | `Case` |
| a studio differing only in a trailing space from the form it was typed into | `Studio` | `Blumhouse ` | `Blumhouse` | `Resolved` | `Whitespace` |
| a studio differing only in an accent the other server's provider dropped | `Studio` | `Gaumont Francaise` | `Gaumont Française` | `Undecided` | `Accents` |
| a studio differing only in the full stop after an abbreviation | `Studio` | `Warner Bros.` | `Warner Bros` | `Undecided` | `Punctuation` |
| a studio arriving at a server that holds none | `Studio` | `A24` | | `Create` | `-` |
| a person spelled exactly as this server spells them | `Person` | `Greta Gerwig` | `Greta Gerwig` | `Resolved` | `-` |
| a cast list imported in capitals | `Person` | `GRETA GERWIG` | `Greta Gerwig` | `Resolved` | `Case` |
| a name with a doubled space between its two parts | `Person` | `Greta  Gerwig` | `Greta Gerwig` | `Resolved` | `Whitespace` |
| a name differing only in its accents, which is the case the prior art turns into two records | `Person` | `Zoe Saldana` | `Zoë Saldaña` | `Undecided` | `Accents` |
| a name differing only in the full stop after a suffix | `Person` | `Robert Downey Jr.` | `Robert Downey Jr` | `Undecided` | `Punctuation` |
| a person nobody here has heard of | `Person` | `Ana de Armas` | `Greta Gerwig` | `Create` | `-` |

## What this does not do

It creates nothing. `Create` says what would be created and stops there. Where
that mark would be kept is no longer the missing half: #47 landed the record of
what this plugin wrote, and #61 gave every store this plugin owns one shape, so
a store holding those marks is three members and a registration rather than a
design nobody has taken.

What is missing is at the other end. Nothing in this plugin asks this resolver
anything, so no reference is ever created and there is no mark to keep. The only
source that names it is its own:

<!-- the plugin sources that name the reference resolver: one per line, the file first, read by ReferenceCreationTests -->

- `Jellyfin.Plugin.MetadataSync/References/ReferenceResolver.cs`, where the resolver is declared

<!-- end of the sources that name the resolver -->

That list is held against the plugin's own sources in both directions, so a
second source naming the resolver reddens this paragraph rather than leaving it
to go quietly stale, which is what the sentence it replaces did between the
store landing and somebody reading this file. The caller belongs inside a pass,
which is #36, #38 and #40, and the field register's rows for genres, studios and
people declare that they do not move until the mark is produced.

It records nothing. An outcome is returned to whatever asked for it and written
to no register, because the log a non-resolution belongs in is #48 and it is not
built. Until then the third condition of #15 is unmet, and this document says so
rather than letting a returned value read as a recorded one.

It reads nothing but the two arguments. No library, no clock, no file, no
transport, so every row above is decidable with nothing running.

It does not decide which item a payload is about. That is provider identifiers,
in `docs/provider-identifiers.md`, and it is a different question with a
different table.
