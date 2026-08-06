# What this plugin moves, and how much of it is personal data

Most of what a metadata sync carries describes a work rather than a person. A
runtime, a genre, a production year and a studio are facts about a film, and a
statement that stopped there would conclude that this plugin holds no personal
data at all.

That conclusion is wrong in three places, and this file exists because the three
are easy to miss.

## What exists today

Nothing in this plugin moves a field yet. The field register, the reconciliation
pass and the payload validation are planned and not built, so every sentence
below about what moves describes what the design permits to move rather than
what a running plugin does:

    git ls-tree -r --name-only origin/master -- Jellyfin.Plugin.MetadataSync/
    Jellyfin.Plugin.MetadataSync/Configuration/IPluginConfigurationProvider.cs
    Jellyfin.Plugin.MetadataSync/Configuration/PluginConfiguration.cs
    Jellyfin.Plugin.MetadataSync/Configuration/PluginConfigurationProvider.cs
    Jellyfin.Plugin.MetadataSync/Configuration/configPage.html
    Jellyfin.Plugin.MetadataSync/Jellyfin.Plugin.MetadataSync.csproj
    Jellyfin.Plugin.MetadataSync/Plugin.cs
    Jellyfin.Plugin.MetadataSync/PluginServiceRegistrator.cs
    Jellyfin.Plugin.MetadataSync/packages.lock.json

Writing this before the code is deliberate. A statement written after the fact
is a description of whatever was built; written first, it is a bound the build
has to stay inside.

## The three places metadata is personal

**People records carry names.** An actor, a director and a writer are people,
and their names are personal data about them. Some libraries carry more than a
name, because the server's person entity has room for a birth date and a death
date, and a provider that populates them puts them on the item this plugin would
send. None of those people is a user of either server, so neither operator has
any relationship with them through which the data was volunteered. That does not
make it less personal; it makes it data about a third party, which is the harder
case rather than the easier one.

**Any free-text field can contain anything.** An overview, a tagline, a sort
title and a tag are fields an operator can type into, and a field somebody can
type into eventually holds a sentence about a household. "The copy with the
subtitles for Anna" is a note about a person, sitting in a field this plugin
treats as a description of a work. No rule can tell that string apart from a
plot summary, and this file does not pretend one could. What follows from it is
that free text is treated as capable of carrying personal data at every point
where the treatment differs, which is logging, the administrator surface and
anything exported.

**The shape of a library says something about a household.** That a given item
exists here, sent to a peer, tells the peer's operator what this household
holds. Item by item that is unremarkable; across a library it is a profile. It
is the one item on this list that no field-level rule reaches, because it is
carried by the existence of the payload rather than by anything inside it.

## Whose data it is

Three parties, and they have different standing.

The people named in people records, who are not users of either server and did
not choose to be in either library.

The operator of this server, and the members of their household, whose free text
and whose library shape are what the two paragraphs above are about.

The operator of the peer, symmetrically, for anything this plugin writes here
that arrived from there.

Users of either server, as users, are not on this list. Watch state, favourites,
play counts and anything else scoped to a user account are the clearest personal
data in a media server, and none of it is this plugin's to move. A field scoped
to a user is refused rather than synced, which is issue #18, and watch history
belongs to a sibling plugin with its own statement.

## Where it goes

To one place: the peer server that the same operator paired with this one. There
is no telemetry, no analytics, no crash reporting, no update ping carrying
anything but a version, and no third party of any kind.

The mechanism that is meant to make any other destination unreachable is that
this plugin holds no transport of its own. Every request it makes is meant to go
through the pairing plane, which is the pairing plugin's contract, and this
plugin is meant to carry no HTTP client, no socket and no URL of its own through
which a second destination could be named.

That is a design statement and not yet a mechanism, and the difference matters
enough to be measured rather than asserted. What is true of the tree today is
that the plugin declares five package references, two of them the server's own
assemblies and three of them analysers that do not ship in the artefact:

    git show origin/master:Jellyfin.Plugin.MetadataSync/Jellyfin.Plugin.MetadataSync.csproj | grep -n 'PackageReference Include'
    11:    <PackageReference Include="Jellyfin.Controller" Version="10.9.11" >
    14:    <PackageReference Include="Jellyfin.Model" Version="10.9.11">
    20:    <PackageReference Include="SerilogAnalyzer" Version="0.15.0" PrivateAssets="All" />
    21:    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556" PrivateAssets="All" />
    22:    <PackageReference Include="SmartAnalyzers.MultithreadingAnalyzer" Version="1.1.31" PrivateAssets="All" />

None of them is a transport, and nothing refuses a sixth that is. A check that
fails when a transport type becomes reachable from a reconciliation path is one
of the invariants issue #79 is meant to seed, and until it lands the sentence
above this one is held by review. The runtime the plugin already sits on offers
an HTTP client without any package reference at all, so the list above is
evidence about intent and not a boundary.

The payload validation in issue #24 is the other half: it bounds what may be in
a payload at all, which is what keeps the destination question from being the
only one.

## What is logged, and what may never be logged

The plugin's log is read by an operator debugging a pass, and it is also the
part of a media server most likely to be pasted into a public issue. So the
default level carries the shape of what happened and never the content.

What may be logged at the default level: how many items a pass looked at, how
many it wrote, how many it refused, which reason a refusal carried, which field
NAME was involved, which item identifier was involved, and which pairing.

What may never be logged at the default level: the value of any field, on either
side of a comparison. That includes the value being written, the value that was
there before, the peer's value, and the value in a conflict entry. It includes
free text first of all, and it includes a person's name.

Below the default level, at debug, a field value may be logged, because an
operator who has turned on debug logging to diagnose a specific item has made
that choice for their own server. Debug output is not what gets pasted into an
issue by somebody who has not read it.

### What holds that up

Two things, and they cover different halves.

The first is already in the tree. `CA2254` is promoted to an error in
`jellyfin.ruleset`, with the reason written at it, and it refuses a logging
call whose message template is an interpolated string. That closes the route
where the value is baked into the message text, at every level, everywhere in
the repository.

    git grep -n 'Rule Id="CA2254"' -- jellyfin.ruleset
    jellyfin.ruleset:100:        <Rule Id="CA2254" Action="Error" />

What CA2254 permits is the other route: a constant template with a placeholder
in it and the value passed beside it. `_logger.LogInformation("Wrote overview
{Overview}", item.Overview)` is a static template and is exactly the leak.

So the second thing is `DefaultLevelLoggingTests` in the test project, which
reads every C# source file in the plugin and judges each logging call at
Information, Warning, Error or Critical by the names in its template. Structured
logging means every value arrives under a name the author chose, `CA1727` is
also an error here so that name is PascalCase, and a name is a thing a token
scan can decide. What an expression returns is not.

The allowed names are a small set held in the test rather than in this document,
so that this document cannot become the authority and drift away from what the
suite refuses. They are counts, identifiers, field names and outcomes.
`FieldName` is in the set and `FieldValue` is deliberately not, and those two
are the near-miss the fixtures are built on, because they differ by the one word
somebody will get wrong. A call passing more arguments than its template names
is refused too, since a value with no placeholder still reaches the log event.

The guard's reach is narrow and is stated rather than implied.

It reads source text with a token scan rather than a parser, so it judges
spelling. A call whose template is held in a variable is refused as unjudgeable
rather than passed, which is the direction that fails closed. A call assembled
through some future indirection the scan does not recognise is not caught at
all.

It does not reach the exception argument. `LogError(ex, "...")` is allowed,
because refusing it would mean shipping error logs with no diagnosis in them,
and an exception thrown by a library call can carry the value that caused it in
its own message. That is a residual route by which a field value can reach a
default-level log line, it is open, and nothing in the suite closes it.

It judges the name and not the thing behind it. A call naming `{FieldName}` and
passing a value is not caught, because nothing here reads what
`field.Name` returns. The set is a floor under the mistake somebody makes by
accident, not a defence against somebody choosing to mislabel a value.

It says nothing about the administrator surface, the conflict log or an export.
Those are three more places a value appears, they are governed by the issues
that build them, and none of them is covered here.

It currently sweeps a plugin with no logging call in it at all:

    git grep -c "LogInformation\|LogWarning\|LogError\|LogCritical\|LogDebug\|LogTrace" origin/master -- 'Jellyfin.Plugin.MetadataSync/*.cs'
    # no output, exit 1

So the sweep half of that test passes today over an empty set and proves nothing
about this tree. What proves the guard is the fixture pairs next to it, each
handing the same function a call that must be refused and a neighbour differing
by one thing that must not be, and the fact that inserting a real leak into the
plugin turns the sweep red. Both are shown in the change that added this file.

## Retention

Nothing here yet, and the shape is owed by the issues that build the stores.

The record of what was written, issue #47, holds a previous field value per
field per item, which is the largest concentration of field content this plugin
will hold, and it is bounded there rather than here. The unmatched register,
issue #29, holds one row per unresolved item and is updated in place. The
conflict log, issue #48, holds both values of a difference. Issue #61 is where
an operator asks what is held for one pairing and asks for it to be gone.

When those land, the bounds they state belong in this file as well, and a
version of this file that describes stores which hold values without saying how
long they hold them is incomplete.

## Where this sits

`SECURITY.md` is the reporting route and the scope. `docs/threat-model.md` names
the adversaries. This file is neither: it is about what the plugin holds and
moves when everything is working correctly, which is the question a threat model
does not answer.
