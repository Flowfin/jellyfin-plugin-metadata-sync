# What a pass writes, and what it cannot promise

What is settled here is the call a change goes through, and what the server does
on disk once it is made.

Whether a field moves at all is not this document. `docs/field-register.md`
decides which fields may move and why, `docs/direction.md` decides which way,
and `docs/conflicts.md` decides what happens when both sides differ. This
document starts after all three have answered and a change is about to be
written.

## What does not exist yet

There is no pass. Nothing in this plugin calls the library:

    git grep -Iln "ILibraryManager\|UpdateItemAsync\|IItemRepository" -- 'Jellyfin.Plugin.MetadataSync/*.cs'
    # no output, exit 1

So every sentence below about the server was read out of the server, and every
sentence about this plugin is a decision taken before the code rather than a
description of code that runs. #35 splits the pass into a planner and an applier
and #39 makes the write. A reader should take this document as the argument they
land against, and not as an account of what happens on a library today.

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

## What holds this up

Nothing here is refused by a machine, and the reason is that the subject is
absent rather than that the rule is soft. There is no write for an assembly walk
to inspect, no constant for a test to assert, and no plan and apply for a
concurrency fixture to run between.

#39 owes the update reason as one constant, the test that every write uses it,
and the walk that fails if a repository type is reachable from a write path.
Until that lands, the sentences above are a decision somebody can argue with and
not a property anything enforces.
