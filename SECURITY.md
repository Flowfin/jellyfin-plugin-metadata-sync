# Security policy

This plugin writes to somebody's library on the strength of data that arrived
from another machine. That is the whole reason this file says more than where
to send a report.

## What exists today

Nothing in this plugin moves a field yet. The reconciliation pass, the field
register, the payload validation and the administrator surface are planned and
not built, and every sentence below that describes a defence describes one that
is owed rather than one that is running. Where a defence is not yet in the tree
this file says so at the sentence, because a policy that reads as a description
of a working system is a claim about code nobody has written.

    git ls-tree -r --name-only origin/master -- Jellyfin.Plugin.MetadataSync/
    Jellyfin.Plugin.MetadataSync/Configuration/IPluginConfigurationProvider.cs
    Jellyfin.Plugin.MetadataSync/Configuration/PluginConfiguration.cs
    Jellyfin.Plugin.MetadataSync/Configuration/PluginConfigurationProvider.cs
    Jellyfin.Plugin.MetadataSync/Configuration/configPage.html
    Jellyfin.Plugin.MetadataSync/Jellyfin.Plugin.MetadataSync.csproj
    Jellyfin.Plugin.MetadataSync/Plugin.cs
    Jellyfin.Plugin.MetadataSync/PluginServiceRegistrator.cs
    Jellyfin.Plugin.MetadataSync/packages.lock.json

That is a plugin identity, a configuration object and a service registration.
There is no sync in it.

## Reporting

Use this repository's private vulnerability reporting. It is on:

    gh api repos/iderex/jellyfin-plugin-metadata-sync/private-vulnerability-reporting
    {"enabled":true}

Open it from the Security tab of this repository, under Report a vulnerability.
A public issue is the wrong route for anything that would tell a reader how to
make this plugin write where it should not, and once opened it cannot be made
private again.

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

None of those has an implementation to test yet. They are listed now because
the list is what the implementation is written against, and because a reader
who arrives before the code should be able to see what the code is for.

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
payload naming anything outside it. Both are owed and neither is built. An
operator who pairs with a server they do not control has trusted that server,
and this plugin's job is to make the blast radius of that trust small and
stated rather than to second-guess it.

An administrator on this server is not an adversary here. Every field this
plugin writes is one an administrator can already change by hand through the
server's own interface, so a finding whose only path runs through administrator
access is a finding about the server, not about this plugin. The exception is
an administrator action that reaches further than the server's own would, and
that is in scope.

A person who can read the two libraries can already learn what the sync would
tell them. The privacy statement this file will point at once it exists covers
what leaves this server and where it goes.

## The threat model

`docs/threat-model.md` names the adversaries, what each one obtains, and what
stops them, including the risks that stay after the defences are built. It is
the longer half of this file and it is deliberately short.

## Supported versions

There is no released version:

    gh release list --limit 5      # no output
    git ls-remote --tags origin    # no output

Until there is one, the default branch is the only thing there is to fix, and
no version support window is offered because no version exists to offer one
for.
