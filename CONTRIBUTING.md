# Contributing

## Every change starts as an issue

An issue says what is wrong, what the evidence is, and what done means. Where
the evidence is a number, it carries the command that produced it. The change
then lands as a pull request that names the issue, so somebody who finds the
code later can find the reasoning that produced it.

## Where a rule is written

A rule that governs one file is written in that file, at the top, where
somebody editing it meets it before they change anything. A rule that governs
how this plan is made is written here, because there is no file for it to sit
next to.

That is a routing decision and not a preference. A rule written in two places
drifts, and the copy a contributor happens to read is then the one that is
wrong. So this guide restates no rule that already has a home, and it lists
neither the checks nor the workflows: what runs is a property of the tree and
of the ruleset, both of which can be printed, and a list here would be a third
answer that agrees with neither.

Three rules have no file to sit next to. They are the next three sections.
Two more were routed elsewhere, and the section after those says where and
why, because a decision that is not written down is indistinguishable from
one nobody made.

## A change too large to read is re-planned rather than excepted

A pull request that will not fit under the size the hygiene gate states is an
issue whose scope was planned wrong, and the first response is to divide that
issue into sub-issues, each with its own reason to exist and its own
definition of done.

Carving a finished diff into two pull requests is not the same thing. Neither
half is reviewable alone, so the size number is satisfied and the reason for
it is not. Re-planning gives each piece a scope before the work exists, which
is the only point at which the size of the work is still a choice.

## An invariant added to this plan owes a rule that refuses it

Several rules in this plan are a pattern a lint can refuse in a second: no
file-system property in the resolution path, no transport type reachable from
a reconciliation path, no static instance outside the entry point, no second
contract version literal, no timestamp from one server compared against the
other's, no direction comparison outside the direction type.

An issue that declares another invariant of that shape, and adds no rule to
the lint that refuses it, is caught in review and sent back. The rule and the
invariant land together, because an invariant with nothing refusing it is a
sentence, and a sentence nobody can run stops being true without anybody
noticing.

A lint rule also says what it does not catch. A token pattern matches a
spelling and never an intent, so the rule's record names the invariant it
enforces, the issue that declared it, and the shape it would miss.

## A code scanning finding is fixed, or dismissed with its reason recorded

An alert raised by the code analysis is closed one of two ways. Either the
code changes so the alert no longer fires, or the alert is dismissed and the
dismissal carries the reason, written where the dismissal itself is visible
rather than in a pull request body that the next reader of the alert will not
have open.

An alert left open is neither of those. It is the state this rule exists to
make uncomfortable, because a scanning surface that accumulates open alerts
stops being read at all, and then a real finding arrives into a list nobody
opens.

This rule is here rather than in the workflow that runs the analysis, because
what it governs is what a person does after a finding exists. The workflow
decides what is scanned and with which token scopes, and it says so in its own
header. Neither half is the other.

## The two rules that were routed elsewhere

The headless test policy, and what a refused test owes. `docs/testing.md`
holds the policy and defines the register entry format, and it already states
what a refusal with no replacement is. Restating any of that here would give a
contributor two copies of a policy whose whole point is that it is decided in
one place before the suite grows.

What a new or edited workflow owes before it merges. The header of
`.github/workflows/zizmor.yml` holds it, next to the audit that refuses the
violation, where somebody editing a workflow file is already reading. A
contributing guide is not where that person is looking.

Both of those sentences were asked for here by the issues that raised them.
Both are better where they are, and this section is the record of that
decision so neither issue is left waiting on a file that was decided against.

## What holds any of this up

The size rule has a mechanism, and it does not refuse. `.github/workflows/pr-hygiene.yml`
counts the change and says so on the pull request when it is over the number,
which that file states next to the command that prints where the number came
from. It annotates rather than refuses on purpose: no reading of a diff tells a
scope that was planned badly from one that is a single readable thing, and a
gate that reds the second teaches people to ignore it on the first. So the
sentence above about re-planning is still held by review.

The same workflow does refuse three things about a change, and each is written
at the check in the file rather than restated here. Whether one of its refusals
holds a merge is a property of the ruleset, printed below and not written down
anywhere.

Nothing in this repository refuses a violation of the other two rules above.
Each is held by review, which is a person and not a mechanism, and each names
the issue where the mechanism is argued: the invariant rule is issue #79 and
the triage rule is issue #80. Until those land, those two sections are prose,
and this paragraph is here so that neither is read as something the gate would
stop.

## What the gate requires

Printed rather than written down here, because a list in this file drifts
against the thing it describes:

    gh api repos/iderex/jellyfin-plugin-metadata-sync/rulesets \
      --jq '.[] | select(.name == "gate") | .id'
    gh api repos/iderex/jellyfin-plugin-metadata-sync/rulesets/<id> \
      --jq '[.rules[] | select(.type == "required_status_checks")
             | .parameters.required_status_checks[].context]'
