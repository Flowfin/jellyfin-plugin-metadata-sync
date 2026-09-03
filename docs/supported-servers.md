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
| 10.11 | net9.0 | yes | 10.11.0.0 | Jellyfin.Controller 10.11.0 |
| 12.0 | net10.0 | yes | 12.0.0.0 | Jellyfin.Controller 12.0.0-rc4 |

Both rows say yes. A package is built for each line, from a manifest of its own,
and the two are one plugin rather than two: same `guid`, same name, same owner,
same catalogue entry.

## The version band is what tells the two packages apart

A package's `targetAbi` does not choose it. The server uses that field once, as
a floor, and then picks by version - which means an operator on the 12.0 line
would be offered both packages and get whichever the catalogue happened to list
first, if the two carried one version. The reading behind that sentence is the
last section of this page.

So the version carries the server line, in its major:

| Server line | Version band | Manifest |
| --- | --- | --- |
| 10.11 | 1 | build.yaml |
| 12.0 | 2 | build-jf12.yaml |

The band is a major version number, and a release of this plugin is one release
number per line inside its own band: `1.4.0.0` on the 10.11 line is the same work
as `2.4.0.0` on the 12.0 line. A breaking change moves the minor rather than the
major, because the major is spent on the line.

Nothing has been released on either line, and the two manifests sit at different
distances from their bands for that reason. `build.yaml` carries `0.1.0.0`, below
its band, because the first release on the 10.11 line is the 1.0.0.0 that issue
#1 records; `build-jf12.yaml` carries `2.0.0.0`, at the foot of its own. What is
held either way is the property an operator depends on: the 12.0 package's
version is above the 10.11 package's, so a 12.0 server that keeps both entries
takes the one built for it.

`ManifestTests` holds the two manifests against this table, against each other
and against that ordering, so a band edited in one place and not the other
reddens rather than shipping.

## Both lines are compiled here, and both are packaged

The `Package built here` column is about an artefact the build produces. It is
not about whether the source compiles for a line, and the two are separate
answers even now that they agree.

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

The packaging is a second answer on top of that one. The packaging tool reads a
manifest out of `build.yaml` under the sources path it is given and takes no
manifest path, so the two packages are one build workflow with two jobs: the
first packages the committed `build.yaml`, and the second copies
`build-jf12.yaml` over it on the runner before the tool reads it. The committed
file is not edited by either.

What neither job produces is a release. Nothing is published from this
repository yet, on either line, and `docs/RELEASING.md` describes the one route
that would publish and the one manifest it reads. A second release leg is not
written, so what exists per line today is a build artefact rather than something
an operator can install.

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

The plugin is then marked `NotSupported` and no part of it runs.

THIS GATE FIRES ON A VERSION AS WELL AS ON A MISSING TYPE, AND THIS PAGE SAID IT
FIRED ON A MISSING TYPE AND ON NOTHING ELSE. An assembly names the version of
every server assembly it was compiled against, the runtime binds that reference
only to a server assembly at that version or above, and a server carrying a
lower one has, to the runtime, no such file. So the ABI floor a package declares
is a promise the build breaks the moment the packages it compiles against are
newer than the floor: the server admits the package on the ABI and then refuses
every type in it. That was measured rather than read, on the archive the
0.1.0.0 release published, which was compiled against the 10.11.11 packages and
declares `10.11.0.0`. On a 10.11.8 server:

    grep -h -A3 "Failed to load assembly" data-10.11.8/log/*.log | grep "Could not load" | sort -u
    Could not load file or assembly 'MediaBrowser.Common, Version=10.11.11.0, Culture=neutral, PublicKeyToken=null'. The system cannot find the file specified.
    Could not load file or assembly 'MediaBrowser.Controller, Version=10.11.11.0, Culture=neutral, PublicKeyToken=null'. The system cannot find the file specified.
    Could not load file or assembly 'MediaBrowser.Model, Version=10.11.11.0, Culture=neutral, PublicKeyToken=null'. The system cannot find the file specified.

and the same archive on a 10.11.11 server loads and reports `Active`. The row
in the table above compiles the 10.11 package against the line's first release
for exactly this reason, and `ManifestTests` refuses a manifest whose ABI is
below the version its packages bind at, so a package bump that leaves the floor
behind reddens here instead of shipping a package the floor's servers refuse.

What is left of the old sentence is still true and is the bound worth knowing:
an assembly bound at or below the server's version, whose every referenced type
still exists there, passes both gates and runs against a surface it was never
compiled against, and the failure that produces arrives later, at whichever
call first meets a member that moved.

## Which of two packages a server installs, which is why the bands exist

The two gates above are about a package that has already been chosen. This is
the step before them, and it is where two packages under one identity are
decided between. Three readings, identical on both supported lines.

The catalogue's entries are filtered by ABI, and the filter is the same floor
comparison as the manifest gate rather than a match:

    git grep -n 'Version.Parse(x.TargetAbi) <= appVer' v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Updates/InstallationManager.cs
    v10.11.11:Emby.Server.Implementations/Updates/InstallationManager.cs:266:                .Where(x => string.IsNullOrEmpty(x.TargetAbi) || Version.Parse(x.TargetAbi) <= appVer);
    v12.0-rc4:Emby.Server.Implementations/Updates/InstallationManager.cs:269:                .Where(x => string.IsNullOrEmpty(x.TargetAbi) || Version.Parse(x.TargetAbi) <= appVer);

What survives is ordered by version number and by nothing else:

    git grep -n 'OrderByDescending(x => x.VersionNumber)' v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Updates/InstallationManager.cs
    v10.11.11:Emby.Server.Implementations/Updates/InstallationManager.cs:277:            foreach (var v in availableVersions.OrderByDescending(x => x.VersionNumber))
    v12.0-rc4:Emby.Server.Implementations/Updates/InstallationManager.cs:280:            foreach (var v in availableVersions.OrderByDescending(x => x.VersionNumber))

and the version is the whole of what the server holds a package by, both in the
directory it unpacks into and in the plugin it calls already installed:

    git grep -n 'targetDir += "_" + package.Version' v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Updates/InstallationManager.cs
    v10.11.11:Emby.Server.Implementations/Updates/InstallationManager.cs:548:            targetDir += "_" + package.Version;
    v12.0-rc4:Emby.Server.Implementations/Updates/InstallationManager.cs:570:                targetDir += "_" + package.Version;
    git grep -n 'p.Id.Equals(package.Id) && p.Version.Equals(package.Version)' v10.11.11 v12.0-rc4 -- Emby.Server.Implementations/Updates/InstallationManager.cs
    v10.11.11:Emby.Server.Implementations/Updates/InstallationManager.cs:575:            LocalPlugin? plugin = _pluginManager.Plugins.FirstOrDefault(p => p.Id.Equals(package.Id) && p.Version.Equals(package.Version))
    v12.0-rc4:Emby.Server.Implementations/Updates/InstallationManager.cs:618:            LocalPlugin? plugin = _pluginManager.Plugins.FirstOrDefault(p => p.Id.Equals(package.Id) && p.Version.Equals(package.Version))

So a 10.11 server never sees the 12.0 package, because the floor removes it. A
12.0 server sees both, because a floor is only a floor, and it then takes the
higher version. Two packages carrying one version would be one plugin to it, in
one directory, chosen by catalogue order and silently overwriting each other -
which is the failure the band table above exists to make impossible rather than
a risk it reduces.

## What is checked here, and what is not

`SupportedServersTests` holds the table above against the manifests and the
plugin project, so the runtime, the ABI and the server package in a row claiming
a built package cannot drift away from what the build actually produces. Each
built row is matched to the manifest that declares its ABI, so a row cannot pass
by agreeing with the other line's file, and the number of rows claiming a package
is held against the number of manifests: a row added ahead of a manifest offers
an operator a package that is not made, and a manifest added ahead of a row hides
a package that is.

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

Nothing here checks the two sections about the server: the one on the gates a
package meets, and the one on which of two packages a server installs. Both are
a reading of a Jellyfin checkout at the two tags, quoted with the commands that
produced them, and the suite has no server to re-derive them from. A change to
the server's own plugin manager or its installation manager would leave either
section stale and nothing in this repository would notice.

That reaches the band table, and it is the one place on this page where it costs
something. The bands are held against the manifests by the suite, so they cannot
drift from what is built; why they are needed at all rests on the version
selection quoted above, and if that ever stopped being how a server chooses, the
bands would go on being enforced for a reason that had gone.

THIS PAGE SAID NOTHING HAD BEEN INSTALLED ON A SERVER ON EITHER LINE, AND THE
10.11 LINE HAS BEEN WALKED. The archive the 0.1.0.0 release published, and the
assembly built at the change that moved the row above to 10.11.0, were each
copied into `plugins/Metadata Sync_0.1.0.0/` of a fresh data directory of a
server started from the vendor's own build of the line, and the plugin list was
read back after the startup wizard:

    curl -sS -H "Authorization: MediaBrowser Token=\"$TOKEN\"" http://127.0.0.1:$PORT/Plugins

    server 10.11.8,  assembly bound at 10.11.11.0 (the 0.1.0.0 release)  -> Metadata Sync 0.1.0.0 NotSupported
    server 10.11.11, assembly bound at 10.11.11.0 (the 0.1.0.0 release)  -> Metadata Sync 0.1.0.0 Active
    server 10.11.8,  assembly bound at 10.11.0.0  (built at this change)  -> Metadata Sync 0.1.0.0 Active

The three lines are not a table on purpose: the suite finds this page's tables
by their shape, and a third one would be read as a claim about a line.

The server started with no web client and no library, so what the walk
establishes is the load and nothing past it: the assembly is accepted, the
plugin is listed as active, and no pass was run. 10.11.8 is the oldest build of
the line the vendor's file server still carries, so `10.11.0` itself was not
started; what stands between it and 10.11.8 is the binding rule quoted above
and not a measurement.

Nothing was installed on the 12.0 line, and there is nothing released for it.
So what a 12.0 server does with the artefact this repository builds is read from
the server's source and not measured. The sentence that it would be admitted by
the manifest gate rests on the comparison quoted above; whether its types all
survive on that line is not something this document claims either way.
