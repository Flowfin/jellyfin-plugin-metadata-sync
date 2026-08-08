# The register of refused tests

Tests this suite will not carry, why, and what proves the same property
instead. The policy that produces these refusals is in `testing.md`, and the
entry format is defined there.

An entry whose `Instead` is `nothing` is a gap. It stays in this file until
something covers it, because a gap that is written down can be argued with and
a gap that is absent cannot.

## The configuration page loads and renders in a browser

Needs: a display

Instead: `PluginIdentityTests.ConfigurationPageResourceResolvesFromThePluginNamespace`

The failure this would catch is the page the server cannot find. The page path
is built at run time from the plugin type's namespace, so a namespace change
that leaves the embedded resource name behind gives an operator a configuration
page that fails to load with nothing said at build time. The substitute asserts
that the path the plugin computes is a resource the assembly actually carries,
which is the same failure without a browser.

What it does not cover, stated rather than left to be assumed: whether the page
renders correctly once found. That is not covered by anything here, and the
endpoints the page calls are asserted directly instead once they exist.

## A certificate is installed or read from a trust store

Needs: elevation or a machine trust store

Instead: nothing

The failure this would catch is transport trust decided wrongly: a peer
presented with material this plugin should have refused, or refused material it
should have accepted. It is refused because installing a certificate is a
machine-wide change made by a test, on a machine somebody else is using, and
because a test that needs an administrator is a test that will be run with one.

This is a gap and the register says so rather than naming a substitute that does
not exist. What is planned to cover it is the pairing plugin's test double,
which is where transport trust is decided in the first place, and until that
double is in this suite nothing here covers this property at all. A reader
should assume the property is untested rather than tested elsewhere.

## A real server is started and the plugin loaded into it

Needs: a running server

Instead: nothing

The failure this would catch is the plugin that builds, passes every unit test
and then cannot be loaded by the server it was built for, or reaches a library
surface that behaves differently from the interface it was written against.

It is refused because a test that starts a server owns a port, a database and a
directory tree, and because the failure it catches arrives on the first install
either way. The substitutes are the interfaces the plugin already depends on,
substituted in the suite, and those are how everything below the load itself is
reached.

What is left uncovered is the load, and that is a gap rather than a
substitution: nothing in this suite establishes that a real server accepts this
assembly. The manifest assertions cover the claims a package makes about itself
and not the loading of it.

## A socket is opened to a peer

Needs: the network

Instead: `HeadlessPolicyTests.TestProjectReferencesNoPackageOutsideTheAllowedSet`,
and the container run described under `testing.md`

The failure this would catch is a payload this plugin sends or accepts being
wrong on the wire.

The substitute is two-sided and neither side is the test that was refused. The
package assertion refuses the dependency such a test would arrive with, which is
the route rather than the act. The container run puts the whole suite behind
`--network none`, so a test that opened a socket anyway fails there. Together
they cover a test that reaches the network by accident, and neither covers
whether a payload is correct, which is what the pairing test double is for once
it is in this suite.

## Nothing else is refused yet

This register holds four entries and three of them are gaps. That is a fact
about how little of this plugin exists rather than a claim that the policy has
been tested against a hard case, and the number is expected to grow as the suite
reaches the dashboard, the pairing transport and the reconciliation pass.
