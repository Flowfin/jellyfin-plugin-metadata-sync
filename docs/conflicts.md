# The conflict rules

These are the rules that decide what happens when this server and the peer hold
different values for a field that may move. They were written down before the
resolver was, so the fixtures are derived from this document rather than from
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
lands in. The resolver hands back one of the two values it was given and has
nowhere else to get one from, and the suite holds that by reference rather than
by equality, so a value rebuilt out of the two fails even where it compares
equal to one of them.

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

## A character an operator can type and nobody can see

A metadata field holds whatever somebody typed, and some of what can be typed
has no glyph. A zero-width space arrives in an overview pasted from a web page.
It changes the bytes and it shows nothing on either server, so an operator
comparing the two libraries by eye sees two identical descriptions.

That reaches two of the rules above from opposite sides, so each is a row in
the table below rather than a sentence here.

Two values differing only by such a character differ. `values-agree` compares
ordinally and repairs nothing, which is the paragraph above applied to a
character rather than to a space, so the pair is a difference no declared rule
settles and it is refused. A resolver that stripped the character before
comparing would answer that the two agree, keep the local value, and produce no
entry saying it had decided anything.

A value made of nothing but such a character is a value. That is the runtime's
answer rather than a choice taken here: `string.IsNullOrWhiteSpace` reads a
zero-width space as text, because Unicode files it as a format character rather
than as a space. So `peer-value-absent` does not fire on it, the two sides
differ, and the case is refused. Reading it as an absence instead would be this
rule set repairing a value in the case where repairing is easiest to argue for,
which is the case worth refusing it in.

Both answers write nothing. A value nobody can read is a difference for an
operator to look at, and neither library moves while they do.

Two things are worth saying about how far this reaches. The character is named
by its codepoint in the table because `unicode-guard` is a required check here
and refuses these characters in tracked text, on a threat that has nothing to do
with metadata: source that renders differently from how it runs hides logic from
a reviewer. A row carrying the literal would be a row nobody could commit. And
the set these two rows are about is wider than the set that check names. A field
can hold a character no check here has an opinion on, and the answer above is
the same for all of them, because it comes from comparing ordinally rather than
from a list of characters.

## No rule turns on a clock

The obvious rule is that the more recent change wins, and it is the one rule
this plugin does not get to have. Two servers in two households share no time
source that anything here can establish. If one of them is an hour ahead, the
more recent change is always that one's, on every field, for as long as both
keep running, and nothing reports it. The failure is silent, permanent, and
indistinguishable from the plugin working.

The timestamps the server carries would not settle it even between two clocks
that agreed. They record when the item was saved and not when a field was
changed, so an item saved for an unrelated reason looks newer in everything it
holds.

What is used instead is causal rather than temporal.
`local-unchanged-since-this-plugin-wrote-it` knows what this plugin last wrote
for the field, so it knows whether the value here has been touched since it
arrived. That is the question the clock was being reached for, and it is
answered from a record this plugin made rather than from a machine's opinion
about the time. A difference that rule does not settle, and that no other row
settles either, is refused and recorded rather than handed to a weaker rule.

This is not shut forever. The pairing plane injects a clock and fixes a skew
policy of its own, so it may be able to hand a consumer an observed offset and a
bound on it. That is asked for in #26 and is unanswered. If it comes back yes, a
time-based rule becomes arguable, with the bound as an explicit input and a
refusal rather than a decision wherever the bound is exceeded. It would be a new
row here, argued like every other row, and never a default arriving underneath
them.

Two checks hold this, from opposite ends. `ConflictClockTests` reads the
resolver's whole input surface and refuses anything that is not one of the two
shapes a conflict input is made of, so a clock cannot arrive under a name nobody
anticipated or as a count of ticks. The same file reads the conditions in the
declared table and refuses one that fires on which side is more recent. Each is
proved against the rule this plan rejected by name and accepted against the
causal rule that stands in its place. A third guard, the invariant lint, refuses
the spellings a clock arrives under in the plugin's own source text, and it says
in its own record what it cannot catch.

What none of the three reaches is a comparison built out of two values that
arrived as ordinary strings. Nothing in this tree tells a date somebody typed
into a field from any other text, so a rule comparing two of those would pass
every check here. That one is held by this argument and by review.

## When no rule fires

Nothing is written, on either side, and the difference is recorded for an
operator with the field and both values. That is #45, and it is not a rule in
the table above: it is what is left when the table is exhausted, so a rule set
that lost a row fails closed rather than falling through to an implicit answer.

The ordinary case that reaches it is the one the whole plan is careful about.
Both servers hold a value, the two differ, and this plugin did not write either
of them. Two operators have disagreed, and no reading of the two values says
which of them is right.

The resolver arrives here by running out of rules, and it answers with a refusal
that names no rule at all rather than with a rule whose name would have to be
invented. What that answer owes an operator is #45.

## The fixture table

Every row here is a case the rule set has to answer, with the rule that answers
it and the outcome that rule produces. The rules are quoted from the table above
by name, and the suite reads these rows out of this document rather than
restating them in code, so a row changed here and nowhere else turns the suite
red.

A value is written in quotes, and an empty cell is no value at all. The locks
column is one of `none`, `item here`, `field here` or `field on the peer`. An
empty rule cell means no rule fires and the case falls to the refusal above.

A character with no glyph is written as its codepoint, `<U+200B>`, and the
reader builds the character from it before the rule set sees the value. Two
rows need one, and the section above says why the name is there instead of the
character.

| Case | This server | The peer | This plugin last wrote | Locks | Rule | Outcome | The mistake it would catch |
| --- | --- | --- | --- | --- | --- | --- | --- |
| the operator locked the whole item | `"Kept by hand"` | `"From the peer"` | | item here | `item-locked-here` | `KeepLocal` | Reading the item lock after a rule that can write, which makes the coarsest claim the server offers advisory. |
| the item is locked here and the values already agree | `"A description"` | `"A description"` | | item here | `item-locked-here` | `KeepLocal` | Not reading the item lock at all, which this row alone does not show because the rule below it answers the same way. |
| the operator locked this field | `"Kept by hand"` | `"From the peer"` | | field here | `field-locked-here` | `KeepLocal` | Reading only the item lock, so an operator who claimed one field is told it was not a claim. |
| both sides already say the same thing | `"A description"` | `"A description"` | | none | `values-agree` | `KeepLocal` | An early return for equal values, which takes almost every field on almost every pass out of the declared table. |
| neither side has a value | | | | none | `values-agree` | `KeepLocal` | Reading two absences as a difference, which turns every unset field on every item into a refusal. |
| the peer has nothing and this server has text | `"A description"` | | | none | `peer-value-absent` | `KeepLocal` | Treating absence as information, which writes emptiness over a description on the first pass. |
| the peer's value is whitespace only | `"A description"` | `" "` | | none | `peer-value-absent` | `KeepLocal` | Reading whitespace as text, so a provider that found nothing replaces a description with a space. |
| the peer's value is one character with no glyph | `"A description"` | `"<U+200B>"` | | none | | `Refuse` | Reading a format character as whitespace, so a peer value the runtime counts as text is taken for an absence and the row above answers a case it was not written for. |
| the two values differ only by a character with no glyph | `"A description<U+200B>"` | `"A description"` | | none | | `Refuse` | Repairing a value before comparing it, so a difference neither operator can see is answered as agreement and nobody is ever told. |
| the peer has nothing and this server holds what this plugin wrote | `"An older description"` | | `"An older description"` | none | `peer-value-absent` | `KeepLocal` | Reading rule 6 before rule 4, which takes the peer's absence because nobody here has touched the value since it arrived. |
| this server has nothing and the peer has text | | `"From the peer"` | | none | `local-value-absent` | `TakePeer` | A first sync that writes nothing, because an empty field and a disagreement were not told apart. |
| the local value is exactly what this plugin last wrote | `"An older description"` | `"A newer description"` | `"An older description"` | none | `local-unchanged-since-this-plugin-wrote-it` | `TakePeer` | Reaching for a clock to answer whether the value here has been edited since it arrived. |
| the local value differs from what this plugin wrote by one character | `"An older description."` | `"A newer description"` | `"An older description"` | none | | `Refuse` | Comparing what this plugin wrote loosely, so an operator who changed one character is read as not having edited it. |
| the peer holds this field locked and the values differ | `"Kept by hand"` | `"Theirs, and locked"` | | field on the peer | `peer-field-locked` | `Refuse` | Taking the locked value because it cannot move, which makes one operator's lock authority over the other's library. |
| the peer holds this field locked and this server has nothing | | `"Theirs, and locked"` | | field on the peer | `local-value-absent` | `TakePeer` | Reading rule 7 before rule 5, which refuses a field this server holds nothing in. |
| both sides carry a value this plugin never wrote and they differ | `"Ours"` | `"Theirs"` | | none | | `Refuse` | A default underneath the table, answering what no declared rule answered. |

The last column is why each row is here. A fixture that could not have failed
proves less than one that nearly did, so every row names the mistake somebody
would actually make and this table would catch, in the row rather than in a
paragraph beside it. Two of them name each other: reading rule 6 before rule 4,
and reading rule 7 before rule 5, are the two orderings a reader arrives at by
starting from the rule that interests them, and each is caught by one row and
by nothing else.

Two rows are worth reading together. The item lock asserted where nothing would
have been written anyway is the row that says the lock is read first, and it is
the one row whose own answer does not show it, because the rule below it answers
the same way. The one-character difference from what this plugin wrote is the row
that separates an update from a conflict, and a resolver comparing loosely
answers it wrongly while answering its neighbour correctly.

No two rows name the same mistake, and the suite holds that. A line copied from
the row above is the shape a row takes when it was added for the count.

## What each rule is holding up

The table above says a case is answered by a rule. It does not say the rule is
why. A resolver in which one row never fires answers most of these cases the
same way, because another row underneath it produces the same outcome, and a
fixture table alone cannot tell the two apart.

So each rule is also taken out on its own, with the other six left where they
are, and this table declares what the same case answers once it is gone. The
declaration is the point: it is a prediction about the resolver rather than a
description of it, and the suite compares the prediction with what comes back.

An empty rule cell means no rule fires and the case falls to the refusal, the
same spelling the fixture table uses.

| Rule | Proved on | Rule once it is gone | Outcome once it is gone |
| --- | --- | --- | --- |
| `item-locked-here` | the operator locked the whole item | | `Refuse` |
| `field-locked-here` | the operator locked this field | | `Refuse` |
| `values-agree` | both sides already say the same thing | | `Refuse` |
| `peer-value-absent` | the peer has nothing and this server holds what this plugin wrote | `local-unchanged-since-this-plugin-wrote-it` | `TakePeer` |
| `local-value-absent` | this server has nothing and the peer has text | | `Refuse` |
| `local-unchanged-since-this-plugin-wrote-it` | the local value is exactly what this plugin last wrote | | `Refuse` |
| `peer-field-locked` | the peer holds this field locked and the values differ | | `Refuse` |

Two rows are worth reading rather than counting.

`peer-value-absent` is the only rule whose removal writes something. Every other
row here loses a refusal, which is a pass that stops short and says so. Take
this one out and the row below it fires instead, because a local value that is
still exactly what this plugin wrote is a local value nobody here has an opinion
on, and the peer's value is taken. The peer's value is nothing. So the case that
looks like the mildest row in the table, a peer with no value at all, is the one
whose rule stands between a written overview and an empty one, and it is the
reason that row exists rather than an early return.

`peer-field-locked` is the only rule whose removal does not change the outcome.
The case refuses either way. What moves is the name: with the rule, an operator
is told the peer's lock is what stopped the field, and without it they are told
nothing fired. That is the sentence the rule's own reason ends on, and it is
what this row proves, so the rule is held up by what it explains rather than by
what it decides. A reading of this table that counted outcomes would call it
dead.

## What is checked here today, and what is not

The rendered tables are compared to the source, every rule has at least one
fixture, every fixture names a declared rule and a declared outcome, and every
fixture's expected outcome is the outcome its rule declares. A fixture that
names no rule is required to expect a refusal, so the fail-closed floor is held
by the suite rather than by this sentence.

Every fixture also names the mistake it would catch, in its own row, and the
suite refuses a row that names none, one that names the same mistake as another
row, and one holding a rule name where a sentence belongs. What no check reads is
whether the sentence is true of the row it sits on. A row could name a mistake it
would not catch and every route here would pass it, so the column is held by the
same reader who holds the prose.

Every row is also run against the resolver, which landed under #44. The row is
handed over as inputs, and the outcome and the rule name that come back are
compared against the last two columns. The name is compared as well as the
outcome because three rules keep the local value and two take the peer's, so an
outcome on its own would pass with the wrong rule firing, and four of the rows
above exist to tell exactly those apart. The evaluation order is read out of the
declared table on every call rather than written into the resolver, and a
fixture proves it by lifting one row above another and watching the same inputs
answer differently.

Each rule is also taken out on its own, with the other six left in the order
they are declared in, and the answer that comes back is compared against the row
this document declares for it. That is what separates a rule from a rule nobody
needs. Being answered by a rule is not evidence that the rule is why: three rules
keep the local value and two take the peer's, so a row that never fired would
leave its cases to a row underneath it that produces the same outcome, and every
fixture above would stay green. The comparison is on the outcome and the rule
together, because one of the seven refuses either way and is held up by what an
operator is told rather than by what is written.

What that does not reach is a rule that stops being declared. Removing a row
from the rule table removes the obligation to prove it along with it, so the set
that is walked is the set that exists rather than the set anybody intended. That
is a change to the declaration and a reader of the diff is what stands in the way
of it.

The rules are also taken away, which is the only arrangement under which a
default is visible at all. A resolver holding one would answer every row above
correctly, because a declared rule would answer first and the default would sit
underneath all seven of them. So every row, and every combination of the six
values a conflict is decided from, is run against a resolver with nothing to
walk, and each one has to come back refused, naming no rule, holding the value
this server already had. The sweep is over the input surface rather than over
the fields, and those are the same statement here: a rule is handed no field and
no item, so there is nothing it could read that would tell one field from
another.

What is not checked is what happens on a server. Three of the four links are
chained now, and this paragraph said none of them were. The planner asks the
register first and these rules second, and every case above is run through it
rather than only through the resolver. The applier hands a planned row to an
item through the one supported call. And the deferral cases drive deciding and
writing together, which is the pair the window between them belongs to.

What no route constructs is a pass. Nothing reads the peer, nothing turns items
into the observations a plan is made from, and nothing schedules or starts any
of it, so that chain exists in a test and nowhere else, and no field on any real
library has been through it. A decision is still recorded nowhere, in a store
that is built and registered and that nothing writes to. So a green run here says
this rule set decides the cases above as this document writes them, and it says
nothing yet about a library.

Two of the absences above are spellings rather than judgements, and they are
held here rather than left to be read. Each line below is a spelling
`ConflictStatementTests` looks for in the plugin's own sources, and the claim is
that it finds none, so the day a pass turns an item into the observation a plan
is made from, or hands what it decided to the log, the suite reds this page
instead of the page going on saying nothing does. The paragraph above went stale
once already, in the other direction, and what found it was somebody working on
a neighbouring change rather than anybody reading this file.

<!-- the spellings this page says appear nowhere in the plugin's sources: one per line, the spelling first, read by ConflictStatementTests -->

- `new ItemObservation`, the construction that would turn an item into the
  observation a plan is made from
- `ConflictEntries.From`, the call that would turn a decided plan into the rows
  the log keeps

<!-- end of the spellings that appear nowhere -->

That is a negative disclosure and it stays one. What is asserted is the absence
this paragraph states, never that the absence is harmless, and a spelling that
has arrived is a red suite rather than a line quietly deleted from the list.

The list is two lines because the rest of the paragraph is held elsewhere or not
at all, and both belong in writing. That nothing schedules or starts any of it
is already refused by `SecurityPolicyTests` against the absences `SECURITY.md`
rests on, so repeating it here would be a second declaration of one fact, which
is the arrangement where a reader's answer depends on which file they opened and
which this board has already measured the cost of. That nothing reads the peer
is a property of what this repository may reference, which
`SuiteCounterpartyTests` reads from the other end and which no spelling in these
sources would show. That one stays prose, and a reader who takes the fence above
for cover over the whole paragraph is reading it wider than it goes.

THE SECOND LINE IS WHY THIS PARAGRAPH CHANGED, AND WHAT IT SAID BEFORE WAS THAT
A DECISION BEING RECORDED NOWHERE IS A CLAIM ABOUT A STORE THAT DOES NOT EXIST,
SO NO SPELLING COULD NAME IT. The store exists. #48 built it, the entry point
registers it as one instance for the whole server, and `docs/storage.md` is where
what it keeps and what its bound costs are argued. What is absent is not the
place a decision goes but the route that takes one there, and that route has a
name: nothing derives the rows from a plan, so nothing hands the log anything to
keep. A claim about a route is a claim about a call, and a call is a spelling.

The two halves a pass is made of landed in #35 and #39. What would run them is
#36, #38 and #40, the record an entry belongs in is #48, and the refusal a
residual owes an operator is #45.
