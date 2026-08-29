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

The mechanism is #64. **The half that decides is built and the half that starts
it is not.** `Revert` reads the record and what the library holds now and
answers with what it would put back and what it would leave alone, which is a
plan and a set of counts that change nothing by existing; carrying it out is the
ordinary write path's. What has nothing to arrive on is the revocation itself,
which reaches this plugin over a contract published nowhere, and what has nowhere
to be shown is the confirmation, which needs an administrator surface.

The bound above is where the decision is sharpest, and it is worth reading before
the counts are trusted. A value goes back only where this plugin can prove two
things: that it wrote what is there now, and that it can produce what was there
before it ever wrote. The second is not free. A field's history is bounded and
the discard is not recorded, so a history standing AT the bound may already have
lost the write that came first and its earliest surviving value may itself have
come from the peer. Restoring that would put the peer's own value back in the
name of removing it, so it is left alone and counted. A history shorter than the
bound has had nothing discarded, which is what makes this decidable rather than
assumed. `docs/storage.md` argues the bound and this consequence of it.

## What of this is true of the tree today

The decisions above are made. The store they are decisions about exists, and so
does the half of the revert that decides what it would do; what does not exist
is anything that would start one. **This paragraph said neither mechanism was
built.** `Revert` is in the tree and is exercised by the suite, and nothing
constructs it, so what changed is what a reader is owed rather than what an
operator can do.

**This section said there was no store and that nothing in this plugin wrote
anything to a disk.** #16 built one and both sentences stopped being true with
it. Nothing here caught that, because this page was held by no test while
`docs/storage.md` was, so the guard there reddened on the change that falsified
both and this page went on describing the tree from before it. That is repaired
below rather than only regretted: every claim this section makes about the tree
is now a list the suite reads back out of this file and compares with the tree,
so the next one to go stale reds instead of waiting to be noticed.

`LifecycleStatementTests` is what does the reading, and its own bound is written
at it rather than here.

What writes to a disk:

    git grep -ln "FileStream\|StreamWriter\|File.Write\|File.Create\|File.AppendAll" -- 'Jellyfin.Plugin.MetadataSync/'

<!-- the plugin sources that write to a disk: one per line, the file first, read by LifecycleStatementTests -->

- `Jellyfin.Plugin.MetadataSync/Store/WrittenValues.cs`, the plugin's own store
  of what this plugin wrote
- `Jellyfin.Plugin.MetadataSync/Store/PassProgress.cs`, the record of which
  items a pass had finished with when it was interrupted
- `Jellyfin.Plugin.MetadataSync/Store/ConflictLog.cs`, the account of what this
  plugin decided about each field it looked at
- `Jellyfin.Plugin.MetadataSync/Store/StoreFormat.cs`, the stamp saying which
  format the files in that directory are written in

<!-- end of the sources that write -->

`docs/storage.md` carries the same list under its own guard. Two derived lists
are not one list restated: neither is typed by hand, so a second file that writes
to a disk reds both pages at once rather than reddening the one somebody
remembered.

What it keeps is what this plugin wrote, per pairing, per item and per field,
bounded at ten values each with the oldest dropped first. `docs/storage.md` is
where that is argued and what the bound costs is stated; it is not restated here.
#59 is still how a store survives a version change. The refusal of a store
written by a newer build is landed: the directory carries a stamp saying which
format it is in, and a build meeting a format it does not read opens nothing in
that directory. `docs/storage.md` is where that is argued. What is not built is
the chain of steps between two formats, because one format has existed.

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
is recorded.

**No override is owed, and that is decided here rather than deferred.** #62 asked
whether one was, and the answer is no, for three reasons that hold together. An
empty body records nothing a reader can act on: it is indistinguishable from a
method somebody began and did not finish, and whoever opens it learns less than
whoever opens this page. The absence is not assumed either, which is what would
have made the answer unsafe - the list below is the whole set this plugin takes
over from the server's plugin base, compared in both directions, so the hook
arriving reddens this page and the decision is read again at that moment rather
than being quietly replaced by a body. And the one thing an override would
provide, a place for a later act to sit, is not worth having early: a site that
runs at uninstall arrives with the act that needs it and with the proof that it
bites, and one waiting empty for that act has neither.

So this plugin contributes no code at all to an uninstall. Nothing of its own
runs, so nothing of its own can reach the library at that moment, and that is the
form the guarantee takes here rather than a check over a method that does not
exist.

That sentence is the one most likely to go stale, because #62's own third
condition is the thing that would falsify it. What the suite holds is not the
absence of one name but the whole set this plugin takes over from the server's
plugin base, so the hook arriving reds this page, and so does any other member
this page has not been told about:

<!-- the members Jellyfin.Plugin.MetadataSync/Plugin.cs overrides: one per line, the member first, read by LifecycleStatementTests -->

- `Name`, the name the server shows for this plugin
- `Id`, the identifier the server files it under

<!-- end of the members overridden -->

Nothing here is told that a pairing was revoked either, so the fourth act has no
way to start. This plugin codes against a consumer contract that is published
nowhere yet, and the document stating what it would ask for is not in this tree:

    git ls-tree -r --name-only origin/master -- docs/ | grep -c 'consumer.md'
    0

That is a negative and it stays one. What the suite holds is the absence itself,
so the day the document arrives this page reds rather than going on saying it is
missing:

<!-- the paths this page says the tree does not carry: one per line, the path first, read by LifecycleStatementTests -->

- `docs/consumer.md`, the document stating member by member what this plugin
  would ask the pairing plugin for, which #20 writes

<!-- end of the paths the tree does not carry -->

Both halves of the answer above are written, and the operation is now built as
well. What this paragraph said was that neither half was built and that #64
waited on the event and the operation; the operation landed with #64's own
change, so what is left is the event and the surface. The revocation reaches this
plugin over a contract published nowhere, and the confirmation the counts are for
has no administrator surface to be shown on.
