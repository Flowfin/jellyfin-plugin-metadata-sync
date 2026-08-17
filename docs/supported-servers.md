# Which servers this plugin runs on

Two server lines are supported. They need different runtimes, and the runtime
is not a detail an operator can work around: it is the framework the assembly
was compiled for.

    git show v10.11.11:MediaBrowser.Controller/MediaBrowser.Controller.csproj | grep TargetFramework
        <TargetFramework>net9.0</TargetFramework>
    git show v12.0-rc4:MediaBrowser.Controller/MediaBrowser.Controller.csproj | grep TargetFramework
        <TargetFramework>net10.0</TargetFramework>

| Server line | Runtime | Artefact built here | Target ABI | Compiled against |
| --- | --- | --- | --- | --- |
| 10.11 | net9.0 | yes | 10.11.0.0 | Jellyfin.Controller 10.11.11 |
| 12.0 | net10.0 | no | none | nothing |

One row says yes. This repository builds a single artefact, for the first of
the two lines, and the second line has nothing to install. That is the state of
the build rather than a narrowing of what is supported, and issue #9 is where
the second artefact is held.

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
claiming a built artefact cannot drift away from what the build actually
produces. It also refuses a second row claiming one, because the manifest
declares a single `targetAbi` and a document saying otherwise would be offering
an operator a package that is not made.

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
