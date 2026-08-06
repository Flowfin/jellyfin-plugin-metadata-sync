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

Format, one entry per refused test:

    ## <what the test would have proved>

    Needs: <one of: a display, elevation or a machine trust store, a running
    server, the network>

    Instead: <the test that covers the same property, by name>, or the word
    `nothing`, which makes this entry a gap rather than a substitution.

    <why the substitution covers the property, or what the gap costs>

## Where the suite runs

The suite runs on a container with no display server, which is what makes the
policy checkable rather than aspirational. `call / test` is the required check
that runs it, and it runs on `ubuntu-latest` with no display.
