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

That is the whole of the enforcement. The four refusals above are otherwise
prose: nothing reads a test body and decides whether it opened a window, and no
check here refuses one that does. What the assertion covers is the most common
route by which such a test arrives, which is a dependency added first.

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
entry itself carries that sentence. The three entries in the register that say
`nothing` today were written under this rule and each says what a reader should
assume instead, which is that the property is untested rather than tested
somewhere else.

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
