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

## What does not exist yet

The store does not. Nothing in this plugin writes anything to a disk today. The
two files it does read, the field register and the provider identifier table,
are embedded in the assembly and are read rather than kept, which is the
opposite direction. So the rows above that name the store are where those things
will go and not where they are. #16 is the issue that builds it, #47 is the
record it holds first, and #59 is how it survives a version change.

A reader should take the store rows as a decision already made about where
something goes, and not as a description of a file on a disk.
