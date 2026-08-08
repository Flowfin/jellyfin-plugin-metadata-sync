using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Fields;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the field register up as the only place a field is declared.
/// </summary>
/// <remarks>
/// The single most destructive thing this plugin can do is write a field nobody
/// expected it to write, and every leg here is one way that happens: a writer
/// that no row declares, a row that moves with nothing behind it, a field name
/// the server does not have, a value taken from the media file, a document that
/// says something other than the register, and a caller asking for a field that
/// was never declared at all.
/// </remarks>
public class FieldRegisterTests
{
    /// <summary>
    /// A register whose one row names a kind group nothing declares. The refusal
    /// register runs the same text through the same seam, because naming a test
    /// says what a caller saw and never which line refused, and a second copy of
    /// the text would be a copy that drifts.
    /// </summary>
    internal const string UndeclaredKindGroupRegister = """
        {
          "kindGroups": { "all": ["Movie"] },
          "rows": [
            {
              "field": "Overview",
              "declaredOn": "MediaBrowser.Controller.Entities.BaseItem",
              "kinds": "everything",
              "moves": true,
              "class": "Descriptive",
              "fromTheFile": false,
              "reason": "A row whose kind group the register never declares."
            }
          ]
        }
        """;

    /// <summary>
    /// A register whose one row names a lock the server does not have. The
    /// near-miss is a real field name that is not a lockable one:
    /// <c>Description</c> is what somebody writes for the overview, and the
    /// server's lockable set spells it <c>Overview</c>.
    /// </summary>
    internal const string LockTheServerDoesNotHaveRegister = """
        {
          "kindGroups": { "all": ["Movie"] },
          "rows": [
            {
              "field": "Overview",
              "declaredOn": "MediaBrowser.Controller.Entities.BaseItem",
              "kinds": "all",
              "moves": true,
              "lock": "Description",
              "class": "Descriptive",
              "fromTheFile": false,
              "reason": "A row naming a lock the server does not let an operator set."
            }
          ]
        }
        """;

    /// <summary>
    /// A row with no reason is a row nobody can argue with later, which makes
    /// the register a list instead of a set of decisions.
    /// </summary>
    [Fact]
    public void EveryRowCarriesAReason()
    {
        Assert.NotEmpty(FieldRegister.Rows);

        var without = FieldRegister.Rows
            .Where(r => string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length < 40)
            .Select(r => r.Field)
            .ToList();

        Assert.Empty(without);
    }

    /// <summary>
    /// The register names fields as the server names them. A row naming a
    /// property the server does not have is a row that can never be right, and
    /// it is also what a server line renaming a property looks like from here.
    /// </summary>
    [Fact]
    public void EveryFieldNamedIsOneTheServerActuallyHas()
    {
        var unresolved = new List<string>();

        foreach (var row in FieldRegister.Rows)
        {
            if (row.DeclaredOn is null)
            {
                // A field that is not a property on an item owes the interface it
                // is reached through instead, so no row is allowed to name nothing.
                if (row.ReachedBy is null || ResolveServerType(row.ReachedBy) is null)
                {
                    unresolved.Add(row.Field + " (reached through " + (row.ReachedBy ?? "nothing") + ")");
                }

                continue;
            }

            Assert.Null(row.ReachedBy);

            var declaring = ResolveServerType(row.DeclaredOn);
            if (declaring is null || declaring.GetProperty(row.Field) is null)
            {
                unresolved.Add(row.Field + " on " + row.DeclaredOn);
            }
        }

        Assert.Empty(unresolved);
    }

    /// <summary>
    /// The kind column is a controlled vocabulary and the server owns it. A
    /// group naming a kind the server does not have is a row that applies to
    /// nothing, and it reads as one that applies to something.
    /// </summary>
    [Fact]
    public void EveryItemKindNamedIsOneTheServerActuallyHas()
    {
        var kinds = Enum.GetNames<BaseItemKind>().ToHashSet(StringComparer.Ordinal);

        var unknown = FieldRegister.KindGroups
            .SelectMany(g => g.Value)
            .Where(k => !kinds.Contains(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(unknown);
        Assert.All(FieldRegister.Rows, r => Assert.NotEmpty(r.Kinds));
    }

    /// <summary>
    /// Collection membership and playlist membership are out of 1.0, and this
    /// is where that stops being a sentence in a document. A kind group naming
    /// <c>BoxSet</c> or <c>Playlist</c> makes every row that names the group
    /// declare a field that moves on an item kind this release does not carry.
    /// </summary>
    /// <remarks>
    /// The near-miss is a container kind added beside the container kinds that
    /// are already there: <c>MusicAlbum</c> and <c>MusicArtist</c> sit in the
    /// <c>all</c> group today, so <c>BoxSet</c> next to them reads like the
    /// same sort of entry and is the one somebody adds without deciding
    /// anything. What this cannot catch is a collection reached through a
    /// field rather than through a kind, because the register has no column
    /// that would say so, and it cannot catch a kind the server adds later for
    /// the same idea under another name.
    /// </remarks>
    [Fact]
    public void NoKindGroupNamesACollectionOrAPlaylist()
    {
        var outOfScope = new[] { "BoxSet", "Playlist" };

        var naming = FieldRegister.KindGroups
            .SelectMany(g => g.Value.Select(k => new { Group = g.Key, Kind = k }))
            .Where(e => outOfScope.Contains(e.Kind, StringComparer.Ordinal))
            .Select(e => e.Group + " names " + e.Kind)
            .ToList();

        Assert.Empty(naming);

        // The two names above are the server's own. A line that renames either
        // one turns this guard into a scan for a string nothing can match, so
        // the vocabulary is read back out of the server rather than trusted.
        var kinds = Enum.GetNames<BaseItemKind>().ToHashSet(StringComparer.Ordinal);
        Assert.All(outOfScope, k => Assert.Contains(k, kinds));
    }

    /// <summary>
    /// This is the leg the register exists for. The set of fields the writing
    /// code can reach is compared with the set of rows that declare a field
    /// moves, in both directions, so a writer added without a row fails and a
    /// row that moves with nothing behind it fails too.
    /// </summary>
    [Fact]
    public void TheFieldsTheMoverCanWriteAreExactlyTheRowsThatMove()
    {
        var declared = FieldRegister.Rows.Where(r => r.Moves).Select(r => r.Field).ToHashSet(StringComparer.Ordinal);
        var writable = FieldMover.WritableFields.ToHashSet(StringComparer.Ordinal);

        var writesWithoutARow = writable.Except(declared, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var rowsWithoutAWriter = declared.Except(writable, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.Empty(writesWithoutARow);
        Assert.Empty(rowsWithoutAWriter);
    }

    /// <summary>
    /// A field with no row is refused when it is asked for. The refusal is at
    /// run time and not only in review, because review is where the last one of
    /// these got through.
    /// </summary>
    [Fact]
    public void AFieldWithNoRowIsRefusedWhenSomethingAsksToMoveIt()
    {
        var from = new Movie();
        var to = new Movie();

        Assert.Null(FieldRegister.Find("PlaybackPositionTicks"));

        var refused = Assert.Throws<FieldNotDeclaredException>(
            () => FieldMover.Move("PlaybackPositionTicks", from, to));

        Assert.Contains("PlaybackPositionTicks", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A field that has a row saying it does not move is refused for that
    /// reason, and the refusal quotes the row. A caller told only that a field
    /// is unknown would go looking for a missing row that is right there.
    /// </summary>
    [Fact]
    public void AFieldWhoseRowRefusesToMoveIsRefusedWithItsReason()
    {
        var row = FieldRegister.Find("RunTimeTicks");
        Assert.NotNull(row);
        Assert.False(row.Moves);

        var refused = Assert.Throws<FieldNotDeclaredException>(
            () => FieldMover.Move("RunTimeTicks", new Movie(), new Movie()));

        Assert.Contains(row.Reason, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A field derived from the media file describes this server's copy, so the
    /// peer's value is a false statement about ours whatever else is true of it.
    /// No row in that class may move, ever.
    /// </summary>
    [Fact]
    public void NothingDerivedFromTheMediaFileMoves()
    {
        var moving = FieldRegister.Rows.Where(r => r.FromTheFile && r.Moves).Select(r => r.Field).ToList();

        Assert.Empty(moving);
        Assert.Contains(FieldRegister.Rows, r => r.FromTheFile);
    }

    /// <summary>
    /// The class derived from the media file is empty of anything a caller can
    /// enable, because a per-installation switch on one of those fields is the
    /// setting that breaks a library. The configuration is where such a switch
    /// would appear, so that is what is read.
    /// </summary>
    [Fact]
    public void NoConfigurationSettingCanEnableAFieldTakenFromTheMediaFile()
    {
        var fromTheFile = FieldRegister.Rows.Where(r => r.FromTheFile).Select(r => r.Field).ToList();
        Assert.NotEmpty(fromTheFile);

        var settings = typeof(PluginConfiguration)
            .GetProperties()
            .Where(p => p.DeclaringType == typeof(PluginConfiguration))
            .Select(p => p.Name)
            .ToList();

        var enabling = settings
            .Where(s => fromTheFile.Any(f => s.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(enabling);
    }

    /// <summary>
    /// The nine names the server lets an operator lock are each claimed by
    /// exactly one row, in both directions. A lock naming no row refuses
    /// nothing, and a second row under one lock makes the answer to "may this
    /// field be written" depend on which row was read.
    /// </summary>
    [Fact]
    public void EachOfTheNineLockableNamesGovernsExactlyOneRow()
    {
        var server = Enum.GetNames<MetadataField>().ToHashSet(StringComparer.Ordinal);

        var claimed = FieldRegister.Rows
            .Where(r => r.Lock is not null)
            .GroupBy(r => r.Lock!.Value.ToString(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Field).ToList(), StringComparer.Ordinal);

        var unclaimed = server.Except(claimed.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var claimedTwice = claimed.Where(p => p.Value.Count > 1).Select(p => p.Key).Order(StringComparer.Ordinal).ToList();

        Assert.Empty(unclaimed);
        Assert.Empty(claimedTwice);
    }

    /// <summary>
    /// A field the operator locked is not written. Locked on exactly the field
    /// being written rather than on everything, because a fixture that locked
    /// the whole set would pass against a mover that read the wrong entry.
    /// </summary>
    /// <remarks>
    /// Five of the nine lockable names govern a row that moves and are reachable
    /// here. The other four, <c>Genres</c>, <c>Studios</c>, <c>Cast</c> and
    /// <c>Runtime</c>, govern rows the register refuses to move at all, so the
    /// register refuses before the lock is ever read. That is a stronger refusal
    /// and not a gap, and it is why this covers five names rather than nine:
    /// asserting a lock on a field nothing can write would be asserting about a
    /// line no arrangement reaches.
    /// </remarks>
    [Theory]
    [InlineData("Name", MetadataField.Name)]
    [InlineData("Overview", MetadataField.Overview)]
    [InlineData("Tags", MetadataField.Tags)]
    [InlineData("ProductionLocations", MetadataField.ProductionLocations)]
    [InlineData("OfficialRating", MetadataField.OfficialRating)]
    public void AFieldTheOperatorLockedIsNotWritten(string field, MetadataField governing)
    {
        var row = FieldRegister.Find(field);
        Assert.NotNull(row);
        Assert.Equal(governing, row.Lock);

        var from = new Movie();
        var to = new Movie { LockedFields = new[] { governing } };
        var before = Read(to, field);

        var refused = Assert.Throws<FieldLockedException>(() => FieldMover.Move(field, from, to));

        Assert.Contains(governing.ToString(), refused.Message, StringComparison.Ordinal);
        Assert.Equal(before, Read(to, field));
    }

    /// <summary>
    /// The near-miss for the lock. The same item, locked on one field, and the
    /// field beside it written. A mover that read the lock set as a single
    /// answer for the whole item would refuse this too.
    /// </summary>
    [Fact]
    public void AFieldLockedOnAnotherRowDoesNotRefuseThisOne()
    {
        var from = new Movie { Overview = "What the peer says about it" };
        var to = new Movie { Overview = "ours", LockedFields = new[] { MetadataField.Name } };

        FieldMover.Move("Overview", from, to);

        Assert.Equal("What the peer says about it", to.Overview);
    }

    /// <summary>
    /// An operator who locked the item said nothing on it is ours, so no field
    /// is written, including the ones the server has no field-level lock for.
    /// </summary>
    /// <remarks>
    /// <c>Tagline</c> is the field chosen here for that reason: it has no lock
    /// of its own, so it is the row that a mover checking only the nine names
    /// would write straight through an item the operator had locked outright.
    /// </remarks>
    [Fact]
    public void NoFieldIsWrittenOntoAnItemTheOperatorLocked()
    {
        var row = FieldRegister.Find("Tagline");
        Assert.NotNull(row);
        Assert.Null(row.Lock);

        var from = new Movie { Tagline = "what the peer says" };
        var to = new Movie { Tagline = "ours", IsLocked = true };

        var refused = Assert.Throws<FieldLockedException>(() => FieldMover.Move("Tagline", from, to));

        Assert.Contains("Tagline", refused.Message, StringComparison.Ordinal);
        Assert.Equal("ours", to.Tagline);
    }

    /// <summary>
    /// A row naming a lock the server does not have is refused when the register
    /// is read. Read leniently it would be a row that names no lock at all,
    /// which is the row that silently writes over an operator's decision.
    /// </summary>
    [Fact]
    public void ARowNamingALockTheServerDoesNotHaveIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => FieldRegister.Parse(LockTheServerDoesNotHaveRegister));

        Assert.Contains("Description", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ordinary case, and the neighbour every refusal below differs from by
    /// one thing. A declared field is written and the value arrives.
    /// </summary>
    [Fact]
    public void ADeclaredFieldIsWrittenOntoTheItem()
    {
        var from = new Movie { Overview = "What the peer says about it" };
        var to = new Movie { Overview = "What this server says about it" };

        FieldMover.Move("Overview", from, to);

        Assert.Equal("What the peer says about it", to.Overview);
    }

    /// <summary>
    /// There is nothing to take a value from. Writing a field off an item that
    /// is not there would write whatever the default is, silently.
    /// </summary>
    [Fact]
    public void MovingFromAnItemThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => FieldMover.Move("Overview", null!, new Movie()));
    }

    /// <summary>
    /// There is nothing to write to. The same failure in the other direction,
    /// and the one that would otherwise be a null reference somewhere later.
    /// </summary>
    [Fact]
    public void MovingOntoAnItemThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => FieldMover.Move("Overview", new Movie(), null!));
    }

    /// <summary>
    /// The register that ships inside the assembly is the one the plugin runs
    /// on. This is the neighbour for the three loader refusals below.
    /// </summary>
    [Fact]
    public void TheRegisterThatShipsInTheAssemblyLoads()
    {
        var contents = FieldRegister.Load(FieldRegister.EmbeddedResourceName);

        Assert.NotEmpty(contents.Rows);
        Assert.NotEmpty(contents.KindGroups);
    }

    /// <summary>
    /// A build that did not embed the register is a build where no field is
    /// declared. Reading that as an empty register would make every field
    /// undeclared and every write refused, which looks like the plugin working.
    /// </summary>
    [Fact]
    public void ARegisterThatIsNotEmbeddedIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => FieldRegister.Load("Jellyfin.Plugin.MetadataSync.Fields.no-such-register.json"));

        Assert.Contains("no-such-register.json", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Register text that describes no register at all is refused rather than
    /// read as one with no rows.
    /// </summary>
    [Fact]
    public void RegisterTextThatDescribesNoRegisterIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => FieldRegister.Parse("null"));
    }

    /// <summary>
    /// A row naming a kind group nothing declares applies to no item kind. Read
    /// leniently it would be a row that applies to every kind, which is the
    /// opposite of what it says.
    /// </summary>
    [Fact]
    public void ARowNamingAKindGroupNothingDeclaresIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => FieldRegister.Parse(UndeclaredKindGroupRegister));

        Assert.Contains("everything", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// One row per field. Two rows for one field means one of them is the rule
    /// and the other is decoration, and nothing says which.
    /// </summary>
    [Fact]
    public void NoFieldHasTwoRows()
    {
        var repeated = FieldRegister.Rows
            .GroupBy(r => r.Field, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(repeated);
    }

    /// <summary>
    /// Reads a field back off an item by name, so a refusal can be asserted to
    /// have left the value alone rather than only to have thrown.
    /// </summary>
    private static object? Read(BaseItem item, string field)
    {
        var property = typeof(BaseItem).GetProperty(field);
        Assert.NotNull(property);

        var value = property.GetValue(item);
        return value is string[] strings ? string.Join(",", strings) : value;
    }

    private static Type? ResolveServerType(string name)
    {
        return typeof(BaseItem).Assembly.GetType(name)
            ?? typeof(BaseItemKind).Assembly.GetType(name);
    }
}
