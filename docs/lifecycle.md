# Disabling, uninstalling and reinstalling

Three different acts, and an operator doing any of them deserves to know what is
left behind before they do it rather than afterwards. Each row below says what
happens to three things: the library, the plugin configuration, and the plugin's
own store.

| Act | The library | The configuration | The store |
| --- | --- | --- | --- |
| Disable | Untouched. Nothing runs, so nothing is written. | Kept. | Kept. |
| Uninstall | Untouched. Nothing is removed and nothing is reverted. | Kept by the server, which holds it. | Kept, deliberately, and the readme says so. |
| Reinstall | Untouched. | Found and read. | Found, checked for its version, and resumed from. |

## Disabled

The plugin's actions stop and everything it holds stays.

Metadata already written to the library stays too, and that is worth saying
plainly rather than leaving to be inferred. A value this plugin wrote is library
data from the moment it lands. It is what an operator sees in their own server,
what their own clients show, and what their own backups carry. Disabling a
plugin is not an instruction to walk back what it already did.

A disabled plugin that keeps its records can be enabled again without starting
blind, which is the behaviour an operator expects from disabling anything.

## Uninstalled

The server calls a hook on the way out:

    git grep -n "public virtual void OnUninstalling" v12.0-rc4 -- MediaBrowser.Common/Plugins/BasePlugin.cs
    v12.0-rc4:MediaBrowser.Common/Plugins/BasePlugin.cs:76:        public virtual void OnUninstalling()

What that hook does here is a decision rather than a default, and the decision
is to keep the store and to say so.

Deleting it would mean a reinstall starts blind. Every field this plugin
previously wrote would then look like a value somebody here edited, because the
record that says otherwise is gone, and the next pass would produce a conflict
on each one. That is a large, silent cost paid by somebody who uninstalled by
accident or to try something.

Keeping it means an uninstalled plugin leaves data on disk, which an operator
uninstalling for privacy reasons did not ask for. That cost is real and it is
paid deliberately, because it is visible and reversible: the readme states what
is left, and removing it is an action somebody takes on purpose. A destructive
default that fires on an accidental uninstall is worse than a documented
remnant, because only one of the two can be undone.

Nothing is removed from the library at uninstall, in any case. Removing a
field's value and removing the item that holds it are different acts, and only
the first is ever in scope for this plugin at all.

## Reinstalled

The store is found rather than created. Its version is checked, and a store
written by a newer version of this plugin is refused rather than read, because a
version that did not exist when a record was written cannot know what the record
means. The plugin then resumes, which means the next pass re-derives its
resolutions and its plan rather than continuing an old one.

## Revocation is a fourth act

Revoking a pairing is not any of the three above, and it is the one act that
does reach back into what already moved. What arrived through the sync is taken
back. That was decided on 2026-08-09 in #1, and it is the decision in this plan
that reaches furthest, because taking a value back is only safe where this
plugin can prove it put that value there.

The bound is #66, and it is not a detail of the revert. It is what makes a
revert allowable at all. A field this plugin cannot prove it wrote is left
alone and counted, never reverted on an assumption. So a revocation against a
library where the record is incomplete removes less than everything that ever
arrived, and the count of what it did not touch is part of the answer rather
than a footnote to it.

Two other answers were considered and are not taken. Both are written here with
what they cost, because a decision that records only what was chosen reads as
the only thing anybody thought of.

Leaving the metadata in place costs nothing to build, and most of this document
already argues in its favour: a value written into the library is library data,
which is the sentence the disable row above rests on. It is not taken because
unpairing would then not undo the transfer. Two households separating their
servers would each keep whatever the other had sent, with no act left that walks
it back, and an operator who revokes a pairing for a reason is the operator who
wanted that back.

Making it the operator's choice at revocation time is the answer that looks
kindest and is the most expensive one. A choice is only a choice where the
person making it can see what each branch does, so it needs the attribution
record and the previous values in place first, and it needs a confirmation
stating how many fields fall into the not-known case before anything happens.
Offered without those it is a question an operator cannot answer, asked at the
moment they are least able to study it. It is not refused on principle. What
refuses it is that it is strictly more than the answer above, and the answer
above has to exist either way.

The mechanism is #64 and none of it is built.

## What of this is true of the tree today

The decisions above are made. The store they are decisions about exists; the two
mechanisms that act on it do not.

**This section said there was no store and that nothing in this plugin wrote
anything to a disk.** #16 built one and both sentences stopped being true with
it. Nothing here caught that: this document is held by no test, which
`docs/storage.md` is, and the guard there reddened on the same change while this
page went on describing the tree from before it.

    git grep -ln "FileStream\|StreamWriter\|File.Write\|File.Create\|File.AppendAll" -- 'Jellyfin.Plugin.MetadataSync/'
    Jellyfin.Plugin.MetadataSync/Store/WrittenValues.cs

What it keeps is what this plugin wrote, per pairing, per item and per field,
bounded at ten values each with the oldest dropped first. `docs/storage.md` is
where that is argued and what the bound costs is stated; it is not restated here.
#59 is still how a store survives a version change, and that is where the refusal
of a newer-version store belongs.

So the uninstall row above is now a decision about a file that exists rather than
about one that will. Deleting the store would make a reinstall start blind and
produce a conflict on every field this plugin previously wrote; keeping it leaves
data on disk an operator uninstalling for privacy did not ask for. Only one of
those can be undone, which is why the store is kept.

The uninstall hook is still not overridden:

    git grep -n "OnUninstalling" -- Jellyfin.Plugin.MetadataSync/Plugin.cs ; echo "exit=$?"
    exit=1

and with the store kept rather than deleted, an override would be an empty method
either way. What the row above decides is that nothing happens to the store on an
uninstall, and a method whose body does nothing is not how a decision like that
is recorded. #62 carries whether one is owed at all.

Nothing here is told that a pairing was revoked either, so the fourth act has no
way to start. This plugin codes against a consumer contract that is published
nowhere yet, and the document stating what it would ask for is not in this tree:

    git ls-tree -r --name-only origin/master -- docs/ | grep -c 'consumer.md'
    0

Both halves of the answer above are therefore written and neither is built. The
event that would start a revert still has nothing to arrive on. What has changed
is the other half: the record that would bound a revert is no longer absent, so
what #64 waits on is the event and the operation rather than the store as well.
