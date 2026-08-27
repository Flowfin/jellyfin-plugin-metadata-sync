# How two items are decided to be the same

One refusal decides the shape of everything else here, so it is first.

No path, no filename, no directory name, no file size and no hash of a file ever
takes part in deciding that two items are the same. If provider identifiers
cannot answer, the answer is that the item does not resolve.

`provider-identifiers.md` is how identifiers are compared once there are some.
This document is about where identity may come from at all, and about the case
where nothing answers.

## Why this is a refusal and not a fallback

Every published attempt at this problem derives item identity from the file
system, and each one breaks in the same place.

One server-sync plugin matches content by file path and documents that only
single-folder libraries are supported (read at
https://github.com/JPKribs/jellyfin-plugin-serversync on 2026-08-05). Another
requires media identifiers to be exactly identical on all instances, which,
because the server derives them from the path and the filename, means the full
directory structure and the naming have to match on both servers (read at
https://github.com/GermanCoding/jellyfin-server-sync on 2026-08-05). An external
tool matches on provider identifiers and filenames and says plainly that this is
not perfect but works for most cases (read at
https://github.com/luigi311/JellyPlex-Watched on 2026-08-05). Another added a
path-derived identifier specifically for backends with unreliable external
identifiers (read at https://github.com/arabcoders/watchstate on 2026-08-05).

The assumption underneath all four is that two servers hold the same files laid
out the same way. That is true of one server copied twice. It is false of two
households that each built a library, and that is the case this plugin exists
for. File replication being a permanent non-goal is not a limitation here: the
two libraries are not supposed to be the same files.

A path-derived identity is also worse than no answer, because it is confident. A
wrong match writes the overview of one film onto another, and nothing about the
result says a guess was made.

## What it costs

An item with no provider identifiers does not sync at all. Not partially, not by
title, not by year and title together, not by anything.

On a library built without a metadata provider, that can be most of it. The
honest thing to say to an operator in that position is that the fix is to let a
provider identify the items, and never to rename files so two servers agree. The
second is work that has to be redone every time either library grows, and it
makes the two libraries more alike rather than making the plugin more correct.

Two libraries do not need the same files, the same layout or the same names.
That is the difference an operator is choosing this plugin for, and it is bought
by this refusal rather than in spite of it.

## What holds it up

`InvariantLintTests` carries a rule with the identifier
`no-file-system-property-in-item-identity`, seeded from this refusal under #79.
It refuses the tokens a file-system read is spelled with, and it carries a
regression and a near-miss that are both run:

    dotnet test --filter InvariantLintTests

Its reach is wider than the rule as stated, and deliberately so. This paragraph
said the reason was that there is no resolution path in this tree yet. There is
one, and `ResolutionPathTests` walks it: the walk starts at the types this
plugin declares under the namespaces below and asks what a call from them
arrives at, so a filename read three helpers deep is refused where the spelling
alone would not have found it.

<!-- the namespaces the resolution walk starts from: one per line, read by MatchingStatementTests -->
- `Jellyfin.Plugin.MetadataSync.Matching.`
- `Jellyfin.Plugin.MetadataSync.References.`
<!-- end of the namespaces the resolution walk starts from -->

The lint stays wider than that walk rather than being narrowed onto it, because
the two ask different questions. The walk follows a call and stops where it
cannot follow one; the lint matches a spelling anywhere in the plugin's sources,
so a read the walk has no edge to reach still costs a red suite wherever it is
written. Wider is the fail-closed direction. A legitimate file-system read
outside the resolution path costs one entry in that rule's allowed set with the
reason written next to it; one inside it costs a red suite.

What it cannot catch is written at the rule and is repeated here because it is
the part a reader has to weigh. It matches a spelling and never an intent. A
file-system property read somewhere else and handed in as a plain string spells
none of those tokens, and neither does a route through reflection or through an
extension method named after the thing it returns. The register row for `Path`
refuses the other direction, which is writing one.

## Where identity does come from

The item's provider identifiers, compared by the rules in
`provider-identifiers.md`, and nothing else. `ProviderIds` is a row in the field
register that does not move, for the reason written on it: the resolver decides
which local item a payload is about by reading them, so writing them would
change identity underneath a pass that is still running.

## When nothing resolves

Not an error, and not a guess. The item is recorded as unresolved with a reason
per item, which is #29.

That register is what makes this refusal liveable rather than merely correct. An
operator who can see which items did not resolve, and why, can act on it. An
operator who sees a sync that quietly covered two thirds of a library cannot.

## A work more than one item here carries

Comparing identifiers answers whether two items are the same work. It does not
answer which item to write to on a server that holds two of them, and a film
kept in two cuts or two qualities is exactly that: one work, two items, and an
operator who keeps both did nothing wrong.

`CandidateResolver` in the plugin is where that is decided. It adds no
comparison of its own, so every candidate offered to it is answered by the rules
in `provider-identifiers.md`, and what it settles is what happens when those
rules say yes more than once. Nothing is written and both items are named, so an
operator can merge them or say which one this plugin writes to.

It reads the identifiers it is handed and nothing else, so every row below is
decidable with nothing running.

## The fixture table for candidates offered on one server

Every row is a test. The rows are read out of this document by the suite rather
than restated in it, so a row added here is run and a row whose expectation
changes here changes what the suite asserts.

The work is written as its provider identifiers, `Provider=Value`, several
separated by `;`, exactly as `provider-identifiers.md` writes them, and an empty
cell is a work carrying none. A candidate is written as the identity it is
reported back by, then its own identifiers in brackets, and candidates are
separated by a space. `here:extended[Tmdb=550]` is one candidate. An empty cell
is a library that offered none, which is a different thing from a library that
offered candidates none of which is this work.

The last column is what the row is for, on the same terms as the table further
down: it names the implementation the row refuses rather than the behaviour it
wants, because the wanted behaviour is the outcome column and writing it twice
says nothing new.

| Case | The work | Offered here | Outcome | The mistake it would catch |
| --- | --- | --- | --- | --- |
| exactly one candidate is the same work | `Tmdb=550` | `here:1[Tmdb=550] here:2[Tmdb=551]` | `Resolved` | A resolver that answers nothing at all, which leaves every refusing row below green while no item is ever placed. |
| one work held here as two cuts of the same film | `Tmdb=550` | `here:extended[Tmdb=550] here:theatrical[Tmdb=550]` | `HeldByMoreThanOne` | Taking the first candidate the library returned, so the same pass over the same data writes to the other item once a rescan reorders the rows. |
| two items that are one work, beside a third that is not | `Tmdb=550` | `here:extended[Tmdb=550] here:theatrical[Tmdb=550] here:other[Tmdb=551]` | `HeldByMoreThanOne` | Reading the set as settled once one candidate has been told apart from another, when the two that cannot be told apart are still two. |
| a library that offered no candidate at all | `Tmdb=550` | | `NothingOffered` | Reporting a library nobody has scanned with this provider as one holding a different work, which sends an operator looking for a disagreement that is not there. |
| candidates offered and none of them the work | `Tmdb=550` | `here:1[Tmdb=551]` | `NoneIsTheSameWork` | Folding an empty result into a mismatched one, after which nothing separates a provider nobody enabled from two libraries that genuinely disagree. |
| the only candidate offered shares no provider | `Tmdb=550` | `here:1[Imdb=tt0137523]` | `NoneIsTheSameWork` | Taking the only candidate a query returned, which is taking the first with a step in front of it. |
| a work carrying no identifiers of its own | | `here:1[Tmdb=550]` | `NoneIsTheSameWork` | Starting from agreement rather than from nothing, so an item nobody has run a metadata scan over resolves to whatever was offered beside it. |
| two cuts of one work, one writing the provider name in lower case | `Tmdb=550` | `here:extended[tmdb=550] here:theatrical[Tmdb=550]` | `HeldByMoreThanOne` | Looking the provider up by an ordinal dictionary key, which drops one of the two cuts out of the set and leaves the other looking like an answer. |
| two cuts of one work, one of them zero-padded | `Tvdb=75978` | `here:a[Tvdb=0075978] here:b[Tvdb=75978]` | `HeldByMoreThanOne` | Comparing a decimal identifier as text, which hides one duplicate behind its padding and turns an ambiguity into a resolution. |
| two candidates agreeing on one provider where only one agrees on both | `Tmdb=550;Imdb=tt0137523` | `here:a[Tmdb=550;Imdb=tt0137523] here:b[Tmdb=550;Imdb=tt0111161]` | `Resolved` | Answering on the first provider that agrees, which turns the candidate a second provider rules out into a second answer for one work. |

## An item identified by its parent and an ordinal

A film usually carries its own provider identifiers. An episode often does not:
on many setups the identifier dictionary on the episode itself is empty, and
what identifies it is the series it belongs to plus its position inside that
series.

That makes it a two-step resolution, and the steps are named because they fail
differently.

The first step is the parent. The series resolves by its own provider
identifiers, through the comparison in `provider-identifiers.md` that every
other item uses. Nothing under a parent that did not resolve is looked at, and
that is an ordering rather than an optimisation: an ordinal counts within a
series, so `S01E05` on its own names an episode in every series at once.

The second step is the ordinal, decided inside the resolved parent. It compares
a season and a number against the items under that parent, and it answers only
where exactly one of them carries both.

The rule is written for the shape and not for episodes. A season inside a series
is the same shape, and so is anything else whose identity is its parent plus a
count. Writing it once for the shape is what keeps the answer the same when the
next such kind arrives.

`OrdinalResolver` in the plugin is where this lives. It reads two identities and
nothing else, so every case below is decidable with nothing running.

## Nothing resolves by proximity

There is no nearest, no next, no only remaining candidate and no range that
contains a number. Each of those is a fallback some published matcher takes, and
each of them writes one episode's metadata onto another while reporting a
successful sync.

The one that is easiest to write by accident is the only remaining candidate. A
series has resolved, one item is left unmatched on each side, and taking the pair
looks like arithmetic rather than like a guess. It is a guess, and the two
libraries disagreeing about how many episodes a season has is the ordinary case
rather than the strange one.

## What each answer means

One row per answer the resolver may give. The sentences are declared in
`OrdinalResolver.Statement` and rendered here, so this table is a reading of the
code rather than a second copy of it, and the suite compares the two character
for character.

| Verdict | Step | What it says |
| --- | --- | --- |
| `Resolved` | `Ordinal` | The parent resolved on its own provider identifiers and exactly one item under it carries this season and this number. |
| `ParentDidNotResolve` | `Parent` | No candidate's parent is the same work as this item's parent, so the ordinal was never read. An ordinal counts within a series and means nothing until the series is known. |
| `NotNumbered` | `Ordinal` | The item carries no season and number pair and no absolute number either, so it has nothing to be resolved by once its own identifiers have failed to answer. |
| `AbsoluteNumbering` | `Ordinal` | The item is numbered absolutely and carries no season and number pair. An absolute number counts through a series as one provider divided it into seasons, so reading it as a position needs that provider's season lengths, which is the thing that differs between two libraries built from different providers. |
| `CoversMoreThanOneEpisode` | `Ordinal` | The item's ordinal is a range, which is what a file holding more than one episode carries. It is not one episode, so no single item on the peer is the one it is the same as, and taking the item at either end of the range would write two episodes' metadata onto one. |
| `SeasonZero` | `Ordinal` | The item is in season zero, which is the bucket for everything a provider did not place in a numbered season. A special's position inside that bucket is assigned by whichever provider each server used, so two servers agreeing on a number there is not evidence they mean one episode. |
| `NothingAtThatOrdinal` | `Ordinal` | The parent resolved and nothing under it carries this season and this number. What lies nearest to it is not consulted, and an only remaining candidate is not taken. |
| `OrdinalHeldTwice` | `Ordinal` | The parent resolved and more than one item under it carries this season and this number. Which of them a value would be written against is not decidable from the numbering, so nothing is written. |

Only the first names a match. Every other row is an item written into the
unmatched register, and the step it carries is what an operator acts on: a
series that did not resolve is one thing to fix and it fixes every episode
under it, while an episode that did not resolve inside a series that did is a
different thing at the other end of the library.

## What the three awkward numberings cost

The three refusals above are worth reading as costs rather than as rules,
because each of them is a real library that syncs less than an operator might
expect.

An absolutely numbered series does not sync by ordinal at all. On a library
where the episodes carry no identifiers of their own and the numbering is
absolute, that is the whole series. The fix is a provider that identifies the
episodes, and never a conversion here: converting an absolute number to a season
and a number needs the season lengths of whichever provider assigned it, and
those are what the two servers disagree about in the first place.

Specials do not sync by ordinal. Season zero is where a server puts everything a
provider declined to place in a numbered season, so two servers with different
providers hold different things there in a different order, and the number is a
position in a list rather than a name.

A file holding two episodes does not resolve, in either direction. Neither does
a single episode whose number falls inside a peer's range. The two libraries have
split their files differently, which is the case this plugin exists for, and
there is no one item on either side that the other is the same as.

In all three the item is recorded as unresolved with its reason, which is what
makes them liveable. An operator can read them and act; they do not disappear
into a count of items that synced.

## The fixture table

Every row is a test. The rows are read out of this document by the suite rather
than restated in it, so a row added here is run and a row whose expectation
changes here changes what the suite asserts.

An item is written as its parent's identifiers, then a space, then its ordinal.
`Tvdb=121361 S01E05` is episode five of season one of that series, `S01E05-E06`
is a file covering two, `A137` is an absolute number, and `-` in either position
is nothing there. The peer's cell holds the candidates it offered, separated by
semicolons, and an empty cell is a peer that offered none.

The last column is what makes a row worth its line. A near-miss that could not
have failed proves less than one that nearly did, so every row names the mistake
somebody would actually make and this table would catch. The suite refuses a row
that names none and refuses two rows that name the same one, which is the shape a
row takes when it was added for the count and its cell was copied from the row
above. What no check reads is whether the sentence is true of the row it sits on.

| Case | This server | The peer | Outcome | The mistake it would catch |
| --- | --- | --- | --- | --- |
| an episode the peer holds at exactly this ordinal | `Tvdb=121361 S01E05` | `Tvdb=121361 S01E04; Tvdb=121361 S01E05; Tvdb=121361 S01E06` | `Resolved` | A resolver that never answers, which would leave every refusing row below green while nothing ever synced. |
| the ordinal offered under two series, one of which is this one | `Tvdb=121361 S01E05` | `Tvdb=305288 S01E05; Tvdb=121361 S01E05` | `Resolved` | Narrowing by ordinal before narrowing by parent, which takes whichever series was offered first. |
| a series the peer does not hold | `Tvdb=121361 S01E05` | `Tvdb=305288 S01E05` | `ParentDidNotResolve` | Reading the ordinal first and matching S01E05 in whatever series happened to be offered. |
| a peer that offered nothing at all | `Tvdb=121361 S01E05` | | `ParentDidNotResolve` | Reporting an empty candidate set as an episode the peer is missing, when what is not known is whether the series is there. |
| a series carrying no identifiers on this server | `- S01E05` | `Tvdb=121361 S01E05` | `ParentDidNotResolve` | Letting a series with nothing to compare pass the first step, after which the ordinal matches in every series at once. |
| the episode before and the episode after, but not this one | `Tvdb=121361 S01E05` | `Tvdb=121361 S01E04; Tvdb=121361 S01E06` | `NothingAtThatOrdinal` | Taking the nearest number when the exact one is absent. |
| one unmatched episode left under a series that resolved | `Tvdb=121361 S01E05` | `Tvdb=121361 S01E09` | `NothingAtThatOrdinal` | Pairing the only remaining candidate, which reads as arithmetic and is a guess. |
| the same number in a different season | `Tvdb=121361 S02E05` | `Tvdb=121361 S01E05` | `NothingAtThatOrdinal` | Comparing the episode number and forgetting the season it counts within. |
| a peer file covering the number this episode carries | `Tvdb=121361 S01E05` | `Tvdb=121361 S01E05-E06` | `NothingAtThatOrdinal` | Reading a range that contains a number as an item that carries it. |
| the peer holding this ordinal twice under one series | `Tvdb=121361 S01E05` | `Tvdb=121361 S01E05; Tvdb=121361 S01E05` | `OrdinalHeldTwice` | Taking the first of two candidates that carry the same numbering. |
| a file on this server covering two episodes | `Tvdb=121361 S01E05-E06` | `Tvdb=121361 S01E05; Tvdb=121361 S01E06` | `CoversMoreThanOneEpisode` | Resolving a double file to the episode its range begins at, which writes two episodes' metadata onto one. |
| an episode numbered absolutely against a peer numbered by season | `Tvdb=121361 A137` | `Tvdb=121361 S07E12` | `AbsoluteNumbering` | Counting season lengths to convert an absolute number into a season and a number. |
| an absolute number on both sides | `Tvdb=121361 A137` | `Tvdb=121361 A137` | `AbsoluteNumbering` | Resolving on an absolute number because both sides happen to carry one, when neither says which provider counted. |
| a special in season zero the peer numbers the same way | `Tvdb=121361 S00E02` | `Tvdb=121361 S00E02` | `SeasonZero` | Treating season zero as a season, where the number is a position two providers assign differently. |
| an episode carrying no numbering at all | `Tvdb=121361 -` | `Tvdb=121361 S01E05` | `NotNumbered` | Reading an absent number as zero and matching whatever the peer put first. |

## What is not written here yet

What happens to an item that did not resolve, beyond that it is recorded. The
register itself, its reason per item and what an operator does with it are #29,
and the two-step failure above is written to be recorded in it: the step is
carried on every answer so the register can say which of the two failed rather
than that something did.
