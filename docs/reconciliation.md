# What a pass writes, and what it cannot promise

Two things are settled here. The call a change goes through, and what the server
does on disk once it is made. And the window between deciding a change and
making it, which is a property of the design rather than a defect in the code
that will fill it.

Whether a field moves at all is not this document. `docs/field-register.md`
decides which fields may move and why, `docs/direction.md` decides which way,
and `docs/conflicts.md` decides what happens when both sides differ. This
document starts after all three have answered and a change is about to be
written.

## What exists, and what still does not

The write exists. `LibraryPlanTarget` is the implementation the interface
between the two halves was waiting for, it calls the member named below and
nothing else, and the suite reads the compiled instructions rather than the
source to say so:

    git grep -Iln "ILibraryManager" -- 'Jellyfin.Plugin.MetadataSync/*.cs'
    Jellyfin.Plugin.MetadataSync/Reconciliation/LibraryPlanTarget.cs

What does not exist is a pass. Nothing reads two servers, nothing turns items
into the observations a plan is made from, and nothing registers the write path
as a service, so a plan can be carried out in a test and nowhere else. Every
sentence below about the server was read out of the server.

The window and the deferral are code rather than a decision taken ahead of it.
The item is fetched again immediately before its plan is carried out, its
last-saved stamp is compared against the one the plan was made from, and an item
that moved is deferred with nothing written on it. `## What holds this up` below
names the check behind each of those and what removing it reddens.

One half of the deferral is still a decision taken before the code, and it is
named where the deferral is described rather than here: a deferred item is
counted and handed back to whoever asked for the apply, and nothing picks it up
on a later pass, because there is no later pass.

## What a plan row can carry, and what it cannot

A plan row holds one string per field, and the server holds nine different
things. Four of the fields the register lets move are strings and the row is the
value. Two are dates and are read in the round-trip spelling,
`DateTime.ToString("O")`, strictly: a value in any other spelling is refused
rather than read under whichever locale the server happens to run in, which is
how a day silently becomes a month. One is a year and is read as digits with no
sign, no separator and no space.

The remaining two, `Tags` and `ProductionLocations`, are sets of strings and are
refused. Any character chosen to separate two entries inside one string is a
character an operator may have typed inside one entry, so a value written on one
side comes back as one entry too many or one too few, and nothing anywhere
declares an escaping that would stop it. Whoever builds the half that reads
items into observations owns that declaration, because it has to hold on both
sides of it; until it exists a row for either field is refused loudly and no
part of the item is written.

The suite holds the two sets against the register in both directions, so a tenth
row declared to move reds it until somebody has decided which of the two the new
field is in.

## The one call

There is one supported way to change an item and have the server notice, and it
is the same member on both supported lines:

    git grep -n "UpdateItemAsync" v10.11.11 v12.0-rc4 -- MediaBrowser.Controller/Library/ILibraryManager.cs
    v10.11.11:MediaBrowser.Controller/Library/ILibraryManager.cs:282:        Task UpdateItemAsync(BaseItem item, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken);
    v12.0-rc4:MediaBrowser.Controller/Library/ILibraryManager.cs:332:        Task UpdateItemAsync(BaseItem item, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken);

It forwards to the plural form, which is where everything in this document
happens:

    git show v10.11.11:Emby.Server.Implementations/Library/LibraryManager.cs | sed -n '2202,2203p'
        public Task UpdateItemAsync(BaseItem item, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken)
            => UpdateItemsAsync([item], parent, updateReason, cancellationToken);

Writing underneath it, into the repository or into the database file, leaves the
server's caches and every connected client holding the old value. It is not
something this plugin does, and the guard that refuses a repository type on a
write path is #39's.

## An item is the unit

A plan for one item can name several fields, and the rows are applied together or
not at all. Every refusal an item's rows can raise is raised before the first of
them is set: the write path reads each row into the assignment it will make, and
only once every row has been read does it make them.

The order matters because the object being set is the library's own item and not
a copy of it. A path that set two fields and then refused the third would leave
that item holding a mixture neither server ever described. Not written to disk by
this plugin, since the supported call is never reached, and then written by
whatever saves that item next for its own reasons, with nothing recording where
the mixture came from. Stopping short of the call does not undo what was set.

What it costs is one list of assignments per item, held for as long as that item
takes to write.

Two bounds. It is one item and not a plan: a pass interrupted between two items
has written the first and not the second, which is the pass's own resumption and
is #38. And it ends at the supported call, so a failure the server raises inside
that call is outside what this can promise, because by then the values are on the
item and the server owns what happens to them.

## The update reason is the parameter that reaches the disk

`ItemUpdateType` is a flag set, and the comparisons the server makes on it are
numeric rather than flag tests, so the value chosen decides which thresholds are
cleared:

    git show v12.0-rc4:MediaBrowser.Controller/Library/ItemUpdateType.cs | grep -n "= [0-9]"
    10:        None = 1,
    11:        MetadataImport = 2,
    12:        ImageUpdate = 4,
    13:        MetadataDownload = 8,
    14:        MetadataEdit = 16

The reason travels straight from the call into the savers:

    git show v10.11.11:Emby.Server.Implementations/Library/LibraryManager.cs | sed -n '2153,2165p'
        public async Task UpdateItemsAsync(IReadOnlyList<BaseItem> items, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken)
        {
            foreach (var item in items)
            {
                item.DateLastSaved = DateTime.UtcNow;
                await RunMetadataSavers(item, updateReason).ConfigureAwait(false);

                // Modify again, so saved value is after write time of externally saved metadata
                item.DateLastSaved = DateTime.UtcNow;
            }

            _itemRepository.SaveItems(items, cancellationToken);

and the savers are reached only for an item the server holds on a file protocol:

    git show v10.11.11:Emby.Server.Implementations/Library/LibraryManager.cs | sed -n '2211,2219p'
        public async Task RunMetadataSavers(BaseItem item, ItemUpdateType updateReason)
        {
            if (item.IsFileProtocol)
            {
                await ProviderManager.SaveMetadataAsync(item, updateReason).ConfigureAwait(false);
            }

            await UpdateImagesAsync(item, updateReason >= ItemUpdateType.ImageUpdate).ConfigureAwait(false);
        }

Both lines carry that method identically, which is worth having as a command
rather than as a claim:

    diff <(git show v10.11.11:Emby.Server.Implementations/Library/LibraryManager.cs | sed -n '2211,2219p') \
         <(git show v12.0-rc4:Emby.Server.Implementations/Library/LibraryManager.cs | sed -n '2658,2666p')
    # no output

## The reason chosen, and what it costs

`MetadataEdit`. What this plugin does is a deliberate change from an authority
the operator chose, so recording it as a download from a metadata provider would
be false in the one field a later reader uses to tell the two apart. The
constant that holds it and the test that asserts every write uses it are #39's,
and nothing passes it yet.

The cost is that `MetadataEdit` is the highest of the five values, so it clears
every threshold the server has. Two of them are worth naming, because an
operator meets both on their own disk.

A sidecar saver decides for itself, against a threshold of its own:

    git show v10.11.11:MediaBrowser.XbmcMetadata/Savers/MovieNfoSaver.cs | sed -n '76,90p'
        public override bool IsEnabledFor(BaseItem item, ItemUpdateType updateType)
        {
            if (!item.SupportsLocalMetadata)
            {
                return false;
            }

            // Check parent for null to avoid running this against things like video backdrops
            if (item is Video video && item is not Episode && !video.ExtraType.HasValue)
            {
                return updateType >= MinimumUpdateType;
            }

            return false;
        }

    git show v10.11.11:MediaBrowser.XbmcMetadata/Savers/BaseNfoSaver.cs | sed -n '130,141p'
        protected ItemUpdateType MinimumUpdateType
        {
            get
            {
                if (ConfigurationManager.GetNfoConfiguration().SaveImagePathsInNfo)
                {
                    return ItemUpdateType.ImageUpdate;
                }

                return ItemUpdateType.MetadataDownload;
            }
        }

Sixteen clears eight, so the saver runs. That is the intended half.

The unintended half is one branch further in, where a library with local
metadata saving switched off is not the refusal an operator would expect:

    git show v10.11.11:MediaBrowser.Providers/Manager/ProviderManager.cs | sed -n '749,759p'
                        if (!item.IsSaveLocalMetadataEnabled())
                        {
                            if (updateType >= ItemUpdateType.MetadataEdit)
                            {
                                // Manual edit occurred
                                // Even if save local is off, save locally anyway if the metadata file already exists
                                if (saver is not IMetadataFileSaver fileSaver || !File.Exists(fileSaver.GetSavePath(item)))
                                {
                                    return false;
                                }
                            }

So an operator who turned local metadata saving off, and who has an old sidecar
file still lying beside their film, gets that file rewritten by a sync. It is
the server's own rule for a manual edit and this plugin is asking to be treated
as one. Stating it is the point: the alternative is an operator discovering it
from a file timestamp.

## What an operator sees on disk after a sync

For a film in its own folder, on a file protocol, in a library with a sidecar
saver enabled, the file rewritten is the first path the saver offers:

    git show v10.11.11:MediaBrowser.XbmcMetadata/Savers/MovieNfoSaver.cs | sed -n '/protected override string GetLocalSavePath/,+1p'
        protected override string GetLocalSavePath(BaseItem item)
            => GetMovieSavePaths(new ItemInfo(item)).FirstOrDefault() ?? Path.ChangeExtension(item.Path, ".nfo");

which is `movie.nfo` in the folder holding the film, and the film's own name with
an `.nfo` extension where the folder holds more than one film. Episodes, series
and the rest each have their own saver and their own path, and this document
names one rather than listing them, because the shape is the saver's and not this
plugin's.

The write is bracketed so the server does not read its own change back as an
edit somebody made outside it:

    git show v10.11.11:MediaBrowser.Providers/Manager/ProviderManager.cs | sed -n '696,701p'
                    try
                    {
                        _libraryMonitor.ReportFileSystemChangeBeginning(path);
                        await saver.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
                        item.DateLastSaved = DateTime.UtcNow;
                    }

Images are the second threshold, and this is the part an operator is least
likely to expect. `ImageUpdate` is four, every value that clears a sidecar
saver's threshold also clears that one, and the force branch treats every image
the item carries as outdated rather than only the ones needing work:

    git show v12.0-rc4:Emby.Server.Implementations/Library/LibraryManager.cs | sed -n '2459,2461p'
            var outdated = forceUpdate
                ? item.ImageInfos.Where(i => i.Path is not null).ToArray()
                : item.ImageInfos.Where(ImageNeedsRefresh).ToArray();

An image that is not already a local file is then fetched and written locally,
and an image whose fetch fails is dropped from the item:

    git show v10.11.11:Emby.Server.Implementations/Library/LibraryManager.cs | sed -n '2988,2998p'
        public async Task<ItemImageInfo> ConvertImageToLocal(BaseItem item, ItemImageInfo image, int imageIndex, bool removeOnFailure)
        {
            foreach (var url in image.Path.Split('|'))
            {
                try
                {
                    _logger.LogDebug("ConvertImageToLocal item {0} - image url: {1}", item.Id, url);

                    await ProviderManager.SaveImage(item, url, image.Type, imageIndex, CancellationToken.None).ConfigureAwait(false);

                    await item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, CancellationToken.None).ConfigureAwait(false);

So a poster the item still held as a remote address can become a file on disk as
a consequence of a metadata write, and a poster whose address no longer answers
can disappear from the item. Neither is this plugin copying an image: the bytes
are the server's own fetch from an address the item already carried, and the
permanent non-goal in `docs/field-register.md` is untouched by it. What changes
is what an operator watching that folder sees, which is why it is here rather
than left to be found.

There is no update reason that avoids this and still writes a sidecar. Anything
at or above eight is at or above four, and the two values below four are below
the saver's own threshold as well, so a write that persists metadata to disk and
leaves images alone is not a thing the parameter can express.

`DateLastSaved` moves either way. It is set on the item before the savers run
and again after them, in the plural call quoted above, so it moves for an item
with no saver enabled and for an item on a protocol the savers never reach.

## The window between planning and applying

The pass decides first and writes second, which is #35, and the gap between the
two is real time on a running server. A scheduled library scan, a provider
refresh, or an operator editing the same field in the web client can all land in
it.

What the server offers for seeing a refresh is four members, and none of them is
a lock:

    git grep -n "GetRefreshQueue\|OnRefreshStart\|OnRefreshComplete\|GetRefreshProgress" v10.11.11 -- MediaBrowser.Controller/Providers/IProviderManager.cs
    v10.11.11:MediaBrowser.Controller/Providers/IProviderManager.cs:212:        HashSet<Guid> GetRefreshQueue();
    v10.11.11:MediaBrowser.Controller/Providers/IProviderManager.cs:214:        void OnRefreshStart(BaseItem item);
    v10.11.11:MediaBrowser.Controller/Providers/IProviderManager.cs:218:        void OnRefreshComplete(BaseItem item);
    v10.11.11:MediaBrowser.Controller/Providers/IProviderManager.cs:220:        double? GetRefreshProgress(Guid id);

Two of them are reads and two are notifications a component makes about itself.
Nothing there takes an item exclusively for the length of a write, and nothing
in `ILibraryManager` does either, so a pass cannot ask the server to hold an item
still.

What narrows the window is re-reading the item immediately before applying and
comparing its last-saved stamp against the one the plan was made from. That
comparison is sound because the stamp moves on every update through the
supported call, whether or not a saver ran, which is the paragraph above rather
than an assumption. Two bounds on it. The comparison has to be against an item
fetched again, not against the instance the plan carries, because the call
mutates the instance it is given. And it detects a save, not an intent, so an
item a component is part way through refreshing looks unchanged until that
component saves.

That comparison is made. The stamp travels from the reading, onto the plan, to
the write, and it travels as a string rather than as a time: the only thing done
with it is equality, nothing orders two of them, and the planner's input surface
refuses a clock outright. It is derived in one place, so the half that reads
items and the half that writes them cannot spell it two ways.

An item that loses the comparison is deferred rather than failed. The pass writes
nothing on it, counts it apart from the items it decided against, and carries on
with the rest; an item that has gone entirely is deferred on the same footing,
because both are ordinary events on a library somebody uses. A plan row that does
not describe a value is not one of these and stops the pass, because that is a
defect in whatever produced the row and passing over it would hide it.

The next pass picking a deferred item up is the half that does not exist. There
is no pass: nothing schedules one, nothing reads two servers into a plan, and the
count of deferred items is returned to whoever asked for the apply and recorded
nowhere.

This document does not claim the window is closed. What is left after the
re-read is the interval between the comparison and the write, this plugin cannot
make it zero, and a pass that reported otherwise would be reporting on a race it
lost. The suite drives an item that moves between planning and applying and
asserts the deferral, on one thread and between two statements, so what is proved
is the comparison and the counting and never a race.

## What holds this up

The write is refused by a machine now, in three places, and each was proved by
breaking it rather than by being written down.

The update reason is one constant and every write carries it, which reddens if
the constant moves. A walk over the compiled assembly starts at the types that
carry a plan to a library, follows every call into this plugin's own types, and
fails on a database namespace, a repository type or a member that writes
underneath the supported call. And the library the suite writes against answers
two members and throws on every other one, so a path that reached for anything
else fails with the member's name in it.

What that walk cannot do is stated where it lives rather than only here. It
follows calls into this assembly and stops at the edge of it, so what the server
does after the supported call is outside it on purpose. It refuses a name, so a
repository reached through an interface with an innocent name, or through
reflection, spells nothing it can see.

The window is narrowed by a check the suite proves bites. Removing the
comparison reddens the fixture that moves an item under a plan; removing the
deferral from the applier reddens the two that carry on past one; widening it to
catch every failure reddens the one that says a defect stops the pass. What
nothing holds is the next pass picking a deferred item up, because there is no
next pass to hold.

One allowance was added to the invariant lint for this, and it is named here
because a lint quietly narrowed is worse than one that reds. The rule that
refuses a timestamp compares a stamp from one server against a stamp from the
other, and the file that writes reads this server's stamp and holds it against
this server's own earlier reading. The rule's record now carries a file and the
reason, the rule still refuses that text from anywhere else, and the suite
refuses an allowance naming a file that is not in the tree.

An item being the unit is held by a pair rather than by one test. A plan whose
third row is refused is asserted to leave the item holding what it held, and the
same three rows with every one of them readable are asserted to all arrive, so
the first is not satisfied by a path that writes nothing. Collapsing the two
loops back into one reddens the first and leaves the second green; a writer that
stops assigning reddens the second. Both were run.

One more thing nothing holds. A stopped pass stops within one item, and it
reports nothing about how far it got: the applier throws where the operator
asked it to stop. Turning that into a result carrying the items already written
is the pass's own bound, #37, and its resumption is #38.
