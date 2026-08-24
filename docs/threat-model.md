# The threat model

Three adversaries are worth naming for a plugin that writes to a library on the
strength of data from another machine. This document says what each one
obtains, what stops them, and what is left over once the thing that stops them
is working.

It is short on purpose. A threat model that names every conceivable actor is a
document nobody checks a change against.

Most of the defences named here are owed rather than built. Where one is in the
tree the entry names the file it is in, and where one is not the entry says so
and names the issue that owes it, so a reader can tell a control from a plan.
`ThreatModelTests` holds that separation to the tree for the part of it a
machine can read, and says at itself what it cannot reach. `SECURITY.md`
carries the same disclosure and the reporting route.

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

What stops it. Four of the five decisions that would stand between this
adversary and a write are in the tree, and each one is named below with the file
it is in, so a reader can open the thing rather than take this document's word
for it. The fifth is owed and says so at the entry.

<!-- the defences of this adversary: one per line, the state first, then the file or the issue, read by ThreatModelTests -->

- in the tree, `Jellyfin.Plugin.MetadataSync/Fields/FieldRegister.cs` declares
  which fields may move at all, so a field it holds no row for has nothing
  saying what a wrong value there would cost
- in the tree, `Jellyfin.Plugin.MetadataSync/Reconciliation/Planner.cs` answers
  a field with no row, a field the register declares as not moving, and a field
  outside the kind group of the item, before any rule is asked
- in the tree, `Jellyfin.Plugin.MetadataSync/Matching/CandidateResolver.cs`
  answers that a work did not resolve where nothing offered is that work and
  where more than one local item is, so a peer claiming an item is a different
  item meets a refusal rather than a guess
- in the tree, `Jellyfin.Plugin.MetadataSync/Conflicts/ConflictResolver.cs`
  refuses a field the operator locked, on either side, from a declared rule
  rather than from a default
- owed, #24, payload validation, which would refuse a payload naming a field
  outside the register, refuse one above the size bound before parsing it, and
  refuse one whose purpose is not this plugin's

<!-- end of the defences -->

Nothing reaches any of the four, and that is the part to read before taking the
list for a working defence. Nothing consumes a payload:

    git grep -In "Payload" -- 'Jellyfin.Plugin.MetadataSync/'
    # no output, exit 1

so there is no route by which this adversary's data arrives at any of them, and
nothing reads a peer for it to arrive from. The four are decisions a pass would
make, reached from the suite and from nowhere else. The honest statement about
this adversary today is still that nothing stops it, and what has changed is
that the decisions it would meet are code somebody can read rather than four
issue numbers.

What is left over once they do. A peer sending plausible wrong values inside
the register, on items that resolve correctly, is doing exactly what a
legitimate peer does. No mechanism in this plugin distinguishes the two, and
none is planned. What limits the damage is that the register is small and that
what was overwritten is recorded, so an operator can see it and undo it. That
recording is #47, and it is the reason provenance is written at the moment a
field is written rather than added later.

What a conflict does when no declared rule fires is settled, and the answer is
to refuse. The declared rules are walked in order, and where the table runs out
nothing is written on either side and the difference belongs to an operator:

    git grep -In "ConflictOutcome.Refuse, rule: null" -- 'Jellyfin.Plugin.MetadataSync/Conflicts/ConflictResolver.cs'

Decision 2 in #1 is where that was taken and #45 is the issue that carries it.
What holds it up is `ConflictFloorTests`, which runs every case
`docs/conflicts.md` argues with the rules taken away and asserts that each one
refuses, so a default answer added under the table reddens rather than quietly
making every rule above it advisory.

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
