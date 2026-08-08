# Gate parity

The target for this repository's gate is the one on `iderex/jellyfin-plugin-sso`.
That repository is public and its workflows are readable, so the comparison is a
thing anybody can re-run rather than a thing to be remembered.

This document holds one row per workflow on either side. Where the two sides
differ, the row carries the reason, in both directions: a workflow the target has
and this repository does not, and a workflow this repository has and the target
does not. An unexplained gap is a defect. An explained one is a decision, and it
stays explained when somebody asks about it later.

## What was compared, and against what

Compared on 2026-08-06.

The target was read at commit `54873c4920a88812f65f3a7884c5830e2ce31fb5`. That
commit was `main` when the comparison was made and it has already stopped being
`main`, so the command that reproduces what was read names the commit and not the
branch:

    gh api repos/iderex/jellyfin-plugin-sso/commits/54873c4920a88812f65f3a7884c5830e2ce31fb5 --jq '.commit.committer.date'
    2026-08-06T14:13:06Z

Where `main` has moved on since, this is what to compare that commit against:

    gh api repos/iderex/jellyfin-plugin-sso/commits/main --jq '.sha'

This repository was read at commit `7e9b50346a0379d883956d302422fbc01ec1e606`.

The workflow lists both tables are built from. The first is pinned to the commit
above, so it prints what the table was built from rather than what is on the
target today:

    gh api 'repos/iderex/jellyfin-plugin-sso/contents/.github/workflows?ref=54873c4920a88812f65f3a7884c5830e2ce31fb5' --jq '.[].name'
    git ls-files .github/workflows

## The required sets are printed, not restated

Neither required set is copied into this document. A list here would drift
against the ruleset it describes, and the ruleset is the authority. Print them:

    gh api repos/iderex/jellyfin-plugin-sso/rules/branches/main --jq '.[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context' | wc -l
    gh api repos/iderex/jellyfin-plugin-metadata-sync/rulesets/20464851 --jq '[.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context] | length'

On 2026-08-06 the first printed 13 and the second printed 3. Both numbers move,
in both directions, so re-run the commands rather than quoting these. The gap
between them is what this milestone closes or explains, and every row below that
says adopt is a step across it.

A workflow existing in the tree and a workflow being in the required set are
different states, and several rows below are in the first and not the second.
Only the ruleset says which, which is why the count is printed and not written
out.

## One row per workflow on the target

| Workflow on the target | State here | Reasoning |
| --- | --- | --- |
| `build.yml` (Build) | Present, different shape | The target carries its own package and SBOM steps. Here `build.yaml` calls the shared plugin workflow instead, which is a smaller surface in this tree and a dependency on somebody else's workflow file. That trade is the row. |
| `codeql.yml` (CodeQL) | Present, not required | `scan-codeql.yaml` runs here and calls the shared workflow. Putting it in the required set alongside the package inventory and build provenance is #80. |
| `dco.yml` (DCO) | Present, and now stricter than the target | Arrived with the template and the audit set, carrying a comparison that built both sides from the same commit, so a commit whose author address was not an address agreed with itself and passed. #120 fixed that here. The target still carries the original line, which the difference section below prints. |
| `dependency-review.yml` | Present | Arrived with the template and the audit set. No difference to explain. |
| `dotnet.yml` (.NET) | Partly present | This workflow carries the target's `build`, `ABI floor build` and `Package (JPRM)` jobs. Here `build.yaml` and `test.yaml` call the shared workflows for the first. There is no ABI floor build, because a floor needs the supported server lines fixed first, which is #9. |
| `e2e-login.yml` (E2E Login Harness) | Not adopted | This plugin has no login path, and a workflow that tests nothing is a green check that means nothing. The condition that changes the answer is this plugin gaining an interactive authentication path of its own, which the plan does not give it. |
| `fuzz.yml` (Fuzz, SharpFuzz) | Not adopted for 1.0 | The parse surface here is one payload shape, against two security protocols on the target. The condition that changes the answer is the payload gaining a format this plugin parses by hand rather than through a library. |
| `manifest-freshness.yml` | Not adopted yet | The target asserts its beta manifest lists the newest release per generation. This repository has no published channel to assert against yet. #88 makes the plugin installable and proves the channel is fresh, and this row is part of it. |
| `nightly-betas.yml` | Not adopted yet | A nightly beta channel presumes a decided set of published server lines. Decision 4 in #1 fixes how many there are, and the shape of this workflow follows that answer. |
| `opengrep.yml` (Repo Invariant Lint) | Not adopted yet | Adopted by #79, which also seeds the invariants from this plan rather than copying the target's. |
| `pr-hygiene.yml` (PR Hygiene) | Not adopted yet | Adopted by #78. This plan is made of many small issues, so a change that lands without naming its issue detaches the work from the reasoning that produced it. |
| `prettier.yml` (Prettier Lint) | Not adopted yet, and this row was written wrong once | See the correction below. This repository has files inside the target's glob, so the formatter has something to format here today. The count is printed there rather than here. |
| `publish-beta.yml` | Not adopted yet | A beta channel, waiting on the same decision 4 in #1 as the nightly one. |
| `publish-jf12-beta.yml` | Not adopted yet | A per-generation beta channel. Same condition: decision 4 in #1 fixes how many generations are published. |
| `publish-jf12-stable.yml` | Not adopted yet | A per-generation stable channel. Same condition as the row above. |
| `publish-failure-alert.yml` | Not adopted yet | The target sweeps for any workflow that concluded non-success on its default branch. Useful only once there is a publication that can fail unattended, so the condition is the same decision 4 in #1 that creates the channels. |
| `publish.yml` (Publish Release) | Present, different shape | `publish.yaml` here calls the shared plugin publish workflow rather than carrying its own steps. Same trade as the build row. |
| `regenerate-manifest.yml` | Not adopted yet | Manual regeneration of a published manifest, which presumes a published manifest. Held by #88 with the freshness row. |
| `scorecard.yml` (Scorecard supply-chain security) | Present | Arrived with the audit set. No difference to explain. |
| `stryker-mutation.yml` (Stryker mutation testing) | Not adopted for 1.0 | There is no decision code here to mutate yet, so a run would score the plugin skeleton and report nothing about the rules this repository cares about. The section below carries the reasoning and the condition that changes the answer. |
| `unicode-guard.yml` | Present, required | Already in this repository's required set. No difference to explain. |
| `wiki-lint.yml` (Wiki Lint) | Not adopted | This plugin has no wiki. The condition that changes the answer is a wiki being created for it. |
| `zizmor.yml` (Workflow Security Analysis) | Present, not required | The audit runs here and reports. Putting it in the required set is #77. |

## The prettier row, corrected

This row was proposed as a deviation on the grounds that this repository has no
files a formatter could act on. That was wrong, and it is written here rather
than quietly fixed, because the reason a row says no is the whole value of the
row.

The target lints `**/*.{js,html,md,css,scss}`:

    gh api repos/iderex/jellyfin-plugin-sso/contents/.github/workflows/prettier.yml --jq '.content' | base64 -d | grep prettier_options

This repository has eight tracked files inside that glob:

    git ls-files | grep -Eic '\.(js|html|md|css|scss)$'
    8

That number includes this document, which is the reason it was first written
here as seven: the count was taken before the file holding it was added, which is
a measurement of the tree the writer had rather than the tree the reader gets.
Run it against the commit under review.

So the condition the row named as unmet is already met, and the honest state of
this row is that the formatter is not adopted yet and has no issue on this board
adopting it. That is a gap this document records rather than closes. The count
moves as documents are added, so re-run it.

## The mutation testing answer

Not adopted for 1.0. This section is the recorded answer rather than a note
that the question is still open, because an unexplained absence is the thing
this document exists to remove.

The reasoning is one line. The case for adopting the tool is that the decision
code in this plan is pure by construction and its rules are exactly the kind a
test can pass without really checking, and at the commit under review that
decision code does not exist:

    git ls-files -- 'Jellyfin.Plugin.MetadataSync/*.cs' | wc -l
    5

Those five files are a plugin entry point, a configuration holder, a
configuration provider with its interface, and a service registration. A
mutation run over them would score a skeleton. The score would be a number
nobody would act on, and a repository that adopts the tool and then works
nothing from its output has bought a report, which is the case against.

The condition that changes the answer sits here next to it. Ask the question
again once the pure planner asked for in #35 and the conflict resolver asked
for in #44 are both in the tree and carry rules. Those two are the decision
code the case for adoption is about, and until they exist the case cannot be
tested against anything. The count above moves as the plugin grows, so re-run
it rather than quoting this number.

What the shape would be if the answer became yes is written in #81 and is not
copied here, because a second copy of it would drift against the issue that
decides it.

## The sign-off row, where this repository is now ahead

The rows above mostly describe things the target has and this repository does
not. This one goes the other way, and it is written down for the same reason:
a difference nobody explained is indistinguishable from a mistake.

Both sign-off gates build the line they look for out of the commit they are
looking at, so the check asks whether the commit agrees with itself and the
answer is yes whatever the author address is. The target still carries that
line, at a named commit rather than at a branch that moves:

    gh api 'repos/iderex/jellyfin-plugin-sso/contents/.github/workflows/dco.yml?ref=e9cee021e95763e5240b44b8d7af16598df609ce' --jq '.content' | base64 -d | grep -c 'expected="Signed-off-by: ${author_name} <${author_email}>"'
    1

Here the same line is preceded by a check that the author address, and every
sign-off and co-author address in the message, is syntactically an address.
#120 has the measurement and the evidence.

Nothing here says the target is wrong to be where it is. It says the two files
differ, and why, so the next person comparing them does not read the difference
as drift. The target is a separate board and this document does not open work
on it.

## One row per workflow here that the target does not have

| Workflow here | Reasoning |
| --- | --- |
| `changelog.yaml` | Came in with the plugin template. It drafts a release and opens a version bump pull request. #84 gives this repository a changelog with a class per entry, and what that leaves this workflow doing is decided there. |
| `command-dispatch.yaml` | Came in with the plugin template. It turns a comment into a workflow run, which is a write surface the target does not carry. Nothing on this board has argued for keeping it. |
| `command-rebase.yaml` | Came in with the plugin template, and is the one command the dispatcher above routes to. It stands or falls with that row. |
| `sync-labels.yaml` | Came in with the plugin template pointed at the upstream label file, where it deleted this board's own labels. That defect was #11 and it is closed: the workflow now syncs from this repository's own `.github/labels.yaml`. The workflow stays, and the row is here because the target has no equivalent. |

## What this document does not say

It does not say the gap is closed. On the comparison date the target required
more checks than this repository did, and most of the rows above that say adopt
are open issues rather than landed workflows.

It does not judge whether the target's own set is right. The target is the
yardstick this repository was measured against, and a row saying a workflow is
present on both sides is a statement about presence and not about quality.

It does not read either ruleset for anything except the two counts printed
above. Which checks are required, on either side, is what the commands print.
