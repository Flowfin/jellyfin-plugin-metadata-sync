# The headless test policy

Every test in this plugin runs with no display, no elevation and no machine
trust store. That is a property of the plan rather than a preference, and it is
written here before the suite grows, because the cheapest moment to refuse a
test that needs a desktop session is before somebody writes it.

## What the suite may not need

**No display.** No test opens a window, drives a browser or needs a session bus.
The configuration page is tested as the file it is, and by asserting the
endpoints behind it, never by rendering it.

**No elevation and no machine trust store.** No test installs a certificate,
reads a machine trust store or needs an administrator. Anything about transport
trust is tested against material the test itself creates in a temporary
directory.

**No running server.** No test starts a real Jellyfin server. The library
surface this plugin uses is reached through the interfaces it already depends
on, and those are substituted in the suite.

**No network.** No test reaches the network. The pairing transport is the test
double the pairing plugin ships, and never a socket.

Those four are the needs a test can have that this policy refuses, and they are
the four a register entry cites by name.

## One rule that is not a refusal

**No ambient clock.** Time is injected, and a test that needs a skew sets one. A
test cannot cite this as a need the way it cites the four above, because there
is nothing to refuse: a test that reads the wall clock is a test written wrong,
not a test that needed something the policy withholds.

## What holds the policy up

A policy nothing reads is a paragraph. The part of this that a machine refuses
is narrow and is named rather than implied.

`HeadlessPolicyTests` in the test project reads the test project's own file and
fails if it carries a package reference outside an allowed set. The allowed set
is in the test rather than in this document, so a package added to reach a
display server, a real server or the network fails the suite instead of passing
review.

`RefusedTestRegisterTests` reads the register beside it and holds it to its own
shape and its own arithmetic: an entry that declares what it needs without
declaring what proves the property instead, and a closing sentence whose count
of entries or of gaps disagrees with the entries above it, both fail the suite.
The counts in that sentence are rendered from the entries and compared, so the
sentence is derived rather than typed.

**A run holds one of the four, and this paragraph said all four were prose.** It
said the two checks above were the whole of the enforcement. The route under
`## Where the suite runs` that starts a container with no network interface has
been in this repository since `32c4f8f9425d7e49a9bd53c71d1d24e5223169bc`, dated
2026-08-08:

    git log --diff-filter=A --format='%H %ad' --date=short -- .github/workflows/headless.yml

and the sentence saying the two checks were all of it was rewritten in
`8f8edcba8064073c011baf9ce4846bad4cb31f57`, dated 2026-08-23, fifteen days after
that, in this file, fifty lines above the section that describes the run. Two
answers in one file is the arrangement where which one a reader gets depends on
where they stopped reading, and this is the file somebody deciding whether to
write a test that needs a socket opens first.

The count is deleted rather than corrected, because the routes are not all the
same kind of thing and the count was what hid it. A check reads a file and
refuses what it finds there. A run refuses nothing: it is a place where a test
that needed what the policy withholds fails.

<!-- what holds any part of this policy up: one per line, the name first, read by TestingStatementTests -->
- `HeadlessPolicyTests` - the package set the test project declares
- `RefusedTestRegisterTests` - the register's own shape and its own arithmetic
- `.github/workflows/headless.yml` - this suite run with no network interface
<!-- end of what holds this policy up -->

The list is read in one direction only, and that is a bound rather than an
oversight. Every name in it has to be in this tree, so a route that is deleted or
renamed reddens this page; which routes hold a policy up is a judgement about
what a check reaches, no reading of this tree makes it, and a fourth route added
tomorrow and left out of the list is refused by nothing.

What none of them reaches is what the policy is mostly about. Nothing reads a
test body and decides whether it opened a window, and no check here refuses one
that does. What the package assertion covers is the most common route by which
such a test arrives, which is a dependency added first. What the register
assertion covers is the accounting and never the argument: an entry naming a
substitute that does not prove the property reads to both checks exactly like one
that does. And what the run establishes is bounded in the same direction: it is a
run in which no test reached the network, never a refusal of a test that would,
and the workflow prints that bound at the end of its own output.

## The register

`refused-tests.md` is the second half. A test that would need any of the four is
not written and not skipped. It is refused in that file, which names what the
test would have proved, which of the four it needed, and what proves the same
property instead.

A refusal with no replacement is a gap. The register makes the gap visible
rather than absent, which is the whole reason it is a file rather than a
decision somebody remembers.

**A refusal with no replacement fails review.** Writing the entry is what makes
the gap arguable; it is not what makes it acceptable. A change that refuses a
test and names `nothing` in its place is sent back unless the pull request says
why the property can go uncovered for now and what would cover it, and the
entry itself carries that sentence. The entries in the register that say
`nothing` were written under this rule and each says what a reader should
assume instead, which is that the property is untested rather than tested
somewhere else. How many of them there are is that register's own count and is
not repeated here: a number restated in a second file is a number that goes
stale in one of them, and this one had.

Nothing refuses this. It is a rule review holds, in the same way as the rest of
this policy except the package assertion above, and it is written here rather
than in the contributing guide because that is where the rest of the headless
policy lives and a second home for half of it is a second answer.

Format, one entry per refused test:

    ## <what the test would have proved>

    Needs: <one of: a display, elevation or a machine trust store, a running
    server, the network>

    Instead: <the test that covers the same property, by name>, or the word
    `nothing`, which makes this entry a gap rather than a substitution.

    <why the substitution covers the property, or what the gap costs>

## Where the suite runs

Two routes, and they are not the same run.

`call / test` is the required check. It runs the suite on `ubuntu-latest`, which
has no display server and does have a network, so it holds the display half of
the policy and nothing at all of the network half.

`Suite with no display and no network` is the second route and it is where the
network half becomes a fact. The suite runs inside a container started with
`--network none`, so the container has no interface but its own loopback and a
test that reached anything outside it fails. The restore and the build run in a
separate step that does have a network, because fetching packages is the
toolchain doing its job rather than the suite reaching out, and the run under
test is `dotnet test --no-build`.

What that proves is bounded and the workflow prints the bound at the end of its
own run: no test in this suite reached the network, and nothing there
establishes that a test which tried to would be caught by anything other than
failing. This is not in the required set today, and what the gate requires is
printed rather than restated here:

    gh api repos/Flowfin/jellyfin-plugin-metadata-sync/rulesets/20464851 --jq '[.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context]'
