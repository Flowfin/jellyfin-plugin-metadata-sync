# Releasing

A release is published by pushing a tag. Nothing is created by hand.

## The tag

The tag has the form `X.Y.Z-stable` or `X.Y.Z.W-stable`, for example `1.4.0-stable`
or `0.1.0.0-stable`. The numeric part is the plugin version that Jellyfin installs,
and it must be exactly the `version` in `build.yaml`, written the same way, with the
same number of parts. The `-stable` suffix lives only in the tag and in the release
name.

## This route publishes one of the two packages

The build produces one package per supported server line, from a manifest each:
`build.yaml` for the 10.11 line and `build-jf12.yaml` for the 12.0 line. This
route reads `build.yaml` and nothing else, so what it releases is the 10.11
package. The 12.0 package exists as a build artefact and has no release leg.

That is a gap rather than a decision that the newer line is not published, and it
is named here so a reader does not take a green release run as having shipped
both. `docs/supported-servers.md` is where the two packages and their manifests
are described.

## What a version number means once the two lines are packaged

`docs/supported-servers.md` gives each server line a version band, which is a
major version number, and the reason is in that document: a server filters a
catalogue by target ABI as a floor and then takes the highest version, so an
operator on the newer line is offered both packages and the version is the only
thing that separates them.

So a major version number here says which server line a package is for, on top of
what a release number ordinarily says. The section at the foot of this page
insists that a shipped version number keeps its meaning, and this does not spend
that: no number that has been released is redefined, because nothing has been
released on either line. What it does is fix what the numbers mean from the first
release onward, which is the only moment at which that is still free.

The cost, stated rather than left to be met: a breaking change moves the minor
rather than the major, because the major is spent on the line. `docs/changelog.md`
is where a change's class is declared, and the class is what a reader acts on;
the major is not.

## The release note is written from the issues

Every change on this board starts as an issue that says what is wrong, what the
evidence is and what done means, so the entry for it is a sentence out of that
issue rather than a reading of the diff. A note written from the diff describes
what moved in the tree, which `git log` already does better, and it gives the
change an operator has to act on the same voice and the same size as the one
they will never notice.

`docs/changelog.md` holds the classes an entry carries and an example of each.
Three of them exist to mark the changes an operator has to read before
upgrading, because those arrive spelled as a fix as often as not.

## Cutting a release

1. Update `version` in `build.yaml` on the release branch and merge it.
2. Check that the commit you want to release is on that branch.
3. Push the tag for that commit:

    ```
    git tag 1.4.0-stable <commit>
    git push origin 1.4.0-stable
    ```

The `Publish Release` workflow takes it from there.

Push one tag at a time and wait for its run to finish. GitHub keeps at most one
queued run per concurrency group, and although the group here is keyed on the tag,
serialising them by hand is what keeps the release order readable.

## What the run produces

The workflow builds the plugin from the tagged commit, creates the GitHub release
for the tag, and attaches six files:

- the plugin archive
- the packaging metadata written beside it, `<archive>.zip.meta.json`
- one `.md5` file, the checksum of the archive
- one `.sha256` file for the same archive
- `sbom.cyclonedx.json`, the inventory of what the plugin is built from
- `sbom.cyclonedx.json.sha256` for that inventory

The `.md5` is the value a Jellyfin catalog serves as the plugin checksum. There is
exactly one per release so that no generator can pair a checksum with the wrong
file. The inventory therefore carries a `.sha256` and never an `.md5`. The archive,
the metadata and the inventory are each checked for existence by name before the
release job writes anything, so a release with five of the six files is not a state
this route can reach.

The inventory is a CycloneDX document listing the packages the plugin is compiled
against, generated from the dependency graph `packages.lock.json` fixes rather than
from the project file read by hand. It answers what is inside the thing an operator
installs, which is a question a release should not need its author to answer.

It is generated in its own job, which holds neither the signing scopes nor write
access. The generator is a tool fetched at run time, so it is code this repository
did not review, and the job it runs in can reach the restore graph and nothing else.
That is why the inventory is shipped as a checksummed asset and is not itself
attested.

The run also signs a build provenance statement for the archive, in a separate job
that downloads the archive and runs no build tooling. A downloaded archive can be
checked against it:

```
gh attestation verify <archive>.zip --repo <owner>/<repository>
```

Nothing here writes a plugin catalog. A GitHub release is the whole output, and
the path through the Jellyfin meta plugins workflow is gone. Both halves of that
are unchanged.

THIS PARAGRAPH SAID NO CATALOG IS FED UNTIL A MANIFEST GENERATOR IS ADDED, AND
ONE EXISTS. It is not in this tree and does not need to be. `Flowfin/hub` builds
the served catalogue out of the releases of the repositories it declares as
sources, and this repository is one of them, declared and enabled before it had
tagged anything so that the catalogue grows on the day it first publishes:

```
gh api repos/Flowfin/hub/contents/sources/metadata-sync.json --jq '.content'   | base64 -d | grep -E '"repository"|"enabled"'
    "repository": "jellyfin-plugin-metadata-sync",
    "enabled": true,
```

So a release made by the steps above is what feeds the catalogue, and nothing
further is owed here for it. The sentence that was here would have had a reader
of this page conclude that a release changes nothing outside this repository,
which is the opposite of what happens.

What this tree still has no route to is the catalogue's own state. Whether the
served file caught up with a release is read where the catalogue is built rather
than here, and a green publish run in this repository is not evidence that it
did.

## What fails the run

- The tag does not end in `-stable`, or the workflow was started from something
  other than a tag.
- The numeric part of the tag differs from `version` in `build.yaml`.
- `build.yaml` is missing a required field, or `version`, `targetAbi`, `framework`
  or `guid` has the wrong shape.
- `framework` in `build.yaml` names a target the plugin project is not built for.
- A packaging manifest that shadows `build.yaml` is present, such as `jprm.yaml` or
  `meta.yaml`.
- `build.yaml` declares an `image` file that is not in the repository.
- The tagged commit is not contained in a release branch, or the tag was moved after
  the run started.
- There is no `packages.lock.json` next to the plugin project, so the release build
  cannot restore against a reviewed dependency graph. Create one with
  `dotnet restore <project> -p:RestorePackagesWithLockFile=true` and commit it.
- The version stamped into the assembly is not the version in `build.yaml`.
- The build produced no archive, or more than one, or no packaging metadata.
- The inventory generator wrote nothing, or wrote a document listing no components.
- The inventory did not reach the release job.
- A release already exists for the tag.

All of these fail before anything is published.

## What the run notes without failing

The packaging tool warns when `build.yaml` declares neither `image` nor `imageUrl`.
The plugin then shows without a logo in a catalog. That is a warning on every run
until a logo exists, and it is not a reason to hold a release.

## Re-running

A release that exists is not touched again. The release job asks whether a release
exists for the tag before it writes anything and stops if one does, and the upload
step is configured not to replace an asset of the same name. Replacing the bytes of a
version people have already installed is the failure this prevents, and it is worth
more than the convenience of a re-run.

So: if a release went out with the wrong contents, fix the problem, raise the version
in `build.yaml`, and push a new tag.

If a run failed **before** the release was created, the tag is still clean. Fix the
cause and re-run the workflow from the Actions page, or delete and re-push the tag.

If a run failed **after** the release was created but before every asset was attached,
the release is incomplete and a re-run will refuse it. What is possible then depends
on the repository settings below. Without immutable releases you can delete the
incomplete release, delete the tag, and push it again. With immutable releases you
cannot, and the version has to be raised.

## A shipped version number keeps its meaning

The section above fixes the bytes of a release. This one fixes what the number
means, which is the other half and the one nothing can enforce after the fact.

Two artefacts stay on an installation after the plugin is upgraded: the plugin
configuration, and the plugin's own store. Both are stamped with the version that
wrote them, and an upgrade steps them forward one released version at a time. A
step is written against the shape the number had on the day it shipped.

So once a version has been released, what that number says about those two
artefacts is settled. Redefining it later does not change the step that reads it.
It leaves the step in place and makes it wrong, on every installation still
holding an artefact written under the old meaning, and the result is a file that
looks migrated and is not. Nothing detects that: the version matches, the step
ran, and the values are quietly the wrong ones.

A shape that has shipped is therefore superseded by a new number and never edited
into an old one. Where a released version turns out to describe the wrong thing,
the repair is the next number plus a step from the one that was wrong, the same
answer this document gives for a release that went out with the wrong contents.

Both artefacts carry a stamp now, and this sentence said the configuration
carried none. What neither stamp is yet is a released version: nothing has been
released, so the numbers say which shape a file is in rather than which release
wrote it, and this section binds the first release rather than describing the
current tree. The two coincide until that release and the reading has to be
re-made then.

The step chain exists on the store's side now, and this paragraph said it did
not. What that changes is smaller than it sounds and worth reading exactly: the
chain is empty, nothing runs it, and what it refuses is a build whose steps
cannot reach its own current format rather than a number whose meaning somebody
redefined. Nothing here or anywhere else refuses that redefinition, because no
reading of a tree separates a shape that was corrected from one that was
replaced, and the artefacts a wrong step would damage are on installations this
repository cannot see. So this section stays a rule carried by whoever raises
the number. #59 is where the mechanism is built and `docs/storage.md` is where
the two artefacts and the split between them are argued.

## Repository settings this expects

- Default workflow permissions set to read only.
- A rule that restricts who may push `*-stable` tags.
- The `ABI floor build` check required on the release branches.
- Immutable releases, if the repository wants the guarantee that a published release
  can never be edited or deleted at all. The workflow does not depend on it: the
  refusal to touch an existing release is enforced in the release job. Turning it on
  removes the only recovery path for an incomplete release, so try it on one
  repository and cut a release there before turning it on everywhere.
