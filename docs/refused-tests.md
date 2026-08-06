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

## Nothing else is refused yet

This register holds one entry. That is a fact about how little of this plugin
exists rather than a claim that the policy has been tested against a hard case,
and the number is expected to grow as the suite reaches the dashboard, the
pairing transport and the reconciliation pass.
