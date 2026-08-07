# The threat model

Three adversaries are worth naming for a plugin that writes to a library on the
strength of data from another machine. This document says what each one
obtains, what stops them, and what is left over once the thing that stops them
is working.

It is short on purpose. A threat model that names every conceivable actor is a
document nobody checks a change against.

The defences named here are almost all owed rather than built. Where a defence
does not exist yet the entry says so, and the issue that owes it is named, so a
reader can tell a control from a plan. `SECURITY.md` carries the same
disclosure and the reporting route.

## What is being protected

The library on this server. Specifically, the metadata values an operator
either wrote themselves or let a provider write, and the fact of which items
that library contains.

Not the media files. This plugin reads and writes metadata through the server's
own library calls and carries no file transfer of any kind, which is a
permanent non-goal rather than a feature that has not arrived. That is what
`build.yaml` states in the description an operator reads before installing.

## Adversary: a paired peer that has been taken over

The operator paired two servers by hand. Some time later the other one is
running code its operator did not choose.

What it obtains. Everything the contract lets a peer send, which means values
for every field the register permits to move, on every item that resolves. It
can send values chosen to be wrong, values chosen to be offensive, or values
chosen to be enormous. It can also answer resolution questions dishonestly and
claim that an item is a different item.

What stops it. The field register bounds what may be written at all, so a field
outside the register is not writable no matter what arrives. Payload validation
refuses a payload naming a field outside the register, refuses one above the
size bound before parsing it, and refuses one whose purpose is not this
plugin's. Item resolution refuses to write to an item that did not resolve or
that resolved to more than one candidate. A field the operator locked is not
written even when the payload is otherwise valid.

None of those exists in the tree today. The register is #12, the validation is
#24, the resolution refusals are #29 and #31, and the lock refusal is #13.
Until they land, the honest statement is that nothing stops this adversary,
because nothing consumes a payload at all yet.

What is left over once they do. A peer sending plausible wrong values inside
the register, on items that resolve correctly, is doing exactly what a
legitimate peer does. No mechanism in this plugin distinguishes the two, and
none is planned. What limits the damage is that the register is small and that
what was overwritten is recorded, so an operator can see it and undo it. That
recording is #47, and it is the reason provenance is written at the moment a
field is written rather than added later.

What a conflict does when no declared rule fires is not settled. Refusing is
one of three answers under consideration and it is the fail-closed one, which
is why the paragraph above does not lean on it. Decision 2 in #1 is where the
answer will be written, and #45 is the issue that carries whichever answer it
gets. Until then this document claims nothing about it.

Also left over: the fact that an item exists in this library, which is
disclosed to the peer by the act of resolving it. That is inherent in syncing
at all, and an operator who does not want it should not pair.

## Adversary: another plugin on this server that has been taken over

Jellyfin plugins share a process. A second plugin running hostile code is
inside this plugin's address space, and no boundary this plugin can build
holds against that.

What it obtains. Everything. It can call this plugin's services directly, read
its configuration, and reach the pairing plane on its own account.

What stops it. Nothing here, and this is stated rather than softened. Plugin
isolation is the server's problem and this plugin cannot solve it from inside
the same process.

What this plugin does instead is narrower and worth naming for what it is. It
declares one purpose and registers for that purpose only, so a payload sent
under another sync plugin's purpose is not delivered here and is not inspected
here. That is separation of routing between two plugins that are both behaving,
which is a real property, and it is not a defence against a plugin that has
been taken over. #24 owes the purpose declaration and the grep that fails if a
second purpose literal appears.

The residual risk is the whole of this section. An operator's control is which
plugins they install.

## Adversary: a caller reaching the endpoints without authorization

The administrator surface will have endpoints that start a pass, apply a plan,
resolve a conflict and remove what a pairing left behind. Those write.

What it obtains. If an endpoint is reachable without authorization, a caller
can start writes across the library, or read a plan that lists what the library
contains.

What stops it. Every endpoint carries the server's own authorization
requirement, and the endpoint authorization table is asserted by reflection so
an endpoint added without one fails the suite rather than review. That is #54,
and it is the shape that matters: a table somebody maintains by hand drifts,
and a test that enumerates the endpoints does not. Every action that writes
also confirms with a number and records who asked, which is #57.

Neither exists yet, because there are no endpoints yet.

What is left over. Reflection asserts that an attribute is present. Whether the
server enforces that attribute correctly is the server's property and not this
plugin's, and this plugin asserts nothing about it. An administrator who is
authorized can start any of these actions, which is the intended behaviour and
is why the confirmation and the record of who asked exist.

## The residual risks, gathered

These stay after everything above is built. They are listed together because a
risk stated once inside a long section is a risk a reader skips.

The write window. This plugin does not hold a lock across another component's
write, because there is no such lock to take. A pass re-reads an item
immediately before applying and cancels that item if the last-saved timestamp
moved, which narrows the window between the read and the write. It does not
close it. A provider writing inside that window still wins, and the operator
sees a field that changed after a sync. #41 carries the mechanism and states
the same thing in the reconciliation document.

A peer inside the contract. Covered above. A hostile peer that stays inside the
register and the size bound is indistinguishable from an honest one.

Anything typed into a free-text field. An operator can put anything in an
overview, including something about a person, and this plugin moves overviews.
The privacy statement in #17 is where that is written down for an operator to
read before they decide.

Provenance is bounded by when it started. Nothing this plugin can undo predates
the first pass that recorded provenance. A value that was already wrong when
the plugin was installed is not this plugin's to restore, and an operator
expecting undo to reach further will be disappointed.

The metadata this plugin never sees. Watch history, user data and anything else
scoped to a user is refused here, which means this plugin is not what protects
it. #18 carries the refusal.

## What this document is not

It is not a claim that the defences named here are correct, and not a claim
that they are present. Most are not present. A change that adds one of them is
where its correctness is argued, and this document is updated when that change
lands rather than in advance of it.
