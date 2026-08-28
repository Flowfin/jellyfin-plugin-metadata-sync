> [!NOTE]
>
> **Part of [Flowfin](https://github.com/Flowfin).** It works with any Jellyfin
> server, and with the Flowfin clients.

# Metadata Sync

Two Jellyfin servers, in two households, holding two libraries that overlap. One
operator fixes a wrong title, writes a better description, corrects an age
rating. This plugin carries that work to the other server, and it carries
nothing else.

It is worth knowing what it will not do before knowing what it will, because the
refusals are what make it safe to point at a library somebody is responsible for.
They are listed first for that reason.

## Where this is today

There is no release. This repository has published none, which you can check:

    gh release list --repo Flowfin/jellyfin-plugin-metadata-sync

Nothing comes back. There is no tag either, so there is nothing to install from
a catalogue and nothing to install by hand.

What is built today is a set of parts a pass would be made of. The field
register and the conflict rules, and the planner that puts those two together
into a plan. The write path that carries a plan to this server's library. The
provider identifier comparison and the reference comparison, each a decision
nothing has been wired to yet. And the suite that holds all of them up.

What is not built is the pass itself. Nothing schedules one, nothing starts one,
and nothing reads the other server, so none of the parts above is reached by any
route an operator can take:

    git grep -In "IScheduledTask" -- 'Jellyfin.Plugin.MetadataSync/'
    # no output, exit 1

The pairing consumer is not written either, and this plugin references no
pairing package, so there is nothing for the parts above to be handed. The
administrator surface is not written: the one page this plugin declares says it
has no settings.

Read the sections below as what this plugin is being built to do, and read this
section as the reason not to expect any of it to run yet.

## What this plugin refuses to do

No video, no audio, no subtitle sidecar, no file of any kind moves between the
two servers. This plugin reconciles what a
library says about a work, and moving the work itself is a different program.

Image bytes stay where they are: not posters, not backdrops, not logos, not
thumbnails, not people's photographs. An image is a file, and copying one is
copying media by another name.

Both of those are permanent. They are not features held back for a later
release, and there is no configuration that turns either on.

The cost is real and it lands on artwork. Two servers that have different
posters stay that way. An operator who curated a poster by hand on one server
has to do it again on the other, and this plugin will not tell them the two
disagree.

Played state, playback positions, favourites and personal ratings belong to a
user and not to a work, and this plugin neither reads nor writes any of them.
Nor does it move anything else scoped to a person. The plugin
that does move them is
[jellyfin-plugin-watch-sync](https://github.com/Flowfin/jellyfin-plugin-watch-sync),
and an operator who wanted watch state and installed this one has installed the
wrong plugin.

The cost is that reconciling metadata and carrying watch state are two installs
rather than one. What it buys is that a review of what this plugin exposes about
a person does not have to reason about user state at all, because the surface is
not reachable from here.

**It does nothing at all without a pairing, and it does not do the pairing.**
Trust, key material, peer addresses and user mapping live in
[jellyfin-plugin-server-pairing](https://github.com/Flowfin/jellyfin-plugin-server-pairing).
This plugin holds no key material, no peer address and no credential of its own,
so there is no second thing to rotate, revoke or leak, and no path by which
metadata leaves your server without going through that plugin.

The cost is a second install and a pairing an operator has to set up by hand.
There is no discovery, nothing is automatic, and two servers that have not been
paired deliberately will never exchange anything.

**It never decides that two items are the same from a file path, a filename or
file bytes.** Identity comes from provider identifiers and from nothing else. If
they cannot answer, the answer is that the item does not resolve, and it is
recorded as unresolved rather than guessed at.

This is the difference from every prior attempt in this space, and it is the
reason to choose this one. Two libraries do not need the same files, the same
directory layout or the same names. They are two libraries that two people
built, which is the case the plugin exists for.

The cost is that an item with no provider identifiers does not sync at all. Not
partially, not by title, not by anything. The way to fix that is to let a
metadata provider identify the item, and never to rename files to match.

**It plans before it applies.** A pass produces the full plan, item by item and
field by field, showing the current value and what would replace it, and writes
nothing. Applying is a separate action an operator takes on purpose. The server
has no undo for a bulk metadata edit, so the plan is the undo.

**It refuses a field nobody declared.** Every field that may move has a row in
the register, with a reason written next to it. A field with no row is refused
when it is asked for, rather than merged, guessed at, or quietly skipped. The
cost is that adding a field is a decision somebody argues for in the open, and
not a configuration checkbox.

## What does not sync

Read this before installing. This plugin moves a declared set of metadata
fields between two paired servers and nothing outside that set. Which fields,
and the reason for each one, is
[docs/field-register.md](docs/field-register.md); a field with no row there does
not move whatever the configuration says. The table under
[what each row means for your library](docs/field-register.md#what-each-row-means-for-your-library)
is the one to read first: it answers, per field, whether what you wrote here
survives and whether what you fixed here reaches the other server.

Two things an operator is likely to expect are deliberately absent from 1.0.
Collections and playlists do not sync. A collection is a set of references to
other items, so carrying one means resolving every member, and a member that
does not resolve on the other server looks exactly like a member somebody
removed on purpose. A sync that cannot tell those two apart shortens the
collection a little more on each pass. Playlists have the same problem and
usually belong to a user besides.

That is a scope decision rather than a refusal for all time, and the register
states what would have to be true before a later release could carry them.
Image bytes are the separate case: they never move, for their own reason, which
the register also states.

## What an uninstall leaves behind

Disabling the plugin stops it. No pass runs and nothing is removed. Metadata
that already reached your library stays, because once a title is written it is
your library's title and not this plugin's.

Uninstalling keeps the plugin's own records rather than deleting them. That is a
decision and not an oversight: deleting them means a reinstall starts blind,
treats every field it previously wrote as somebody's local edit, and produces a
conflict on each one. A destructive default that fires on an accidental
uninstall is worse than a remnant that is written down here.

Those records exist. They are files in the plugin's own data folder, and they
are what an uninstall leaves behind:

<!-- the files this plugin leaves in its data folder: one per line, the file first, read by ReadmeStatementTests -->

- `written-values.jsonl`, what this plugin wrote to your library and what stood
  there before each write, keyed by pairing, item and field, and bounded at ten
  values per field
- `pass-progress.jsonl`, which items a sync had finished with when it was
  interrupted, so the next one continues instead of starting again. It is
  emptied whenever a sync runs to the end
- `store-format.json`, one number saying which format the files beside it are
  written in

<!-- end of the files left behind -->

`docs/storage.md` is where each of them is argued and where that bound is
costed.

Removing them is therefore a deliberate action rather than a side effect. The
plugin carries the removal itself, per pairing, and what is missing is a route
an operator can take to it: the administrator surface is not written, which the
section at the top of this file states with the command behind it.

Nothing this plugin does ever deletes an item.

## Checking that what you downloaded is what this repository built

A release is built by a workflow run here, and that run writes two checksums for
the archive and signs a provenance statement for it. Three commands check a file
on your disk against those, and none of them needs anything from this repository
beyond the release itself.

The sidecars are named after the archive with the `.zip` replaced rather than
appended, so an archive named `<archive>.zip` has `<archive>.md5` and
`<archive>.sha256` beside it. Run them from the directory the archive is in:

    md5sum -c <archive>.md5
    sha256sum -c <archive>.sha256

Those two say the bytes match what the release published. They do not say who
published it. The provenance does, by tying the archive to the workflow run and
the commit it was built from:

    gh attestation verify <archive>.zip --repo Flowfin/jellyfin-plugin-metadata-sync

A failure from any of the three means the file is not the one this repository
built, and the answer to that is to stop rather than to install it and watch.

There is nothing to run them against today. No release exists, which the section
at the top of this file says and this prints nothing for:

    gh release list --repo Flowfin/jellyfin-plugin-metadata-sync

So the commands above are what a release will be checkable with, and nothing
here claims that anybody has checked one.
[docs/RELEASING.md](docs/RELEASING.md) is the other end of the same route and
says what a run produces and what makes it fail.

## Security

Report a vulnerability through this repository's private vulnerability
reporting rather than in a public issue. [SECURITY.md](SECURITY.md) gives the
route, what a report should carry, what is in scope, where the pairing plane
and the server go instead, and what this plugin deliberately does not defend
against. [docs/threat-model.md](docs/threat-model.md) names the adversaries,
says what each one obtains and what stops them, and gathers the risks that
stay after the defences are built.

[docs/personal-data.md](docs/personal-data.md) is the other half and answers a
different question: what this plugin moves when everything is working
correctly, how much of it can be personal data, whose it is, where it goes, and
what may never appear in a log.

## Where the detail is

- [docs/field-register.md](docs/field-register.md) is the set of fields that may
  move, one row each, with the reason. It is the document to read before
  deciding whether to let a pass apply.
- [docs/personal-data.md](docs/personal-data.md) states what can be personal
  data among what moves, whose it is, and what may never reach a log.
- [SECURITY.md](SECURITY.md) is the reporting route and the scope.
- [docs/threat-model.md](docs/threat-model.md) names the adversaries and the
  risks that remain after the defences.
- [docs/provider-identifiers.md](docs/provider-identifiers.md) is how two items
  are compared, identifier by identifier.
- [docs/testing.md](docs/testing.md) is the headless test policy, and
  [docs/refused-tests.md](docs/refused-tests.md) is the register of tests this
  suite will not carry and what covers the property instead.

## Building

    dotnet build Jellyfin.Plugin.MetadataSync.sln
    dotnet test

The suite needs no display, no elevation, no running server and no network.
[docs/testing.md](docs/testing.md) is the policy that keeps it that way and says
what it does and does not enforce.

[CONTRIBUTING.md](CONTRIBUTING.md) is how a change gets in, and
[docs/RELEASING.md](docs/RELEASING.md) is what a release is made of.
