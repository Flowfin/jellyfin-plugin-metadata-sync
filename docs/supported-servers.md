# Which servers this plugin runs on

Two server lines are supported. They need different runtimes, and the runtime
is not a detail an operator can work around: it is the framework the assembly
was compiled for.

    git show v10.11.11:MediaBrowser.Controller/MediaBrowser.Controller.csproj | grep TargetFramework
        <TargetFramework>net9.0</TargetFramework>
    git show v12.0-rc4:MediaBrowser.Controller/MediaBrowser.Controller.csproj | grep TargetFramework
        <TargetFramework>net10.0</TargetFramework>

| Server line | Runtime | Package built here | Target ABI | Package compiled against |
| --- | --- | --- | --- | --- |
| 10.11 | net9.0 | yes | 10.11.0.0 | Jellyfin.Controller 10.11.11 |
| 12.0 | net10.0 | no | none | nothing |

One row says yes. This repository packages a single artefact, for the first of
the two lines, and the second line has nothing to install. That is the state of
the build rather than a narrowing of what is supported, and issue #9 is where
the second package is held.

## Both lines are compiled here, and only one is packaged

The column above is about a package an operator can install. It is not about
whether the source compiles for a line, and those two are now different
answers.

The plugin project and the suite target one framework per line, and each target
references that line's server packages:

    git grep -n "TargetFrameworks" -- '*.csproj'

So every commit is compiled against both server surfaces, and the whole suite
runs twice, once per line. A member that exists on one line and not on the
other is a build failure here rather than a plugin the other line marks as not
supported after somebody installs it.

That is worth the cost it carries, which is that the second line is a release
candidate and the reference to it is a prerelease. The alternative was reading
the newer server's source and believing the reading, which is how the guard
below came to name a member the 12.0 line does not have.

What it does not give an operator is anything to install. The manifest declares
one `framework` and one `targetAbi`, so one package is produced, and the second
package with its own manifest entry is the half of #9 that is still open.

The first thing compiling the second line found was in this repository rather
than in the server. `ItemDeletionTests` refuses the naming of the library's
item removal members, and one of the five names it holds,
`MediaBrowser.Controller.Persistence.IItemRepository.DeleteItem`, is not
declared on the 12.0 line:

    git grep -c "DeleteItem" v10.11.11 v12.0-rc4 -- MediaBrowser.Controller/Persistence/IItemRepository.cs
    v10.11.11:...:1
    v12.0-rc4:...:0

The leg that asks whether a guard's vocabulary is real had only ever asked one
server, so a name that had gone stale for one line read exactly like one that
bites on both.

## What happens on a line this plugin was not built for

Two gates stand between a package and a running plugin, they answer different
questions, and the first is weaker than its name suggests.

The manifest gate is a floor and not a range. The server parses the `targetAbi`
the manifest declares and compares its own version against it in one direction
only, identically on both lines:

    git grep -n "_appVersion >= targetAbi" v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Plugins/PluginManager.cs
    v10.11.11:Emby.Server.Implementations/Plugins/PluginManager.cs:702:                    return new LocalPlugin(dir, _appVersion >= targetAbi, manifest);
    v12.0-rc4:Emby.Server.Implementations/Plugins/PluginManager.cs:703:                    return new LocalPlugin(dir, _appVersion >= targetAbi, manifest);

So a server older than the declared ABI refuses the plugin before any assembly
is opened, and says so once in its log:

    git grep -n "Skipping disabled plugin" v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Plugins/PluginManager.cs

A server newer than the declared ABI does not refuse it. An artefact declaring
`10.11.0.0` is admitted by a server on the 12.0 line, because 12.0 is greater
than 10.11, and nothing in the manifest expresses an upper bound for it to fail.
An operator reading `targetAbi` as the line the package is for will read it
wrongly in exactly that direction.

The second gate is the one that catches a mismatch, and it catches it by
failing rather than by checking. After the assemblies load, the server asks each
one for its types, and a type the plugin references that this server does not
carry is where it stops:

    git grep -n "references an incompatible version of one of the shared libraries" v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Plugins/PluginManager.cs

The plugin is then marked `NotSupported` and no part of it runs. The bound worth
knowing is that this gate fires on a missing type and on nothing else. An
assembly built against one server line whose every referenced type still exists
on the other passes both gates and runs against a surface it was never compiled
against, and the failure that produces arrives later, at whichever call first
meets a member that moved.

## What is checked here, and what is not

`SupportedServersTests` holds the table above against the manifest and the
plugin project, so the runtime, the ABI and the server package in the row
claiming a built package cannot drift away from what the build actually
produces. It also refuses a second row claiming one, because the manifest
declares a single `targetAbi` and a document saying otherwise would be offering
an operator a package that is not made.

Every row's runtime is held against the frameworks the project builds, in both
directions, so a line the build stops compiling cannot stay in this table and a
line the build starts compiling cannot be left out of it. Each row's server
packages are held against the line the row is about, which is what refuses a
conditional reference wired to the wrong framework: the newer line compiled
against the older server passes every other leg here, because each cell still
agrees with the file it was copied from.

One source is compiled for both lines, and that is now refused rather than
described. `InvariantLintTests` carries `no-server-line-named-in-a-plugin-source`,
which refuses `#if`, `#elif`, `NET9_0` and `NET10_0` in every plugin source, so a
rule branching on a server line reddens instead of compiling green on both
targets. That is the failure worth naming here: an unconditional line-specific
type breaks one of the two builds and cannot arrive quietly, and a conditional
one builds twice and leaves this plugin resolving items one way on one line and
another way on the other with nothing saying so.

The rule's own record says what it cannot see, and three of the four matter to a
reader of this page. A compile item conditioned per target in the project file
swaps a whole file and is not a `.cs` source, so it passes. A difference taken at
run time rather than at compile time spells none of those tokens. And the suite
is outside the subject: `ItemDeletionTests` carries two conditional blocks,
because one of the item removal names it holds is not declared on the 12.0 line,
and nothing judges those. What the rule does not do at all is the other half of
the first condition of #9, which is that a call differing between the lines sits
behind one interface with an implementation per line. No such call exists here
yet, so there is no seam and no allowance for one; the day there is, the seam is
the rule's first allowance and the interface is what makes it legitimate.

Nothing here checks the two paragraphs above. They are a reading of a Jellyfin
checkout at the two tags, quoted with the commands that produced them, and the
suite has no server to re-derive them from. A change to the server's own plugin
manager would leave this section stale and nothing in this repository would
notice.

I did not install this plugin on a server on either line. There is no release
to install:

    gh release list --repo Flowfin/jellyfin-plugin-metadata-sync --limit 3
    # no output

So what a 12.0 server does with the artefact this repository builds is read from
the server's source and not measured. The sentence that it would be admitted by
the manifest gate rests on the comparison quoted above; whether its types all
survive on that line is not something this document claims either way.
