# Which way metadata moves

Direction decides whether a change here reaches the peer, whether a change there
reaches here, or both. It is one value read in one place, because a direction
check written in four places will disagree with itself in one of them, and the
one it disagrees in is the one nobody tests.

## The model

Direction belongs to the pairing. Not to a field, not to a library, not to a
server.

    Jellyfin.Plugin.MetadataSync/Configuration/SyncDirection.cs

The type has one member, `TwoWay`, and both servers pull from each other under
one rule set. That is the answer rather than the start of a list. A second
member is a second conflict model, so adding one is a decision somebody argues
for and not a line appended to an enum.

It is an enum with one member rather than nothing at all because a configuration
read off disk can carry a number this plugin has never declared, and the
validator has to be able to say so by name. A property that cannot hold a wrong
value also cannot report one.

## Why it belongs to the pairing

Two servers that agreed to pair agreed to one relationship, and the operator on
each side configures their own. A direction per field would let one field move
one way and its neighbour the other, which is four states per field to reason
about in a conflict log an operator is reading because something already
surprised them.

There is no direction column in the field register, and nothing there has to
change to keep it that way. A column per field is what a per-field model would
have needed, that model was not taken, and the register landed without one.

## What the register decides instead

Whether a field may move at all is the register's answer, and it is a different
question from which way. `docs/field-register.md` holds the first. This document
holds the second. A field that never moves is not a field with a direction, and
a field the register allows moves under whatever direction the pairing carries.

An operator narrows that further by excluding fields in the plugin
configuration, which is a subtraction from what the register allows rather than
a direction of its own.

## What changing a direction does to fields already synced

Nothing, and today the question cannot arise. One member means there is no
second value to change to, so no configuration edit can put a pairing into a
direction it was not already in.

The answer is written here anyway, because the first thing a second member would
need is this paragraph. A field that already moved stays where it is. What
arrived is library data on the receiving server the moment it lands, and this
plugin does not walk back a write because a later configuration says it would
not make that write again. The one act that does reach back into what already
moved is a revocation, which is a different act with its own answer in
`docs/lifecycle.md` and its own bound: nothing is reverted that this plugin
cannot prove it wrote.

## Direction and the conflict log

An entry saying a field was not written is unreadable without knowing whether
that field was ever going to be written in that direction, so every entry
carries the direction in force when it was decided. That is a condition of the
conflict log rather than of this document, and it is named here because the two
are read together.

## What holds this up

Nothing yet, and the absence is the point of saying so here.

`ConfigurationValidationTests` refuses a configuration whose direction is not a
declared member, which is the validator half. What has no mechanism is the rule
that a direction comparison appears in one place only: there is no planner and
no applier in this tree for a comparison to appear outside of, so a lint for it
today would pass because its subject is absent. That guard lands with the first
code that reads a direction, and #34 carries both halves.
