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

Its reach is wider than the rule as stated, and deliberately so. There is no
resolution path in this tree yet, so the scan covers every plugin source rather
than one directory. Wider is the fail-closed direction. A legitimate file-system
read outside the resolution path costs one entry in that rule's allowed set with
the reason written next to it; one inside it costs a red suite.

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

## What is not written here yet

The two-step rule for an item whose identity is its parent plus an ordinal, which
is how most episodes are identified, and the stated outcome for absolute
numbering, for season zero and for a file covering more than one episode. That is
#30, and it is deliberately absent rather than sketched: each of those cases
needs a stated outcome and a fixture row, and a paragraph here that guessed at
one would be read as the answer.

A reader should take episode resolution as undecided rather than as covered
somewhere else.
