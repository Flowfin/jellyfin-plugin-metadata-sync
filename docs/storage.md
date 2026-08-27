# What is stored where

Three places, and the point of the table is that the third one is a place. A
thing this plugin deliberately does not keep is a fact about it, and leaving
that off the list turns an absence into an oversight the next reader has to
rediscover.

| What | Where | Why there |
| --- | --- | --- |
| Which libraries take part, which direction a pairing runs, how often a pass runs, and every other choice an operator makes | the plugin configuration | A choice is a decision somebody made and can hand to somebody else. It is also the only thing safe to paste into a support thread, and that is a property this table exists to keep true. |
| What this plugin wrote, per item and per field, and the value that was there before | the plugin's own store | It is data rather than a decision, it grows with the library, and it is what makes a later pass able to tell a value it wrote from a value an operator edited. |
| The conflict log and the unmatched register | the plugin's own store | Same reason. Both grow with the library, both are read behind an administrator surface, and neither is anybody's setting. |
| Key material, a peer address, a credential, a pairing secret | neither | This plugin holds none of it, in any form, at any moment. Pairing, trust and transport belong to the pairing plugin, and the rule that keeps this true is structural rather than remembered. |
| Played state, playback position, favourites, a personal rating | neither | Nothing scoped to a person is read or written here at all. The plugin that moves it is the watch history plugin, and the plugin assembly is scanned for the server types that carry it. |
| Image bytes and media files | neither | Permanent non-goals. Neither is copied, so neither is stored. |

## Why the configuration is the narrow one

The plugin configuration is serialised to a file on disk with the server's own
permissions. The server reads it back, hands it to the plugin page, and an
operator whose sync is not working attaches it to a bug report. Everything on
that route should be a choice they made.

So the configuration holds choices and the store holds data, and the split is
not a preference. It is what makes the sentence in the operator guide, that a
configuration file is safe to attach to a bug report, a true sentence rather
than a hope.

One member of the configuration is not a choice, and it is named here rather
than left for a reader to find in the type. `Format` says which shape the file
is written in. It is a stamp the same way `store-format.json` is one for the
store, it is read and never chosen, and no page offers it. What the rule above
is against is data - a value copied out of a library, a peer address, a
credential - and a number naming this file's own shape is none of those: it says
nothing about anybody and it is safe on the same route as everything beside it.
The two stamps are separate numbers on purpose, because a configuration restored
from a backup beside a store that was not is a state an operator reaches without
doing anything unusual, and one number covering both would say the wrong thing
about whichever of the two moved.

## What holds it up

`ConfigurationShapeTests` in the suite reads the plugin's configuration type and
refuses two things. A property nobody put in the allowed set, so a setting is
added on purpose rather than in a diff nobody reads. And a property whose type
is not the shape a choice is made of, so a store, a log entry, a record of what
was written or a library item is refused by what it is made of rather than by
what it is called.

Both were proved by making them fail, one at a time, and each reddens exactly
its own leg.

That is the whole of the enforcement, and the bound is worth knowing. It reads
the configuration type, so data written to disk by some other route is outside
it. It cannot tell a sensitive string from an ordinary one either: a peer
address is a string like any other and no reading of the type says which. What
keeps the address out is that this plugin never learns one, which is #20 and is
structural rather than a rule anybody keeps.

## What writes to a disk

The store exists. This section said it did not, under a command with an empty
result pasted below it, and #16 built it. The same command returns something
now, and what it returns is named rather than counted:

    git grep -ln "FileStream\|StreamWriter\|File.Write\|File.Create\|File.AppendAll" -- 'Jellyfin.Plugin.MetadataSync/'

<!-- the plugin sources that write to a disk: one per line, the file first, read by StorageStatementTests -->

- `Jellyfin.Plugin.MetadataSync/Store/WrittenValues.cs`, the plugin's own store
  of what this plugin wrote
- `Jellyfin.Plugin.MetadataSync/Store/StoreFormat.cs`, the stamp saying which
  format the files in that directory are written in

<!-- end of the sources that write -->

That list is not maintained by hand. `StorageStatementTests` holds it against
the plugin's own sources, in both directions, so a second file that writes to a
disk arriving with no line here is red and a line here naming a file that has
stopped writing is red too. A reader who wants to know what touches a disk in
this plugin reads that list and nothing else.

## What the store keeps, and what its bound costs

One file under the plugin's own data folder, `written-values.jsonl`, carrying a
line per write rather than one document rewritten. A first pass over a modest
library writes tens of thousands of fields, and rewriting the whole file per
field would cost the square of that. The cost of the shape it takes instead is
stated rather than hidden: the file carries superseded lines until it is
rewritten, and it rewrites itself once it is carrying enough of them to be worth
the whole-file cost.

**The bound is ten values per item and per field, oldest dropped first.** It was
decided on #16 on 2026-08-24. It is a count rather than an age because nothing
here compares two servers' clocks and an age would need one, and it is stated as
a number because a number is what an operator can be told. Ten is more than the
conflict rules need, which decide from the newest value alone; the rest is for
#64, which cannot revert a field it holds no earlier value for.

What the bound costs is that attribution is not permanent. A field this plugin
wrote eleven times holds no record of the first, so an act asking what was there
before that write cannot be answered for it. #66 is where a surface reporting on
attribution has to say so rather than report a clean number, and a store that
declared no bound would have made the same loss invisible instead of stating it.

**The discard itself is not recorded, so the loss is unaskable as well as
unrecoverable.** A field this plugin wrote eleven times and a field it wrote
exactly ten are the same store afterwards: the same history, the same last
written value, the same rows in the report, the same counts. Nothing separates a
field whose earliest value the bound dropped from a field that was never written
that far back. `BoundDiscardTests` holds that as a negative disclosure, in both
halves - the two stores are asked every question this one takes and agree on all
of them, and the set of members the disclosure is about is compared with the set
the store carries, so a member added to answer this reddens rather than leaving
this paragraph saying the opposite of the tree.

What that costs is carried by two issues rather than by this file. The surface
#66 owes, which has to say attribution is incomplete where the bound has
discarded records, cannot derive that from this store; nor can the confirmation
#64 owes, which has to state counts before a revert touches anything. Both would
be reporting a clean number because a clean number is the only one there is.

It is not a counter somebody forgot. A count kept only in memory is lost at the
next restart, and one written into the file is lost at the next compaction,
because the file is rewritten from what is retained rather than having lines
struck out of it. Making the loss attributable therefore changes what a line
carries, which is a step of the format above and belongs with the migration
mechanism in #59 rather than being added beside the bound.

A line that never finished, which is what a pass killed part way through a write
leaves behind, is dropped on the next read and counted rather than thrown. A
store that refused to open after a power cut would have turned one lost write
into every lost write.

**A library that disappears leaves its rows exactly where they are.** #42 asks
for that state to be chosen rather than inherited, and this is the choice: rows
for an item that is in no library any more are kept, are still counted in the
report an operator is shown, and go when that pairing's rows go and not before.

It is the answer rather than the absence of one, and the reason is what the key
already says. A row is filed under a pairing, an item and a field, and under
nothing else; this store asks the server nothing, so a library disappearing is
not an event it can see. Cleaning up after one would mean keying the store
differently in order to know what to clean, and then deleting this plugin's own
proof that it wrote a value on an event it cannot distinguish from a library that
is temporarily unavailable. A record deleted is a field that can no longer be
attributed, which is the direction #66 refuses.

What is refused instead is the configuration still naming a library the server
does not hold, which is where a disappearance is visible and is caught before a
pass reads anything. `OrphanedRowTests` holds the key this choice rests on: a
question about a written value that grew a fourth thing to be filed under reddens
it, because that is the change that would make pruning by library possible in the
first place.

## What a line carries, and why the second half of it is not derived

A line carries the value written and the value that was on the item immediately
before it. That is one line rather than two because it is one write, and the
second half is what makes the record readable by somebody other than the next
pass: a conflict log entry showing a decision without it asks an operator to
remember what their own overview used to say, and #64 has nothing to put back.

**The value that was replaced arrives from the write path and is never worked out
here.** The two candidates look interchangeable and are not. What this store
already holds for a field is what this plugin wrote last time; what a write
replaces is what the library held at the moment of the write. On a field nobody
here touched between two passes those are the same string, and on the field this
whole record exists for they are not, because an operator edited it in between.
A store deriving the previous value from its own newest entry would record this
plugin's own earlier value and lose exactly that edit, in the one place that
could have shown it.

A null on either half is a field that held nothing rather than a half nobody
recorded. What says there is no record at all is an empty history, which is a
different answer from an entry carrying nulls.

## What says which format the store is in

A file in that directory says which format the files beside it are written in.
It is `store-format.json`, it carries one number, and it is read before any
other file in the directory is opened.

The case it exists for is a downgrade. A newer build writes the store, the
operator puts an older build back, and the older build meets a file whose shape
it does not know. Every reader here drops what it does not understand - that is
what a JSON reader does with a member it has no property for - so the loss is
silent, and the next compaction rewrites the file without what the newer build
had put in it. Reading half of a newer file successfully is how a downgrade
destroys data, and the only moment that can still be prevented is before the
first line is read.

**So a store this build cannot place is not opened at all.** A stamp naming a
format newer than this build is refused, and a stamp this build cannot read is
refused as well rather than being taken for the earliest format: a newer file
whose stamp was damaged would otherwise be opened, dropped to what this build
understands, and written back that way. Neither refusal writes anything, so the
directory an operator has to put the newer build back for is exactly as the
newer build left it.

A directory with no stamp is the earliest format rather than an error. Two
things produce one - a plugin that has never written, and a directory written
before the stamp existed - and both hold files of that format. Every later
format is stamped, so an absent stamp cannot mean anything else.

The stamp arrives with the first write and not before it. Reading a store does
not create one, because a directory with no store in it is the state a plugin is
installed in, and a read that stamped it would turn every question about the
store into a write.

**What this is not is a migration.** One format has existed, so there is no step
from one to another and nothing has been migrated. What is built is the half
that has to exist before the first shape change rather than after it: the
artefact says which shape it is, and a build that meets a shape it does not know
stops. This paragraph said the configuration's half of the same question was not
built, and it is: the configuration carries its own stamp, read by the same
validation that refuses every other unusable configuration, so a file written by
a newer build disables every action instead of being acted on under rules it was
not written under. The chain of steps is where #59 says it is and is not built,
and it is the half neither stamp stands in for.

## What an operator can ask for, and what a removal is not

Two questions, and they are the same question asked before and after a decision:
what does this plugin hold about one pairing, and let go of it.

The answer to the first is a report over every store this plugin owns, one entry
per store, the stores holding nothing included. It carries a count per store and
every row behind that count, and it can be handed over as a document. The
document is produced rather than written to a disk: an export saved into the
plugin's own data folder would be the most personal artefact this plugin makes,
sitting beside the store with nothing to clean it up. Which surface hands it to
an operator is #51, and until that exists nothing offers the document to anybody.

**Removing these records does not change the library.** Metadata this plugin
already wrote stays on the items it was written to. An operator asking for a
pairing to be gone may mean either act, so the document says which one this is in
its own opening lines, and a walk in the suite refuses a removal path that can
reach the library at all. Putting values back is #64 and is a different act with
a different confirmation.

Every store answers for a pairing through one interface, and that is a constraint
on how a store is built rather than on the report. A report assembled from a list
of stores somebody wrote down goes on passing the day a sixth store is added and
stops being true in the same act, so the suite derives the set in two directions:
every plugin source that persists declares a store of that shape, and every store
of that shape is registered. #61 asked for exactly this before any store existed,
on the ground that it costs a sentence now and a migration of every store later.

## What happens to a record whose pairing no longer exists

It stays until somebody asks for it to go, and nothing reads it as another
pairing's.

The pairing is a component of every key, and a pairing identifier is derived from
the two servers' public keys with revocation terminal, so two servers that pair
again after a revocation carry a different identifier. The rows of the pairing
that ended are therefore inert rather than misleading: the key that would reach
them is one nothing asks about again, and a later pairing between the same two
servers reads none of them.

Nothing deletes them on its own, and that is a decision rather than an omission.
A store dropping them at the next restart would have nothing left to report the
day an operator asked what it held. They go when somebody asks, through the
removal above, and the file is rewritten from what is left rather than having
lines struck out of it.

What that costs while nothing asks is stated here rather than left to be found: a
server that has paired and revoked several times keeps every one of those
pairings' rows, bounded per item and per field but not by the number of pairings.
Nothing offers an operator the question yet either, because the surface that
would ask it is #51, so today the removal is reachable from the container and
from nowhere a person can press.

## What it reads rather than keeps

Beside the store the plugin reads a set of declared tables, embedded in the
assembly rather than kept anywhere, which is the opposite direction:

<!-- the tables embedded in the assembly: one per line, the file first, read by StorageStatementTests -->

- `Jellyfin.Plugin.MetadataSync/Fields/field-register.json`, which fields may
  move between two servers
- `Jellyfin.Plugin.MetadataSync/Matching/provider-identifiers.json`, which
  provider identifiers name one work
- `Jellyfin.Plugin.MetadataSync/Conflicts/conflict-rules.json`, how a
  disagreement between two servers is decided
- `Jellyfin.Plugin.MetadataSync/References/reference-comparison.json`, when two
  spellings of a person, a studio or a genre are one thing

<!-- end of the tables -->

That list is not maintained by hand. `StorageStatementTests` holds it against
what the plugin project declares as an embedded table, in both directions, so a
table added to the assembly with no line here is red and a line here naming a
table the assembly does not carry is red too. This sentence used to say two, and
the two that arrived after it was written are the third and fourth above.

Of the three rows in the table that name the store, one is a file on a disk now
and two are still decisions about where something will go. What this plugin
wrote, per item and per field, is the first: #16 built it and #47 made it carry
what each write replaced, which is the provenance the same record holds rather
than a second copy of it. The conflict log and the unmatched register are the
other two, and #48 and #29 are where they are built. #59 is how any of it
survives a version change; the stamp above is the half of that which the store
now carries, the configuration carries a stamp of its own for the same reason,
and the chain of steps is not built.

So a reader should take the conflict log row and the unmatched register row as a
decision already made about where something goes, and the row above them as a
description of a file that is there.
