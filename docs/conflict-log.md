# The conflict log entry

This plugin's account of itself is a row per decision. An operator who wants to
know why a description did not change, or why one did, reads these rows, and
nothing else in this plugin can answer that question: the plan says what would
happen and is gone when the pass ends, the store says what was written and is
silent about a field where nothing was, and the library shows the result
without the reason.

This page is about the row. The register the rows are kept in, the bound on how
many are kept, the grouping an operator reads them under and the export are the
rest of #48 and are not built. Nothing in this tree holds an entry today.

## What a row carries

The item on this server and the field, so it is clear which value is being
talked about. Both values, so the disagreement is visible. The declared rule
that decided it, or nothing at all where the table ran out. The outcome. The
direction in force, without which a row saying nothing was written cannot be
read. And the moment, on this server's clock, so two rows about one field can
be told apart.

The peer's item is not carried, and that is a decision rather than an omission.
A resolution is true of two libraries as they stood when it was computed and
nothing keeps one past the pass that derived it, which is #33, so a row that
outlived its pass carrying one would be exactly the slot that rule refuses.
What an operator gets instead is the item they can open.

The moment is never compared against anything the peer produced. It is written
down so rows can be ordered against each other on one machine, and it arrives
as an argument rather than being read where the row is built, so one pass
stamps one moment. Nothing establishes that two servers' clocks agree, which is
#46, and this page adds no exception to it.

## Which decisions earn a row

A field that never reached the conflict rules is not a conflict. One the
register does not declare, one it declares as never moving, one outside this
kind of item and one the operator excluded were all settled before the two
values were compared at all, and an account of them is an account of the
register rather than of a disagreement.

A field where both servers already say the same thing is a decision with
nothing to tell. Everything else earns a row, the first pass filling an empty
field included: a value that appeared in a library overnight is the first thing
an operator asks about, and an account that left out the writes would answer
every question except that one.

What the comparison costs is stated rather than left to be found. It is the two
texts as the rules were handed them, so two spellings of nothing - no value on
one side and a single space on the other - are two different texts here and earn
a row the rules answered as agreement. That is one row too many rather than a
decision left out, which is the direction this account fails in.

## What is shown of a value, and what is said about the rest

A metadata field holds whatever somebody put in it, and an overview runs to
paragraphs. A row showing the whole of both sides would be unreadable on the
field it is most needed for. A row showing half a value without saying so would
be worse: two overviews cut to the same length look identical, and an operator
would read agreement into a cut the rules never saw.

So a value is cut for display and the row carries the fact that it was cut,
beside the text rather than inside it. Nothing is appended to the text, because
a marker inside a value cannot be told from a value that ends in one.

<!-- rendered from ShownValue.DisplayBound, read by ConflictEntryTests: edit the source, not this line -->

    A value longer than 200 characters is shown cut to 200, and the row says it was cut.

<!-- end of the rendered line -->

This is a display bound and it is not the payload bound. What a message may
weigh on the wire is the pairing plugin's number, read at run time rather than
compiled in, which is #24.

A value is never repaired on the way in. Whitespace is kept and so is a
character with no glyph, because the rules read both as text somebody can have
typed, and a row that tidied either would explain a decision the resolver did
not make.

## The fixture table

Every row here is a value and what a log row shows of it. The suite reads these
rows out of this document rather than restating them, so a row changed here and
nowhere else turns the suite red.

The value is written as a sequence of pieces. Text in quotes is itself. A
character with no glyph is written as its codepoint, `<U+200B>`, and the reader
builds it. A run of one character is written as `<repeat:A:40000>`, and where
the length is the display bound it is written as `bound`, `bound-1` or `bound+1`
rather than as the number, so this document states that number once, in the
rendered line above, and the cases move with it.

An empty cell in the value column is a field holding nothing. An empty cell
under the characters shown is a row that shows nothing at all, which is a
different state from a row showing an empty text.

| Case | The value | Characters shown | Truncated | The mistake it would catch |
| --- | --- | --- | --- | --- |
| a value well inside the bound | `"A description"` | 13 | no | Cutting every value to the bound, so a log of short fields reads as though every one of them lost something. |
| the field held nothing | | | no | Spelling an absence as an empty text, so a row saying the peer had no value reads the same as one saying the peer had a blank. |
| a value of exactly the bound | `<repeat:A:bound>` | bound | no | Comparing against the bound the wrong way round, which marks a whole value as half of one and sends an operator looking for the rest of it. |
| a value one character longer than the bound | `<repeat:A:bound+1>` | bound | yes | Cutting a value and saying nothing about it, which is the one thing this shape exists against. |
| a very long value | `<repeat:A:40000>` | bound | yes | Putting a whole overview in a row, which is the field a truncation defect appears on first and the reason this case is here rather than among the short ones. |
| a value the bound falls inside a character of | `<repeat:A:bound-1><U+1F600>` | bound-1 | yes | Counting a string in code units and cutting between the two halves of one character, which does not produce a shorter value, it produces one that is no longer text. |
| a value that is only whitespace | `" "` | 1 | no | Trimming a value on the way into the account, so a row explains a decision the rules never made. |
| a value carrying a character with no glyph | `"A"<U+200B>"B"` | 3 | no | Removing what cannot be seen, so the one difference an operator cannot spot for themselves is the one the account hides. |

The last column is why each row is here. A fixture that could not have failed
proves less than one that nearly did, so every row names the mistake somebody
would actually make, in the row rather than in a paragraph beside it, and no two
rows name the same one.

## What is checked here today, and what is not

The rendered line is compared against the number declared in the source, so the
bound is stated once and this page cannot drift from it. Every row above is run:
the value is built from its spelling, handed over, and what comes back is
compared against the two expectation columns. A spelling the reader does not
understand is refused rather than passed through as text, because a malformed
piece read literally is a short value that shows whole, which is a row that
stays green and stops being about anything.

What no check reads is whether the sentence in the last column is true of the
row it sits on. A row could name a mistake it would not catch and every route
here would pass it.

What is not checked at all is anything about a log, because there is no log. No
row above has been through a pass, nothing keeps an entry, nothing bounds a
collection of them and nothing shows one. A green run here says a decision
becomes a row that carries what this page says it carries, and it says nothing
yet about what an operator sees.
