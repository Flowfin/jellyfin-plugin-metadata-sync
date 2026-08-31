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
between the two halves was waiting for, it calls one member to fetch an item and
one to save it and nothing else, and the suite reads the compiled instructions
rather than the source to say so.

The read exists as well now. `ItemReader` asks the server for the items of the
libraries that take part, which is the subject of `## Which libraries a pass
reads` below. So the plugin names the server's library in the files below, and
each of them asks it something different:

    git grep -Iln "ILibraryManager" -- 'Jellyfin.Plugin.MetadataSync/*.cs'
    Jellyfin.Plugin.MetadataSync/Configuration/ServerLibraries.cs
    Jellyfin.Plugin.MetadataSync/PluginServiceRegistrator.cs
    Jellyfin.Plugin.MetadataSync/Reconciliation/ItemReader.cs
    Jellyfin.Plugin.MetadataSync/Reconciliation/LibraryPlanTarget.cs

`ReconciliationStatementTests` re-runs that reading against the plugin's own
sources in both directions, so a fifth file naming the library reds this
paragraph and a line naming a file that has stopped naming it reds it too. The
count that stood in the sentence above is gone rather than corrected: a numeral
beside a list is a second answer to the question the list answers, and it is the
half nothing could hold.

That paste named one file and the command returned three, before this change
added the fourth. It was found by running the command rather than by reading the
paragraph: the read below makes the plugin name the library in one more place,
and re-running the line to add it returned two others the document had never
carried. `ServerLibraries` asks which libraries the server holds, so that a
configuration can be checked against a range, and the registrator resolves the
library inside a delegate so that range is read when somebody asks rather than
at start-up. Neither is a pass reaching a library, which is what the sentence
above the paste is about, and neither was excluded on purpose.

What does not exist is a pass that runs. A `Pass` type exists, and what it adds
to the halves it drives is an ordering rather than a step: it drives them item by
item and records how far it got, which is `## A pass that was stopped is
continued` below. Nothing constructs it, nothing reads the peer, and nothing
registers it or either half as a service, so all of them can be held together in
a test and nowhere else. Every sentence below about the server was read out of
the server.

The plan-only route exists as well, and it is a type of its own rather than a
mood a pass is in. `DryRun` takes what the two servers hold and hands back the
plan, and `## A plan can be had without anything that writes` below is what that
buys and what it does not.

The constructions are spellings rather than judgements, so they are held. Each
line below is one `ReconciliationStatementTests` looks for in the plugin's own
sources, and the claim is that it finds none, so the day a pass builds a reader
or a write path this paragraph reds instead of going on saying nothing does.

<!-- the spellings this page says appear nowhere in the plugin's sources: one per line, the spelling first, read by ReconciliationStatementTests -->

- `new ItemReader`, the construction that would build the read this document
  describes
- `new LibraryPlanTarget`, the construction that would build the write path a
  plan is carried out through

<!-- end of the spellings that appear nowhere -->

That is a negative disclosure and it stays one. What is asserted is the absence
this paragraph states, never that the absence is harmless.

What the list cannot reach is a line taken out of it. Every spelling written here
is refused, and a claim this paragraph stops making stops being checked along
with it, which is the same bound the rule table in `docs/conflicts.md` states
about a rule that stops being declared. Narrowing this list is a change to what
the page claims, and a reader of the diff is what stands in the way of it.

The third half of the sentence that stood here, that nothing turns items into
the observations a plan is made from, is not repeated. It is the same claim
`docs/conflicts.md` fences and holds, and two documents stating one fact is the
arrangement where the one nobody reads goes stale in silence while the other
reds - which is what this document's own opening paste already cost once. Read
it there.

The window and the deferral are code rather than a decision taken ahead of it.
The item is fetched again immediately before its plan is carried out, its
last-saved stamp is compared against the one the plan was made from, and an item
that moved is deferred with nothing written on it. `## What holds this up` below
names the check behind each of those and what removing it reddens.

One half of the deferral is still a decision taken before the code, and it is
named where the deferral is described rather than here: a deferred item is
counted and handed back to whoever asked for the apply. What a later pass does
with it is now decided rather than absent - a deferred item is not recorded as
finished with, so the pass that runs next reaches it again - and nothing runs a
later pass, because nothing runs a pass at all.

## Which libraries a pass reads

Participation is per library rather than per server, and it is decided before an
item is read. `ItemReader` is built from the set of participating libraries and
asks for the items under those libraries, so a library that does not take part
is never named in a query.

The difference between that and reading everything and keeping what is in the
set is invisible in the answer on a good day. On a bad one it is the whole
thing: a defect in a filter reaches a library an operator excluded, and a query
that never named the library cannot. That is why the participating libraries are
the ancestors the query asks under rather than a test applied to what comes
back.

The empty set is the case to be deliberate about, because it is the state a
plugin is installed in. It means no library takes part, never all of them, so no
query is made at all. What makes that worth writing down rather than assuming is
that the query type reads the other way round: a recursive query carrying no
ancestor is a query over everything the server holds, so turning an empty set
into a query is one line that reads as harmless and enumerates a whole server.

Three things the read deliberately does not do.

It does not ask whether an identifier is a library this server holds.
`ConfigurationValidation` refuses a configuration naming one the server does not
have, and a second answer here would be a second place for that to be decided.

It does not decide how many items come back in one call, which is the section
below rather than this one.

It holds nothing between passes. The next pass asks again, because the library
moved while nothing was running, which is the property a resumed pass needs from
a read and is #38.

Two things follow from where the decision sits, and neither needs a mechanism of
its own. An item moved into a library that does not take part is not written,
and the refusal is at the read: nothing further along is asked about the item at
all. An item moved into a participating library is picked up by the next pass
for the same reason.

The half of that which was already true is a different check and reads like this
one. The write path fetches the item again and defers it where something else
saved it in between, which catches an item that was touched. An item that
changed library and was not otherwise touched is exactly the case that check
does not cover, and the read is what covers it.

What none of this makes true is that a pass runs. Nothing in this plugin
constructs a reader, so the reader, the planner and the write path are held
together in the suite and nowhere else. What would run one is #40. The
administrator surface that shows which libraries take part is #51 and has no
controller to sit on, and when that selection last changed is not derivable from
the configuration, which holds the set and not a moment.

## How much of a library is held while it is read

A first pass over a library of fifty thousand items, on a server that is also
transcoding for two people, is where a read that asks once and is handed a list
becomes the thing that makes a media server unusable. So the read hands items
over a page at a time, and the page size is handed to `ItemReader` rather than
held by it: `PluginConfiguration.ItemsPerRead` is where an operator says how
many, `ItemsPerReadDefault` is what they get if they say nothing, and the suite
reads both rather than restating either.

Which items there are is one question, asked once. Which items those are is
asked a page at a time afterwards, and every one of those page queries names the
participating libraries as well as the identifiers, so the property the section
above is about holds on each call on its own rather than only across the
sequence.

Asking for the identifiers first is a choice against the obvious alternative,
and the reason is what the alternative loses. Paging by offset over a library
something else is writing to skips an item whenever an earlier one is deleted
underneath the walk: the later pages shift up by one, and the item that moved
across the boundary is never asked for. Nothing reports it. The pass finishes,
counts what it wrote, and an operator reads a success over a library that has an
item in it the sync did not look at. A list of identifiers taken in one answer
cannot be overtaken that way.

What that costs is stated rather than avoided. The identifiers of everything
that takes part are held for the length of the pass, at sixteen bytes each,
which is one and a half megabytes at a hundred thousand items. Against that, the
items themselves are the library's metadata and are held one page at a time.

What it does not promise. An item deleted after the identifiers were read is not
handed back by the server and is simply absent from its page; an item created
after them is not seen until the next pass. Both are the library moving under a
pass rather than a defect, and neither is silent in the way the offset walk is,
because the identifier was read once and what became of it is answerable.
Nothing is locked across a page, so an item can still change between the page it
arrived on and the moment a write is attempted. That window is `## The window
between planning and applying` below, and it is not narrowed by anything here.

**Two of the four bounds #37 names are here now, and this paragraph said three
were missing.** How long a pass may run was one of the three, on the reasoning
that it had no pass to bound; a pass exists and the bound is the section on a
pass stopping when its time is up, below. What is still missing is two. How many
resolutions may be in flight is a property of a contract this plugin does not
reference, and how many writes per unit of time wants a measurement against a
real library rather than a number chosen in front of an operator. A bound lands
with the thing it bounds, so those two wait on the thing rather than on the
number, and a setting offered for either would be one an operator can move with
nothing behind it.

The fourth bound is what #37 asks of it: a named constant with a default, in
configuration, with a stated maximum the configuration cannot exceed.
`ConfigurationValidation` refuses a page outside that range and names the
property in the sentence, so a number an operator can save is a number a pass
can read. The range is refused at both ends and clamped at neither, because a
page silently moved to something the operator did not ask for is a pass that
behaves unlike the page they are looking at.

The lower end is the one worth reading. A page of no items advances a read by no
items, so a read handed zero asks the server for the same nothing forever, and
the symptom is a pass that never ends rather than one that reads nothing. It is
refused twice, in two different senses: `ConfigurationValidation` refuses the
file an operator saved, and `ItemReader` refuses the argument it is handed,
because a caller inside this plugin is not an operator and does not go past the
configuration. The maximum is refused in the first place only. It bounds what an
operator may express rather than what the type can do, and a second copy of it
on the reader would be a second place that range is decided.

What the two numbers are is a choice and not a measurement, and that is
unchanged by their having moved into configuration. #37 asks for a measured one,
and the measurement that would produce it is the same one the write rate wants.
What the setting buys before that measurement exists is that an operator whose
server is the one the number is wrong for can change it without a build.

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
a lock. Read at both supported lines rather than at one, because a member the
newer line dropped would leave this section describing a surface half the
operators this plugin is built for do not have:

    git grep -n "GetRefreshQueue\|OnRefreshStart\|OnRefreshComplete\|GetRefreshProgress" v10.11.11 v12.0-rc4 -- MediaBrowser.Controller/Providers/IProviderManager.cs
    v10.11.11:MediaBrowser.Controller/Providers/IProviderManager.cs:212:        HashSet<Guid> GetRefreshQueue();
    v10.11.11:MediaBrowser.Controller/Providers/IProviderManager.cs:214:        void OnRefreshStart(BaseItem item);
    v10.11.11:MediaBrowser.Controller/Providers/IProviderManager.cs:218:        void OnRefreshComplete(BaseItem item);
    v10.11.11:MediaBrowser.Controller/Providers/IProviderManager.cs:220:        double? GetRefreshProgress(Guid id);
    v12.0-rc4:MediaBrowser.Controller/Providers/IProviderManager.cs:223:        HashSet<Guid> GetRefreshQueue();
    v12.0-rc4:MediaBrowser.Controller/Providers/IProviderManager.cs:225:        void OnRefreshStart(BaseItem item);
    v12.0-rc4:MediaBrowser.Controller/Providers/IProviderManager.cs:229:        void OnRefreshComplete(BaseItem item);
    v12.0-rc4:MediaBrowser.Controller/Providers/IProviderManager.cs:231:        double? GetRefreshProgress(Guid id);

The same four members on both lines, at different line numbers. Two of
them are reads and two are notifications a component makes about itself. Nothing
there takes an item exclusively for the length of a write.

Nothing in `ILibraryManager` does either, and that sentence stood here with no
command behind it until now, in a section whose whole subject is what the server
will and will not do for a pass:

    git grep -niE "lock|exclusive|reserv|acquire|semaphore" v10.11.11 v12.0-rc4 -- MediaBrowser.Controller/Library/ILibraryManager.cs ; echo "exit=$?"
    exit=1

So a pass cannot ask the server to hold an item still. That is a search over a
vocabulary rather than a reading of every member: a facility that takes an item
exclusively under none of those five words would not be found by it, and what
would find one is reading the interface through.

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

The next pass picking a deferred item up is held, and the half that is missing
is smaller than this paragraph used to say. A deferred item is not recorded as
finished with - the record is written from the applier's answer rather than from
the loop having reached the end of an item - so a pass over the same pairing
considers it again. `PassResumptionTests` drives a write path that defers
everything and asserts both halves of that in one case: the count the result
carries, and a progress record with nothing in it. The count is a member of the
pass result, `ItemsDeferred`, rather than only the answer the apply returned.

What is absent is a pass that starts by itself. Nothing schedules one and
nothing reads two servers into a plan, so the pickup is proved against a
substituted write path in the suite and has never run on a library. That is a
negative disclosure and it stays one: what is proved is the record the applier's
answer leaves, never that an operator would see a deferred item come back.

This document does not claim the window is closed. What is left after the
re-read is the interval between the comparison and the write, this plugin cannot
make it zero, and a pass that reported otherwise would be reporting on a race it
lost. The suite drives an item that moves between planning and applying and
asserts the deferral, on one thread and between two statements, so what is proved
is the comparison and the counting and never a race.

## A plan can be had without anything that writes

A first release plans and does not write, which is decision 8 in #1 answered on
2026-08-09. So the route that produces a plan is the route that ships, and the
one that applies is asked for afterwards.

**It is a type of its own and not a method beside the one that writes.** A route
that held an applier and declined to use it would read the same way in a diff and
would be one edit from writing, so what is held is not an intention but a
reachability: nothing an applier is made of is reachable from `DryRun` at all,
which `DryRunTests` asks of the compiled assembly. That question can only be put
to a type, because the walk is seeded at one and reads every method it declares -
a plan-only method sitting beside a method that applies is inside one subject and
the two cannot be told apart. The types it refuses are the applier, the interface
it writes through, the implementation behind that interface, the record of what
was written, and the server's library and item underneath them.

**The plan an operator reads is the plan an apply carries out, by construction.**
`Pass` asks `DryRun` for the plan rather than deriving one of its own, so the two
are the same object and cannot disagree. A second derivation beside it would be
the copy that goes on describing a pass this tree has stopped making, which is
the failure `docs/reconciliation.md` has already paid for once at its own opening
paste. It is held rather than argued: a plan is derived, a pass is run over the
same request, and what the write path was handed is compared with what the plan
named.

**What a dry run leaves out is what an earlier pass finished with.** The skip an
interrupted pass creates is derived in one place and both routes read it, so a
plan taken after an interruption describes the resume rather than a first pass.
The count of what was passed over travels beside the plan, because a plan is what
is left to decide and a reader handed only that cannot tell a library with
nothing to change from one a pass has already been most of the way through.

**What it does not do.** It reads neither server, which happens before a request
exists. It takes no clock, so a plan carries no age and nothing refuses one for
being old - that is the fourth condition of #36 and it needs a number nobody has
chosen. And nothing constructs it, so an operator cannot ask for a plan: the
surface that would hand one over is M7, and the form it would travel in is the
second condition of the same issue.

## A pass that was stopped is continued

An interrupted pass leaves a record of the items it had finished with, and the
pass that runs next over the same pairing does not consider them again. The
record is a store of its own, `pass-progress.jsonl`, and what it is and what it
costs is argued in `docs/storage.md` rather than restated here.

**It resumes and it never replays.** What survives an interruption is a set of
item identifiers and nothing that could be obeyed. The items are observed again
by whoever runs the pass, the plan for what is left is built again by the
planner, and the values written are the ones the two servers hold when the
resumed pass runs. A plan stored at the interruption and replayed afterwards
would write the value the peer held before the interruption over a value the peer
has since changed, which is the failure this shape exists against. Nothing in
this plugin serialises a plan, and `PassResumptionTests` refuses a store made of
one rather than leaving the sentence to be trusted.

**The items are applied one at a time.** That is the whole reason a pass drives
the applier instead of calling it once with everything: the moment between an
item's write and the record that the item is done has to be somewhere a resume
can reason about, and inside a loop over a whole plan it is nowhere at all. The
accounting is the applier's four numbers summed, plus one a plan cannot carry -
how many items this pass never considered because an earlier one had finished
with them.

**What it does not promise is that an interruption is free.** The record is
written after the item was written and after what was written was recorded, so an
interruption in between costs that item being written a second time when the pass
resumes. The other ordering costs a library left unsynced with nothing saying so,
and the choice between them is not close. `docs/storage.md` states the same
residual where the record is argued.

**What a stopped pass reports depends on what stopped it.** An operator who
cancels a pass gets an exception where it was stopped and no result at all: they
asked for that, and there is nobody left to hand a value to. A pass that ran out
of time gets a result. Both keep what they recorded, so the pass after either one
continues rather than starting the library again. Turning a cancellation into a
result is still #37.

## A pass stops when its time is up

A pass over a large library, on a server that is also doing something else, ends
when it has done everything or when it has been running long enough. The second
of those is a bound and it is the same shape as the page size above: a named
constant with a default, in configuration, with a stated maximum the
configuration cannot exceed.

`PluginConfiguration.MinutesPerPass` is where an operator says how long,
`MinutesPerPassDefault` is what they get if they say nothing, and
`MinutesPerPassMaximum` is the most they may ask for. The numbers are not
restated here, for the reason the page size's are not: the suite reads all three
and a copy in this page would be a second answer that drifts against them.
`ConfigurationValidation` refuses a bound outside the range and names the
property in the sentence, so a number an operator can save is a number a pass can
read.

**Stopping is not failing, and the difference is the whole of the design.** A
pass that reaches the bound stops at an item boundary and returns; it does not
throw. The result says the pass did not finish, and the counts beside that say
what it got through, which is the value #37 observes a stopped pass never handed
anybody. The record of which items this pass finished with is kept rather than
cleared, so the pass that runs next continues from there. Only a pass that
reached the end of its plan clears it, and routing the stopped path through that
same exit is the one-line mistake this section is written against: the next pass
would start the library again with nothing saying so.

**The boundary is an item, because an item is the only boundary a resume has a
name for.** The record that an item is finished with is written after the applier
has returned from it, so a pass stopped anywhere inside an item would be stopped
at a point the resume cannot reason about. The bound is therefore read before
each item and never during one, and it is read against a start taken once before
the loop: a start re-read inside it would measure the last item instead of the
pass, which is a bound nothing can reach.

**The range is refused at both ends and clamped at neither**, for the reason the
page size's ends are. The lower end is the one worth reading. A pass allowed no
time reaches its bound before its first item, so it stops having written nothing,
on every run and for ever, and the symptom is a plugin that is configured, runs,
refuses nothing and changes no library. It is refused twice, in two senses:
`ConfigurationValidation` refuses the file an operator saved, and `Pass` refuses
the argument it is handed, because a caller inside this plugin is not an operator
and does not come through the configuration. The maximum is refused in the first
place only, since it bounds what an operator may express rather than what the
type can do.

**The clock is handed to the pass rather than read by it.** This plugin holds no
ambient clock and the bound did not add one. `Pass` takes a time source, and the
elapsed time it measures is one machine's, held against a number out of the
configuration and against nothing at all on the peer. That is a different subject
from the invariant which refuses a stamp from one server compared against the
other's, and the invariant's own rule now carries the two spellings a clock
arrives under, with an allowance naming this file and the reason, so the decision
is one a reader meets rather than an absence. What it costs is under what holds
this up, below.

There is one constructor and no overload without the bound. An unbounded overload
beside a bounded one would leave every existing caller on the unbounded path,
which is a bound that exists and is not in force.

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
catch every failure reddens the one that says a defect stops the pass. The next
pass picking a deferred item up is held where the deferral is argued, under
`## The window between planning and applying` above, by the leg that asserts a
deferred item leaves no record that it was finished with.

Two allowances have been added to the invariant lint, and they are named here
because a lint quietly narrowed is worse than one that reds. The rule that
refuses a timestamp compares a stamp from one server against a stamp from the
other. The file that writes reads this server's stamp and holds it against this
server's own earlier reading. The file that runs a pass reads a clock to measure
how long the pass has been going, which is one machine's elapsed time compared
against a setting and against nothing on the peer; the rule's pattern gained the
two spellings a clock arrives under in the same change, because until then the
rule's own record said an injected clock spelled none of them, and a bound
arriving under such a spelling would have been a clock in this plugin that
nothing recorded. In both cases the rule's record carries the file and the
reason, the rule still refuses that text from anywhere else, and the suite
refuses an allowance naming a file that is not in the tree.

The time bound is refused by a machine as well, and each half was proved by
breaking it. Routing the stopped path through the exit that clears the resume
point reddens the case that asserts a stopped pass keeps it and the case that
asserts the pass after it covers only the remainder. Deleting that exit
altogether reddens the neighbour that asserts a finished pass still clears it,
along with three cases about resumption that have nothing to do with the bound.
Re-reading the start inside the loop reddens four, including the one that counts
how many times the clock was read. All three were run.

An item being the unit is held by a pair rather than by one test. A plan whose
third row is refused is asserted to leave the item holding what it held, and the
same three rows with every one of them readable are asserted to all arrive, so
the first is not satisfied by a path that writes nothing. Collapsing the two
loops back into one reddens the first and leaves the second green; a writer that
stops assigning reddens the second. Both were run.

The read is refused by a machine too, and each half was proved by breaking it.
A reader that asked the server for everything reddens the leg that reads the
answer and the leg that reads the ask, and the second is the one that would
still red on the day a filter afterwards happened to be right. An empty
participating set turned into a query reddens the leg that asserts the server is
asked nothing at all. A set held by reference instead of copied reddens the leg
that changes the selection under a reader that already exists.

The proxy those legs run against answers an unbounded query with everything it
holds, exactly as the query means, and a leg asserts that it does. Without it
the rule above would pass against an arrangement that quietly narrowed for the
reader, which is a test asserting its own fixture.

One more thing this section said nothing held, and it is held. A pass that
reaches its time bound stops at an item boundary and hands back a result rather
than throwing: `PassResult` carries a required member saying the pass did not
finish, and the counts beside it are what it got through. That is the value #37
asks a stopped pass to return, and `## A pass stops when its time is up` above
is where the legs behind it are named rather than here. What still hands nothing
back is a pass an operator cancelled, which throws where it was stopped because
the caller asked for that and there is nobody left to hand a result to. What #38
landed is the record on the disk, and the record and the result answer two
different questions: what the next pass may pass over, and what this one did.

The members that paragraph rests on, and the one the deferral above rests on,
are spellings rather than judgements, so they are held the way the absences under
`## What exists, and what still does not` are and in the opposite direction. Each
line below is one `ReconciliationStatementTests` looks for in the plugin's own
sources, and the claim is that it finds every one of them, so a member renamed
out from under a paragraph reds instead of leaving it describing a type that has
moved.

<!-- the spellings this page says the plugin's sources carry: one per line, the spelling first, read by ReconciliationStatementTests -->

- `public required bool Finished`, the member a pass that stopped at its bound
  answers with
- `MinutesPerPass`, the bound it stops at
- `ItemsDeferred`, the count a pass result carries for the items it was kept
  away from

<!-- end of the spellings the sources carry -->

What this list cannot reach is the bound the absence list states about itself,
pointing the other way. A line deleted from it stops being checked, so narrowing
it is a change to what this page claims rather than a failure. And a spelling
found anywhere in the plugin's sources satisfies it, so what is proved is that
the name is in the tree and never that this paragraph describes what it does.

The resumption is held by legs of its own. A pass that stops at each boundary
its loop has is run to that boundary and then resumed, and the union of the two
runs is asserted to be exactly the plan with no item written twice. Recording an
item before its write instead of after it reddens the leg that kills a pass
between the two. Clearing the record at the start of a pass rather than at its
end reddens the leg that asserts a finished pass leaves nothing behind, and
skipping the clear altogether reddens the leg that asserts the pass after a
finished one considers every item again. A resumed pass that reused the plan the
first one built reddens the leg that changes a peer value in between.
