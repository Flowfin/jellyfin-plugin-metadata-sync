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

A line that never finished, which is what a pass killed part way through a write
leaves behind, is dropped on the next read and counted rather than thrown. A
store that refused to open after a power cut would have turned one lost write
into every lost write.

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

## What happens to a record whose pairing no longer exists

It stays, and nothing reads it as another pairing's.

The pairing is a component of every key, and a pairing identifier is derived from
the two servers' public keys with revocation terminal, so two servers that pair
again after a revocation carry a different identifier. The rows of the pairing
that ended are therefore inert rather than misleading: the key that would reach
them is one nothing asks about again, and a later pairing between the same two
servers reads none of them.

They are not deleted either, and that is a decision rather than an omission.
Removing them is #61, which is an act an operator asks for and is told the count
of, and a store quietly dropping them at the next restart would have nothing left
to report when they did ask. What that costs until #61 lands is stated here
rather than left to be found: a server that has paired and revoked several times
keeps every one of those pairings' rows, bounded per item and per field but not
by the number of pairings, and nothing on any surface says so yet.

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
survives a version change, and it is not built either.

So a reader should take the conflict log row and the unmatched register row as a
decision already made about where something goes, and the row above them as a
description of a file that is there.
