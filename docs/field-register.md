# The field register

This is the set of fields this plugin may move between two paired servers, and
it is the only place a field is declared. A field with no row here does not
move, and asking the plugin to move one is refused when it is asked rather than
noticed afterwards.

The register is written for the operator who has to decide whether to let this
run against their library. It is not a summary of the implementation. The
implementation reads the same rows: they are declared once, in
`Jellyfin.Plugin.MetadataSync/Fields/field-register.json`, which ships inside
the assembly.

Every table below is rendered from that file. The suite renders them again and
compares them to what is committed here, character for character, so a row
added to the source with no rendering is red and a line edited here that the
source does not produce is red as well. The prose between the tables is written
by hand and nothing checks it, which is the bound worth knowing: a paragraph
that contradicts the table under it is caught by a reader and by nothing else.

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

<!-- rendered from field-register.json: rows -->
| Field | Item kinds | Moves | Class | From the file | Reason |
| --- | --- | --- | --- | --- | --- |
| `Name` | all | yes | Descriptive | no | The title an operator reads first and corrects by hand on whichever server they were looking at, which is the case this plugin exists for. |
| `ForcedSortName` | all | no | Structural | no | The stored override behind the sort name, usually empty so the server derives ordering itself. Moving one server's sorting conventions into the other's lists changes the shape of a library nobody asked to reshape. |
| `Overview` | all | yes | Descriptive | no | Free text about the item and not about either copy of it, so the same value is right on both servers. |
| `Tagline` | video | yes | Descriptive | no | Free text of the same kind as the overview, carried on the video kinds that display it. |
| `Genres` | all | no | Descriptive | no | A genre the receiving server has never seen becomes a genre entity there, which is growth of that library rather than a value on one item. Decision 5 in #1 permits that growth on one condition, that every entry created this way carries a mark saying where it came from, and #15 holds the resolution and the mark. The row moves when they land and not before: a genre created without the mark cannot be told from one the operator wrote, and decision 7 needs to take it out again. |
| `Tags` | all | yes | Descriptive | no | Tags are free strings held on the item and the server builds no entity from them, which is what separates this row from genres and studios. |
| `Studios` | video | no | Descriptive | no | A studio the receiving server has never seen becomes a studio entity there. Same answer and same condition as genres: decision 5 in #1 permits the growth, the mark is what makes it reversible, and #15 holds both. |
| `ProductionLocations` | video | yes | Descriptive | no | Strings the server builds no entity from, so this row moves where genres and studios wait. The difference is exactly that this one creates nothing on the receiving side. |
| `People` | video | no | Descriptive | no | People are entities of their own reached through the library rather than a property on the item, and a person the receiving server has never seen is created there. Decision 5 in #1 permits it with the same mark, and this row carries a second condition the other two do not: the comparison that decides a person is already here has to be declared before anything creates one, or the same actor becomes two records spelled differently. #15 holds both. |
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
| `ImageInfos` | all | no | Structural | no | The item's list of image records, each naming a file this server holds. Moving one means moving the bytes behind it, which is file replication under another name and a permanent non-goal of this family of plugins rather than a scope decision a later release revisits. It is not in the from-the-file class because an image is a file of its own rather than the media file, and it never moves for its own reason. |
| `PrimaryImagePath` | all | no | Structural | no | The derived path to the item's primary image on this server's disk. It names a location on this machine, so it is meaningless on the peer, and it is the second way to reach the same bytes the row above refuses. |
<!-- end rendered -->

## The kind groups


<!-- rendered from field-register.json: kind groups -->
| Group | Item kinds |
| --- | --- |
| `all` | `Movie`, `Series`, `Season`, `Episode`, `Audio`, `MusicAlbum`, `MusicArtist`, `MusicVideo`, `Book`, `AudioBook` |
| `video` | `Movie`, `Series`, `Season`, `Episode`, `MusicVideo` |
| `playable` | `Movie`, `Episode`, `Audio`, `MusicVideo`, `Book`, `AudioBook` |
| `seriesTree` | `Series`, `Season`, `Episode` |
| `episode` | `Episode` |
<!-- end rendered -->

## Where each field lives

A field is named as the server names it. This table says on which server type,
so a row cannot quietly name a field that does not exist. The suite resolves
every one of them and fails if a name stops resolving, which is what happens
when a server line renames a property.


<!-- rendered from field-register.json: where each field lives -->
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
| `ImageInfos` | `MediaBrowser.Controller.Entities.BaseItem` | - |
| `PrimaryImagePath` | `MediaBrowser.Controller.Entities.BaseItem` | - |
<!-- end rendered -->

## What each row means for your library

The same rows again, in the terms somebody deciding whether to run this actually
asks in: will the thing I wrote here survive, and will the thing I fixed here
reach the other server. Both this table and the one above are rendered from the
same file, so they cannot come apart, and the sentences here are written by hand
in that file rather than derived from the class.

<!-- rendered from field-register.json: what it means for an operator -->
| Field | Does it travel | What that means for your library |
| --- | --- | --- |
| `Name` | yes | The title you see in your library. If the other server has a different one, yours is replaced with theirs, so a title you corrected by hand here will be corrected there too rather than fought over. |
| `ForcedSortName` | no | The hidden value that decides where an item sits in an alphabetical list. It stays as you set it, because the two libraries are sorted by two people with different habits and neither should reorder the other's shelves. |
| `Overview` | yes | The description text. It is the same work on both servers, so the same description is right on both, and this one travels. Lock it on an item if the description there is yours. |
| `Tagline` | yes | The one-line strapline shown above the description on films and shows. Same reasoning as the description, and it travels. |
| `Genres` | no | Does not travel yet. A genre the other server has and you do not would be created here as a new genre, which grows your library rather than editing one item. That is allowed, but only once every entry this plugin creates is marked as its own, so that removing a pairing can take them out again. Until the mark exists your genre list is left alone. |
| `Tags` | yes | Your own free-text tags travel. They are plain strings on the item and the server builds nothing else out of them, so a tag arriving from the other server adds no entry to any list you did not already have. |
| `Studios` | no | Does not travel yet, for the same reason as genres: a studio you do not have would be created here as a new studio, and that is allowed only once the plugin marks what it created. |
| `ProductionLocations` | yes | The filming countries travel. They are plain strings like your tags, and nothing new is created here to hold them. |
| `People` | no | Cast and crew do not travel yet. A person you do not have would be created here as a new person, which is allowed once the plugin marks what it created. It also needs a stated rule for when two spellings of a name are the same person, because matching by name across two servers is how one actor becomes two records with an accent between them. |
| `OfficialRating` | yes | The age rating travels. Read this row before you let a pass apply: the rating decides what a restricted account in your house is allowed to see, so a wrong value arriving here changes what somebody can watch. |
| `CustomRating` | no | Your own override of the age rating stays here. You set it because the fetched one was wrong on this server, and taking the other operator's override would undo your decision with theirs. |
| `CommunityRating` | no | The community score stays here. Both servers fetch it from the same places, so copying it moves how recently the other server refreshed rather than anything about the film. |
| `CriticRating` | no | The critic score stays here, for the same reason as the community score. |
| `PremiereDate` | yes | The release date travels. It is a fact about the work rather than about your copy. It also orders episodes and seasons, so a wrong one moves things in your library rather than only mislabelling one item. |
| `ProductionYear` | yes | The year travels. It is a fact about the work, and your server uses it to tell two films with the same title apart. |
| `EndDate` | yes | The date a series ended travels. It is a fact about the series and not about either server's copy. |
| `IndexNumber` | no | The episode number stays here. It is part of how an episode is identified, so writing it would move the thing this plugin reads to work out which episode it is looking at. |
| `ParentIndexNumber` | no | The season number stays here, for the same reason as the episode number. |
| `SeriesName` | no | The series an episode belongs to stays here. Your server writes it from where the episode actually sits in your library, rather than from anything sent to it. |
| `ProviderIds` | no | The database identifiers stay here. They are what this plugin matches your items against the other server's by, so writing them would change the answer underneath a sync that is still running. |
| `RunTimeTicks` | no | The running time stays here. It is the length of your file, and the other server holds a different file. Their runtime describes their copy and would simply be untrue about yours. |
| `Container` | no | The file format stays here. It describes the file on your disk. |
| `Size` | no | The file size stays here. It describes your file, not the work. |
| `Width` | no | The picture width stays here. Two people holding different encodings of one film is the normal case rather than a problem to sync away. |
| `Height` | no | The picture height stays here, for the same reason as the width. |
| `Path` | no | Where the file sits on your disk stays here, and this plugin neither reads identity out of a path nor writes one. |
| `DateCreated` | no | When your server first saw its copy stays here. It drives your recently-added list, so taking the other server's value would rewrite your own view of your library with somebody else's history. |
| `ImageInfos` | no | Your posters, backdrops, logos and thumbnails stay yours. Each entry here points at a picture file on your disk, and sending one means sending the file, which is the one thing this plugin will never do. Two servers with different artwork stay that way, and a poster you chose by hand here has to be chosen by hand there too. |
| `PrimaryImagePath` | no | Where your main picture for an item sits on your disk stays here. It is a path on your machine, so it would mean nothing on the other server even if the picture were sent, and the picture is not sent either. |
<!-- end rendered -->

## Which lock governs a row

An operator can already tell the server that a field is theirs. Two instruments
do it and they are not the same size. The item-level lock claims the whole item.
The field-level lock claims one of nine named fields, and the nine are the
server's own set rather than this register's, so most rows here have no lock of
their own and are claimed only by locking the item.

A lock refuses a write on this server whatever the direction says and whatever
the configuration says. The mover checks the item first and the field second,
and it refuses before it reaches the writer.


<!-- rendered from field-register.json: locks -->
| Field | The lock that governs it |
| --- | --- |
| `Name` | `MetadataField.Name` |
| `ForcedSortName` | the item-level lock only |
| `Overview` | `MetadataField.Overview` |
| `Tagline` | the item-level lock only |
| `Genres` | `MetadataField.Genres` |
| `Tags` | `MetadataField.Tags` |
| `Studios` | `MetadataField.Studios` |
| `ProductionLocations` | `MetadataField.ProductionLocations` |
| `People` | `MetadataField.Cast` |
| `OfficialRating` | `MetadataField.OfficialRating` |
| `CustomRating` | the item-level lock only |
| `CommunityRating` | the item-level lock only |
| `CriticRating` | the item-level lock only |
| `PremiereDate` | the item-level lock only |
| `ProductionYear` | the item-level lock only |
| `EndDate` | the item-level lock only |
| `IndexNumber` | the item-level lock only |
| `ParentIndexNumber` | the item-level lock only |
| `SeriesName` | the item-level lock only |
| `ProviderIds` | the item-level lock only |
| `RunTimeTicks` | `MetadataField.Runtime` |
| `Container` | the item-level lock only |
| `Size` | the item-level lock only |
| `Width` | the item-level lock only |
| `Height` | the item-level lock only |
| `Path` | the item-level lock only |
| `DateCreated` | the item-level lock only |
| `ImageInfos` | the item-level lock only |
| `PrimaryImagePath` | the item-level lock only |
<!-- end rendered -->

## What this register does not do

It does not say which of two values wins. A field that moves can still be a
field both servers changed, and the conflict rules are argued in M6.

It does not check the kind. The kind group in a row says which item kinds the
row is about, and nothing in the mover reads it today: the mover asks whether
the field moves at all and writes it if so. Enforcing the kind belongs with the
planner that knows what kind of item it is holding, and until that exists this
paragraph is the whole of the disclosure.

It does not decide whether the target of a reference is already here. Genres,
studios and people name something the server holds separately, so writing one
of those rows means finding that thing here or making it. Which two spellings
are one entry, and which pair is too close for this plugin to decide either way,
is declared in `docs/references.md` and not in a row above.

The three rows still say they do not move, and the comparison landing does not
change that. What is missing is the mark: an entry created by a sync has to
carry one, there is nowhere yet to keep it, and until there is, a resolution is
something a plan can show rather than something a pass performs.

It does not carry the identity an image could be fetched from. The two image
rows above refuse the bytes, and what could move in their place is a provider
identifier the receiving server's own image provider fetches its own copy with.
No row above is that identifier. The identifiers this plugin knows about are in
`ProviderIds`, which is declared as a row that does not move because the
resolver reads it to decide which local item a payload is about, and whether an
image identifier moves separately from that is not settled here.

It does not record a lock refusal. A lock stops the write and nothing writes
down that it did, because there is nowhere yet to write it; #48 is the conflict
log that entry is owed to. Until then a refused write is visible to the caller
that asked for it and to nobody else.

It does not know the peer's lock state. The lock table above is about locks on
this server. A field the operator locked on the other server has to refuse a
send rather than a write, which means the answer this plugin gets back has to
carry that state, and it comes from a contract this plugin does not yet
reference. Nothing here covers that direction, and a reader should assume it is
uncovered rather than covered elsewhere.

## What 1.0 does not carry, and which of those can change

Two different kinds of absence sit under this heading and an operator has to be
able to tell them apart, because they read identically in a release note and
they are not the same promise. A permanent non-goal is something this plugin
will not do, in any release, because doing it would be wrong. A scope decision
is something 1.0 does not do, with a stated condition that would let a later
release do it.

Image bytes are the permanent non-goal, and the two rows above are where that
is declared rather than described. `ImageInfos` is the item's own list of image
records and `PrimaryImagePath` is the derived path to one of them, so between
them they are every image field the item type carries. The suite reads the
server's item type back and fails if it grows a third, because an image field
the register does not name is one this document would be silent about.

The cost is real and is stated rather than hidden. Two servers that have
different artwork stay that way. An operator who curated a poster by hand on one
server has to do it again on the other.

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
