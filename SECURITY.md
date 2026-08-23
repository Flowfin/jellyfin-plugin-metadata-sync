# Security policy

This plugin writes to somebody's library on the strength of data that arrived
from another machine. That is the whole reason this file says more than where
to send a report.

## What exists today

Nothing in this plugin moves a field yet, and that is a statement about a pass
rather than about an empty tree. The parts a pass would be made of are here, and
each one is named below with the file it is in, so a reader can open the thing
rather than take this file's word for it.

<!-- the parts in the tree: one per line, the file first, read by SecurityPolicyTests -->

- `Jellyfin.Plugin.MetadataSync/Fields/FieldRegister.cs` declares which fields
  may move at all, and every other decision is asked after it
- `Jellyfin.Plugin.MetadataSync/Conflicts/ConflictResolver.cs` decides a field
  the two servers disagree about, from a declared rule set and never from a
  default
- `Jellyfin.Plugin.MetadataSync/Reconciliation/Planner.cs` turns two readings of
  one item into a plan, asking the register, the kind group and the rules in
  that order
- `Jellyfin.Plugin.MetadataSync/Reconciliation/Applier.cs` carries a plan to a
  target and accounts for every item in it
- `Jellyfin.Plugin.MetadataSync/Reconciliation/LibraryPlanTarget.cs` is the one
  place a value is written to this server's library
- `Jellyfin.Plugin.MetadataSync/Reconciliation/ItemReader.cs` reads the items of
  the libraries that take part, a page at a time
- `Jellyfin.Plugin.MetadataSync/Matching/CandidateResolver.cs` decides which
  local item a work is, and refuses where more than one of them is
- `Jellyfin.Plugin.MetadataSync/References/ReferenceResolver.cs` decides whether
  an incoming person, studio or genre is one this server already holds
- `Jellyfin.Plugin.MetadataSync/Configuration/ConfigurationValidation.cs` says
  what about a configuration cannot be acted on

<!-- end of the parts -->

What is absent is anything that runs them. Nothing schedules a pass and nothing
starts one:

    git grep -In "IScheduledTask" -- 'Jellyfin.Plugin.MetadataSync/'
    # no output, exit 1

and there is no administrator surface for one to be started from, beyond the
configuration page the server itself renders:

    git grep -In "ControllerBase\|ApiController" -- 'Jellyfin.Plugin.MetadataSync/'
    # no output, exit 1

Nothing reads a peer either. The payload validation this file names below is
owed and is not written, and no pairing package is referenced here, so none of
the parts above is reached by any route an operator can take.

This section used to paste the plugin's whole file list here, on the reasoning
that a reader could see how little there was. The list is no longer short, a
paste of it goes stale on every landing, and it had. What a reader wants is the
command, which answers at the moment they run it:

    git ls-tree -r --name-only origin/master -- Jellyfin.Plugin.MetadataSync/

Every sentence below that describes a defence describes one of two things: a
defence that is in the tree, named with the file it is in, or one that is owed,
said to be owed at the sentence. A policy that reads as a description of a
working system is a claim about code nobody has written, and this file is held
to that by `SecurityPolicyTests` for the part of it a machine can read.

## Reporting

Use this repository's private vulnerability reporting. It is on:

    gh api repos/iderex/jellyfin-plugin-metadata-sync/private-vulnerability-reporting
    {"enabled":true}

Open it from the Security tab of this repository, under Report a vulnerability.
A public issue is the wrong route for anything that would tell a reader how to
make this plugin write where it should not, and once opened it cannot be made
private again.

The form is here, without navigating:

<https://github.com/Flowfin/jellyfin-plugin-metadata-sync/security/advisories/new>

What a report should carry, in whatever form is easiest to write:

- what an attacker gets, stated as an outcome rather than as a category
- the shortest sequence that reaches it
- which version, or which commit, you saw it on
- what you think stops it, if anything, so a disagreement about the fix starts
  in the report rather than three replies later

Reports are read by one person. There is no response time to promise and none
is promised here, which is a statement about the size of this project and not
about how seriously a report is taken. You will get an acknowledgement that
says whether the finding is understood and whether it is accepted, and if a
report goes unanswered, saying so in a public issue without the details is a
reasonable thing to do.

A fix lands as a normal change with a normal issue, and the issue says what was
wrong once the fix is available. Credit is given in the change unless the
reporter asks for it not to be.

## What is in scope

This plugin, at any commit on the default branch, and its packaged artefact.

The findings that matter most here are the ones this plugin's whole design is
arranged against. Each one is a thing the plugin must never do, and each is
worth reporting even if you cannot show a full path to it:

- a field moves that the field register does not permit to move
- a write reaches an item that did not resolve, or resolved ambiguously
- data reaches any destination other than the peer the operator paired
- a field value appears somewhere it should not be, which includes logs, the
  administrator surface, an error message and a crash dump
- a value the operator locked is overwritten, on either server

Two of the five have something to test against today and three do not. A field
moving that the register does not permit is refused in the planner, which asks
the register before anything else and answers a row that does not move with a
disposition rather than a write. A locked value being overwritten is refused
there too, on this server, for every name the server declares as lockable; the
half of that bullet about the other server is not built, because nothing here
reads a peer. The remaining three name a write reaching an unresolved item, a
destination other than the paired peer, and a value appearing where it should
not, and each of those needs a pass, a transport or a surface that does not
exist yet. They are listed anyway, because the list is what the implementation
is written against, and because a reader who arrives before the code should be
able to see what the code is for.

## What is out of scope

Three things sit next to this plugin and are not it.

The pairing plane. Pairing, its transport, its authentication and its
revocation belong to the pairing plugin. This plugin holds no pairing state and
makes no request except through that plane. A finding about how a pairing is
established or how it is trusted goes to the pairing plugin's own repository.

The Jellyfin server. Item storage, the metadata providers, the authentication
of an administrator, and every library call this plugin makes belong to
Jellyfin. Report those to Jellyfin. A finding that this plugin calls one of
those wrongly is in scope here.

The metadata providers. What a provider fetches, and from where, is the
provider's business and the operator's configuration. A finding that provider
data reaches a peer it should not is in scope here, because the reaching is
this plugin's.

## What this plugin does not defend against, deliberately

A policy that implies total coverage is worse than one that draws its own line.

A compromised paired peer can send this plugin whatever it likes, as long as
what it sends fits the contract. There is no judgement about the peer's
honesty anywhere in the design, and there will not be one. What stands between
a hostile peer and this server's library is the field register, which bounds
what may be written at all, and the payload validation in #24, which refuses a
payload naming anything outside it. The register is in the tree and the
validation is owed, so what bounds a hostile payload today is which fields may
move at all and not what a payload is allowed to name. An operator who pairs
with a server they do not control has trusted that server, and this plugin's job
is to make the blast radius of that trust small and stated rather than to
second-guess it.

An administrator on this server is not an adversary here. Every field this
plugin writes is one an administrator can already change by hand through the
server's own interface, so a finding whose only path runs through administrator
access is a finding about the server, not about this plugin. The exception is
an administrator action that reaches further than the server's own would, and
that is in scope.

A person who can read the two libraries can already learn what the sync would
tell them. `docs/personal-data.md` covers what leaves this server and where it
goes.

## The threat model

`docs/threat-model.md` names the adversaries, what each one obtains, and what
stops them, including the risks that stay after the defences are built. It is
the longer half of this file and it is deliberately short.

## What this plugin holds and moves

`docs/personal-data.md` is the same subject from the other side. It says which
of the metadata this plugin moves can be personal data, whose it is, that the
only destination is a peer the operator paired with, what may be logged and
what may never be, and which of those sentences a check holds up rather than a
reader.

## Supported versions

There is no released version:

    gh release list --limit 5      # no output
    git ls-remote --tags origin    # no output

Until there is one, the default branch is the only thing there is to fix, and
no version support window is offered because no version exists to offer one
for.
