# The conflict rules

These are the rules that decide what happens when this server and the peer hold
different values for a field that may move. They are written down before the
resolver exists, so the fixtures are derived from this document rather than from
somebody's implementation, and so a rule can be disagreed with without reading
code.

The rules are declared once, in
`Jellyfin.Plugin.MetadataSync/Conflicts/conflict-rules.json`, which ships inside
the assembly. The tables below are rendered from that file and from the closed
outcome set in `ConflictOutcome`, and the suite renders them again and compares
them to what is committed here, character for character. The prose between the
tables is written by hand and nothing checks it, which is the bound worth
knowing: a paragraph contradicting the table under it is caught by a reader and
by nothing else.

## What the rules are written against

The pairing is symmetric and the sync is two-way per pairing, which is decision
1 in #1. Both servers pull from each other, so each server only ever writes its
own library, and this rule set runs on both sides against the same rows. A rule
that would read differently depending on which server is evaluating it is a
defect here rather than a feature.

No server is authoritative, which is decision 3. Nothing in the rule set
resolves a difference by naming a winner, and there is no row that says one side
is right because of which machine it is. Every rule decides on what is known
about the two values.

An undecidable conflict is refused and logged, which is decision 2. That is the
floor underneath this rule set rather than a rule inside it, and it is #45.

## What never reaches a rule

A field with no row in the field register does not move, and asking for it is
refused when it is asked. A field whose row says it does not move is refused the
same way. Neither case is a conflict and neither is decided here: the register
is read first and the rules below only ever see a field that may move. That
boundary is why this document has no row for it.

## The outcomes

The outcome set is closed. Every outcome either keeps a value one of the two
servers already holds or writes nothing at all.

<!-- rendered from the declared rules: outcomes -->
| Outcome | Rules that produce it |
| --- | --- |
| `KeepLocal` | `item-locked-here`, `field-locked-here`, `values-agree`, `peer-value-absent` |
| `TakePeer` | `local-value-absent`, `local-unchanged-since-this-plugin-wrote-it` |
| `Refuse` | `peer-field-locked` |
<!-- end rendered -->

There is no merged outcome, and there is no union outcome for the set-valued
fields either. A merged overview is a sentence neither operator wrote. A union
of two genre sets is a library shape neither operator chose, and it grows on
every pass because a union never removes anything, so two libraries that each
started with a taste converge on the sum of both. The set-valued fields that
move at all are `Tags` and `ProductionLocations`, and both are plain strings the
server builds no entity from, so a union there would still be a value that
arrived from neither side as a whole.

This is also the invariant #44 states in its own words: the resolver never
returns a value that is not one of its inputs. A union row and that invariant
cannot both hold, and the invariant is the one worth keeping, because it is the
only thing that makes an outcome explainable to the operator whose library it
lands in.

## The rules, in evaluation order

A rule's condition is read with every rule above it not having fired. That is
what keeps the conditions short, and it is why the order is part of the
declaration rather than a detail of the resolver: moving a row changes what the
rules below it mean.

<!-- rendered from the declared rules: rules -->
| Order | Rule | When it fires | Outcome | Reason |
| --- | --- | --- | --- | --- |
| 1 | `item-locked-here` | The item carries the server's item-level lock on this server. | `KeepLocal` | The operator has claimed the whole item. It is the coarsest claim the server offers and the only one that covers a field the server has no lock for, so it is read before anything else and nothing below it can reach past it. |
| 2 | `field-locked-here` | The register names a server lock for this field and it is set on the item here. | `KeepLocal` | A lock is one operator's claim on one field, and this plugin arrives from a machine they do not administer. Read after any rule that can write, it would be advisory rather than a lock. |
| 3 | `values-agree` | The two values are equal. | `KeepLocal` | There is nothing to decide. It is a row rather than an early return because it is the outcome of almost every field on almost every pass, and a rule set that leaves it out describes a fraction of what the resolver does while reading as if it described all of it. |
| 4 | `peer-value-absent` | The peer has no value for this field and this server has one. | `KeepLocal` | An absent value is absence of information rather than information. The peer's provider finding nothing is not a statement that the text here is wrong, and a rule set without this row overwrites a written overview with emptiness on the first pass. |
| 5 | `local-value-absent` | This server has no value for this field and the peer has one. | `TakePeer` | Nothing here is lost, because there is nothing here. This is the row that does the work on a first sync, and the only one that writes without needing to know who wrote what before. |
| 6 | `local-unchanged-since-this-plugin-wrote-it` | The values differ, and this server's value is the one this plugin last wrote for this field on this item. | `TakePeer` | Nobody here has expressed an opinion on the value since it arrived, so this is the peer's own value moving on rather than two operators disagreeing. It is the row that separates an update from a conflict, and it is decidable only where #47 recorded what was written. |
| 7 | `peer-field-locked` | The values differ and the peer reports the lock that governs this field as set on their side. | `Refuse` | A lock is a defensive claim on one library and never authority over the other. Taking the locked value because it cannot move would let one operator's lock decide what the other's library says, which is the authoritative server that decision 3 in #1 refused. It is refused under its own name rather than left to the residual, because the operator can be told which side cannot move and the residual cannot tell them that. |
<!-- end rendered -->

## Emptiness is not a value

Two rules turn on it and they point in opposite directions, which is the whole
reason each needs its own row. A value the peer does not have never replaces one
this server has. A value this server does not have is filled from the peer
without anything being weighed.

A value consisting only of whitespace is no value. That is a declaration rather
than an observation about the server: an overview of three spaces is what a
provider writes when it found nothing, and treating it as text would make rule 4
miss the case it exists for.

Emptiness is compared and never repaired. Nothing here trims a value before
comparing it, because a rule that trimmed would be deciding that two values
differing only in trailing space are the same, and that decision belongs to
whoever declares the field rather than to the conflict rules.

## When no rule fires

Nothing is written, on either side, and the difference is recorded for an
operator with the field and both values. That is #45, and it is not a rule in
the table above: it is what is left when the table is exhausted, so a rule set
that lost a row fails closed rather than falling through to an implicit answer.

The ordinary case that reaches it is the one the whole plan is careful about.
Both servers hold a value, the two differ, and this plugin did not write either
of them. Two operators have disagreed, and no reading of the two values says
which of them is right.

## The fixture table

Every row here is a case the rule set has to answer, with the rule that answers
it and the outcome that rule produces. The rules are quoted from the table above
by name, and the suite reads these rows out of this document rather than
restating them in code, so a row changed here and nowhere else turns the suite
red.

A value is written in quotes, and an empty cell is no value at all. The locks
column is one of `none`, `item here`, `field here` or `field on the peer`. An
empty rule cell means no rule fires and the case falls to the refusal above.

| Case | This server | The peer | This plugin last wrote | Locks | Rule | Outcome |
| --- | --- | --- | --- | --- | --- | --- |
| the operator locked the whole item | `"Kept by hand"` | `"From the peer"` | | item here | `item-locked-here` | `KeepLocal` |
| the item is locked here and the values already agree | `"A description"` | `"A description"` | | item here | `item-locked-here` | `KeepLocal` |
| the operator locked this field | `"Kept by hand"` | `"From the peer"` | | field here | `field-locked-here` | `KeepLocal` |
| both sides already say the same thing | `"A description"` | `"A description"` | | none | `values-agree` | `KeepLocal` |
| neither side has a value | | | | none | `values-agree` | `KeepLocal` |
| the peer has nothing and this server has text | `"A description"` | | | none | `peer-value-absent` | `KeepLocal` |
| the peer's value is whitespace only | `"A description"` | `" "` | | none | `peer-value-absent` | `KeepLocal` |
| the peer has nothing and this server holds what this plugin wrote | `"An older description"` | | `"An older description"` | none | `peer-value-absent` | `KeepLocal` |
| this server has nothing and the peer has text | | `"From the peer"` | | none | `local-value-absent` | `TakePeer` |
| the local value is exactly what this plugin last wrote | `"An older description"` | `"A newer description"` | `"An older description"` | none | `local-unchanged-since-this-plugin-wrote-it` | `TakePeer` |
| the local value differs from what this plugin wrote by one character | `"An older description."` | `"A newer description"` | `"An older description"` | none | | `Refuse` |
| the peer holds this field locked and the values differ | `"Kept by hand"` | `"Theirs, and locked"` | | field on the peer | `peer-field-locked` | `Refuse` |
| the peer holds this field locked and this server has nothing | | `"Theirs, and locked"` | | field on the peer | `local-value-absent` | `TakePeer` |
| both sides carry a value this plugin never wrote and they differ | `"Ours"` | `"Theirs"` | | none | | `Refuse` |

Four of those rows are near misses rather than cases. The item lock is asserted
where nothing would have been written anyway, because a resolver that checked
locks last would pass every other lock row. The one-character difference from
what this plugin wrote is the row that separates an update from a conflict, and
a resolver comparing loosely answers it wrongly while answering its neighbour
correctly. The peer's lock with nothing on this side proves the order of rules 5
and 7. The peer having nothing while this server holds what this plugin wrote
proves the order of rules 4 and 6, which is the pair most likely to be swapped
by somebody reading rule 6 first.

## What is checked here today, and what is not

The rendered tables are compared to the source, every rule has at least one
fixture, every fixture names a declared rule and a declared outcome, and every
fixture's expected outcome is the outcome its rule declares. A fixture that
names no rule is required to expect a refusal, so the fail-closed floor is held
by the suite rather than by this sentence.

What is not checked is the only thing that matters in the end: none of these
fixtures is run against a resolver, because there is no resolver. That is #44,
and it is what turns this table from a set of claims into a set of tests. Until
it lands, this document is a design somebody can disagree with and not a
guarantee about behaviour.
