# The field register

This is the set of fields this plugin may move between two paired servers, and
it is the only place a field is declared. A field with no row here does not
move, and asking the plugin to move one is refused when it is asked rather than
noticed afterwards.

The register is written for the operator who has to decide whether to let this
run against their library. It is not a summary of the implementation. The
implementation reads the same rows: they are declared once, in
`Jellyfin.Plugin.MetadataSync/Fields/field-register.json`, which ships inside
the assembly, and this document is checked against that file by the suite. If
the two disagree, the suite is red and neither is quietly right.

## How to read a row

**Moves** is the whole question. `no` means the value never crosses between
servers, whatever the configuration says, and the reason column is where that is
argued.

**Class** is what a wrong value costs, and it is a different question from
whether the field moves. Descriptive means a wrong value mislabels one item.
Structural means a wrong value changes how the server organises the library, or
what a restricted account is allowed to see. Both classes contain fields that
move; the class is what an operator reads before deciding to apply a pass rather
than only plan one.

**From the file** is the class that never moves, and the reason is the same for
every row in it. The value describes the media file this server holds, and the
other server holds a different file. Taking the peer's runtime describes the
peer's copy and is simply a false statement about ours. That is why the column
exists next to the class rather than inside it: a field can be descriptive,
harmless to get wrong, and still be one that must never move.

**Item kinds** names a group rather than listing kinds in every row, and the
groups are written out below.

## The rows

| Field | Item kinds | Moves | Class | From the file | Reason |
| --- | --- | --- | --- | --- | --- |
| `Name` | all | yes | Descriptive | no | The title an operator reads first and corrects by hand on whichever server they were looking at, which is the case this plugin exists for. |
| `ForcedSortName` | all | no | Structural | no | The stored override behind the sort name, usually empty so the server derives ordering itself. Moving one server's sorting conventions into the other's lists changes the shape of a library nobody asked to reshape. |
| `Overview` | all | yes | Descriptive | no | Free text about the item and not about either copy of it, so the same value is right on both servers. |
| `Tagline` | video | yes | Descriptive | no | Free text of the same kind as the overview, carried on the video kinds that display it. |
| `Genres` | all | no | Descriptive | no | A genre the receiving server has never seen becomes a genre entity there, which is growth of that library rather than a value on one item. Whether that may happen is decision 5 in #1 and is held by #15, and a row that moved this field before the answer exists would settle it. |
| `Tags` | all | yes | Descriptive | no | Tags are free strings held on the item and the server builds no entity from them, which is what separates this row from genres and studios. |
| `Studios` | video | no | Descriptive | no | A studio the receiving server has never seen becomes a studio entity there. Same condition as genres: decision 5 in #1, held by #15. |
| `ProductionLocations` | video | yes | Descriptive | no | Strings the server builds no entity from, so this row moves where genres and studios wait. The difference is exactly that this one creates nothing on the receiving side. |
| `People` | video | no | Descriptive | no | People are entities of their own reached through the library rather than a property on the item, and a person the receiving server has never seen is created there. Same condition as genres and studios: decision 5 in #1, held by #15. |
| `OfficialRating` | all | yes | Structural | no | The field an operator most often fixes on one server only, so it moves. It is structural rather than descriptive because a wrong value changes what a restricted account is allowed to see, which is the row to read first when deciding whether to apply a pass. |
| `CustomRating` | all | no | Structural | no | The operator's own override of the rating on this server, set because the fetched one was wrong here. Moving it would overwrite the other operator's deliberate local decision with this one's. |
| `CommunityRating` | all | no | Descriptive | no | Both servers fetch this from the same providers, so moving it copies one server's refresh state rather than a fact, and the value that arrives is stale the moment the receiving server refreshes. |
| `CriticRating` | video | no | Descriptive | no | Same as the community rating: fetched independently on both sides from the same providers. |
| `PremiereDate` | all | yes | Structural | no | A property of the work rather than of either copy. Structural because it orders episodes and seasons, so a wrong value moves items in the library rather than only mislabelling one. |
| `ProductionYear` | all | yes | Structural | no | A property of the work, and one the server groups and disambiguates by, which is why it is structural and not descriptive. |
| `EndDate` | seriesTree | yes | Descriptive | no | The date a series stopped, which is a fact about the series and not about either server's copy of it. |
| `IndexNumber` | seriesTree | no | Structural | no | This is identity. #30 resolves an episode as a series plus an ordinal, so writing the ordinal would move the thing the resolver reads to decide which item it is looking at. |
| `ParentIndexNumber` | seriesTree | no | Structural | no | The season ordinal, and the other half of the same identity. Same reason as the index number. |
| `SeriesName` | episode | no | Structural | no | The series half of an episode's identity, and it is written by the server from the item's place in the tree rather than set on the episode from outside. |
| `ProviderIds` | all | no | Structural | no | The resolver decides which local item a payload is about by reading these, so writing them would change identity underneath a pass that is still running. |
| `RunTimeTicks` | playable | no | Descriptive | yes | The length of the file this server holds. The peer's copy is a different file, so the peer's runtime describes the peer's copy and would be a lie about ours. This is the row the whole from-the-file class exists for. |
| `Container` | playable | no | Descriptive | yes | The format of the file on this server's disk, which the other server's file need not share. |
| `Size` | playable | no | Descriptive | yes | The byte count of this server's file. Same class as the runtime and the same reason. |
| `Width` | playable | no | Descriptive | yes | A property of this server's copy. Two operators holding different encodings of the same film is the ordinary case, not the exception. |
| `Height` | playable | no | Descriptive | yes | The other half of the same measurement of this server's copy. |
| `Path` | playable | no | Structural | yes | Names a file on this server's disk. #28 refuses to read identity out of a path and this row refuses to write one, which are the two halves of the same rule. |
| `DateCreated` | all | no | Structural | yes | When this server first saw its own copy. It drives the recently-added lists, so moving it would rewrite one operator's view of their own library with the other's history. |

## The kind groups


| Group | Item kinds |
| --- | --- |
| `all` | `Movie`, `Series`, `Season`, `Episode`, `Audio`, `MusicAlbum`, `MusicArtist`, `MusicVideo`, `Book`, `AudioBook` |
| `video` | `Movie`, `Series`, `Season`, `Episode`, `MusicVideo` |
| `playable` | `Movie`, `Episode`, `Audio`, `MusicVideo`, `Book`, `AudioBook` |
| `seriesTree` | `Series`, `Season`, `Episode` |
| `episode` | `Episode` |

## Where each field lives

A field is named as the server names it. This table says on which server type,
so a row cannot quietly name a field that does not exist. The suite resolves
every one of them and fails if a name stops resolving, which is what happens
when a server line renames a property.


| Field | Declared on | Reached through |
| --- | --- |  --- |
| `Name` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `ForcedSortName` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Overview` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Tagline` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Genres` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Tags` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Studios` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `ProductionLocations` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `People` | not an item property | `MediaBrowser.Controller.Library.ILibraryManager` |
| `OfficialRating` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `CustomRating` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `CommunityRating` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `CriticRating` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `PremiereDate` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `ProductionYear` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `EndDate` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `IndexNumber` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `ParentIndexNumber` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `SeriesName` | `MediaBrowser.Controller.Entities.TV.Episode` | - |
| `ProviderIds` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `RunTimeTicks` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Container` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Size` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Width` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Height` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `Path` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `DateCreated` | `MediaBrowser.Controller.Entities.BaseItem` | - |

## What this register does not do

It does not say which of two values wins. A field that moves can still be a
field both servers changed, and the conflict rules are argued in M6.

It does not check the kind. The kind group in a row says which item kinds the
row is about, and nothing in the mover reads it today: the mover asks whether
the field moves at all and writes it if so. Enforcing the kind belongs with the
planner that knows what kind of item it is holding, and until that exists this
paragraph is the whole of the disclosure.

It does not decide what happens to a value the receiving server has never seen.
Three rows wait on that question and say so in their reason. Nothing here
settles it.

It does not cover images. Image bytes are a separate refusal with a separate
reason, and #14 is where the rows for them are written.

## What 1.0 does not carry, and which of those can change

Two different kinds of absence sit under this heading and an operator has to be
able to tell them apart, because they read identically in a release note and
they are not the same promise. A permanent non-goal is something this plugin
will not do, in any release, because doing it would be wrong. A scope decision
is something 1.0 does not do, with a stated condition that would let a later
release do it.

Image bytes are the permanent non-goal. Their rows and the reason are #14.

Collection membership and playlist membership are scope decisions.

Neither is a field on an item. A collection and a playlist are items whose
content is a set of references to other items, so carrying one means resolving
every member, and a member that does not resolve leaves a partial collection
that says nothing about why it is short. From the other server that is
indistinguishable from a member somebody removed deliberately, so the two sides
oscillate: this side sends the set it could resolve, the peer reads the missing
members as removals, and the next pass sends the shortened set back. Prior art
in this space ships that behaviour and documents it as a caveat rather than
designing it away.

Playlists carry a second reason of their own. A playlist usually belongs to a
user, and what this plugin may hold and move about a user is decided in #18
rather than here.

The condition that would let a later release carry collections is the unmatched
register in #29 being good enough that a partial collection can explain itself.
An operator has to be able to see, per absent member, why it is absent, and to
tell that apart from a member the other operator removed. Until an incomplete
collection can say which of the two it is, carrying one moves a set nobody
chose. Playlists wait on that and on #18 as well.

The register carries no row for either, and no kind group names `BoxSet` or
`Playlist`. The suite refuses a group that does, rather than leaving this
paragraph to be remembered.
