# The endpoints this plugin adds, and who may call them

Every endpoint here is an endpoint on somebody's media server, reachable by
anything that can reach the server. The authorization on each one is decided in
the table below, and the suite reads this document and refuses an endpoint that
does not match it.

Everything this plugin exposes is an administrator action. Reading the conflict
log means reading fields out of a library the caller may not have access to.
Reading the unmatched register means learning which items exist on a peer.
Starting a pass means writing to the library. There is no read-only endpoint
here that an ordinary signed-in user should have, and if one is ever proposed,
it is proposed against this table rather than added beside it.

## The table

| Method | Route | Policy | What it exposes |
| --- | --- | --- | --- |

The table has no rows. This plugin adds no endpoint yet, and that is the current
state rather than a placeholder: the dashboard, the conflict log and the pass
are planned and not written.

An empty table is worth having before the first endpoint exists, because the
failure this is against is an endpoint added later with the attribute forgotten.
The suite fails on an action method with no row here, so the first endpoint
cannot arrive without somebody writing down who may call it.

## What a row means

`Method` is the HTTP method the action answers, and `Route` is the path it
answers on, both taken from the attributes on the action rather than written
here twice.

`Policy` is the authorization policy in force on the action, whether it is
declared on the action or on the controller that holds it. The server supplies
the name:

    MediaBrowser.Common.Api.Policies.RequiresElevation

A row with no policy is refused, and so is an action that any caller can reach
without authenticating. Those are the same rule written from both ends, because
an endpoint acquires the second state by having its attribute removed and the
first by having it never added.

`What it exposes` is one sentence naming what a caller learns or changes. It is
prose and nothing checks it, which is stated here rather than left to be
assumed. It is in the table because the reason to read a row is to decide
whether the policy on it is the right one, and a route and a policy name alone
do not answer that.

## What holds it up

`EndpointAuthorizationTests` in the suite. It walks the plugin assembly for
controllers, enumerates their action methods, derives each one's method, route
and effective policy from the attributes, and compares the result against this
table. It fails on an action with no row, on a row naming no action, on a
disagreement about the policy, and on an action reachable without
authentication.

It is a reflection walk rather than a review habit, because review catches the
endpoint somebody wrote and misses the one somebody copied.

The bound is worth knowing. The walk answers what the attributes say and never
what the server does with them: a policy name the server does not have would
pass here and refuse every caller at run time, and a policy the server has but
which grants more than its name suggests is not something any reading of this
tree judges. It also says nothing about what an endpoint does once the caller is
past the policy, which is the second half of #54 and needs endpoints to exist
before it can be written.
