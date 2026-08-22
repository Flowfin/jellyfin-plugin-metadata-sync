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
/// expected it to write, and every leg here is one way that happens: a field
/// name the server does not have, a value taken from the media file, a document
/// that says something other than the register, and a caller asking for a field
/// that was never declared at all.
/// <para>
/// What is asserted elsewhere, and deliberately not here. Which fields the
/// writing code can reach, held against the rows that move in both directions,
/// is <see cref="LibraryPlanTargetTests.TheWritersAndTheRefusedFieldsAreExactlyWhatTheRegisterLetsMove"/>,
/// because that is the path a pass takes. Every lock the server declares is
/// held against the code that decides, in <see cref="LockedFieldPlanTests"/>.
/// Both used to be covered here as well, through a second writer this plugin no
/// longer carries, and a lock proved on a call no pass makes proves nothing
/// about the pass.
/// </para>
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
    /// A field with no row is refused when it is asked for. The refusal is at
    /// run time and not only in review, because review is where the last one of
    /// these got through.
    /// </summary>
    /// <remarks>
    /// <c>SortName</c> is the field used here because it is the near-miss
    /// somebody actually writes: it is a real property on the item, the
    /// register carries <c>ForcedSortName</c> beside it, and the two are the
    /// same idea spelled two ways. A register read leniently would answer for
    /// the row next door.
    /// </remarks>
    [Fact]
    public void AFieldWithNoRowIsRefusedWhenSomethingAsksToMoveIt()
    {
        Assert.NotNull(typeof(BaseItem).GetProperty("SortName"));
        Assert.Null(FieldRegister.Find("SortName"));

        var refused = Assert.Throws<FieldNotDeclaredException>(
            () => FieldRegister.RequireMovable("SortName"));

        Assert.Contains("SortName", refused.Message, StringComparison.Ordinal);
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
            () => FieldRegister.RequireMovable("RunTimeTicks"));

        Assert.Contains(row.Reason, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every image field the item type carries has a row, and no row that has
    /// one moves. Image bytes are a permanent non-goal rather than a scope
    /// decision, so the register declares the refusal instead of leaving it to
    /// the absence of a row.
    /// </summary>
    /// <remarks>
    /// The set is read back off the server rather than written here, which is
    /// what makes this catch the case it exists for: an image field the server
    /// adds later arrives with no row, and a register that is silent about it
    /// reads exactly like one that refuses it. What it cannot catch is an image
    /// reached through something that is not a property on the item, which is
    /// the surface <c>ImageBytesTests</c> refuses by name instead.
    /// <para>
    /// Two properties spelled with the word are allowed to have no row, and
    /// they are named below with the reason rather than filtered out by a
    /// pattern. A pattern excusing everything beginning with <c>Supports</c>
    /// would excuse the next such name too, and the point of the leg is that a
    /// name arriving later is decided by somebody rather than by a prefix.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryImageFieldOnTheItemTypeHasARowAndNoneOfThemMove()
    {
        // Neither carries an image or a location of one. They answer what this
        // item kind is capable of, which is a property of the type rather than
        // a value that could be written onto one item, and there is nothing for
        // a row to declare about either.
        var notAField = new[] { "SupportsInheritedParentImages", "SupportsRemoteImageDownloading" };

        var onTheServer = typeof(BaseItem)
            .GetProperties()
            .Select(p => p.Name)
            .Where(n => n.Contains("Image", StringComparison.Ordinal))
            .Where(n => !notAField.Contains(n, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(onTheServer);

        var undeclared = onTheServer.Where(n => FieldRegister.Find(n) is null).ToList();
        var moving = onTheServer.Where(n => FieldRegister.Find(n)?.Moves == true).ToList();

        Assert.Empty(undeclared);
        Assert.Empty(moving);

        // The excused names are the server's own. A line that renames one turns
        // the exclusion above into a name nothing matches, which would silently
        // widen what this leg is looking at rather than narrowing it.
        var properties = typeof(BaseItem).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.All(notAField, n => Assert.Contains(n, properties));
    }

    /// <summary>
    /// Asking to move an image field is refused when it is asked, with the row's
    /// own reason rather than with a general one, so the caller reads the
    /// argument instead of going to look for a missing row.
    /// </summary>
    [Theory]
    [InlineData("ImageInfos")]
    [InlineData("PrimaryImagePath")]
    public void AnImageFieldIsRefusedWithTheReasonItsRowCarries(string field)
    {
        var row = FieldRegister.Find(field);
        Assert.NotNull(row);
        Assert.False(row.Moves);

        var refused = Assert.Throws<FieldNotDeclaredException>(
            () => FieldRegister.RequireMovable(field));

        Assert.Contains(row.Reason, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every field the server's per-user record carries has a row, and none of
    /// them moves. What a server holds about a person is the sibling plugin's
    /// work and never this one's, and the register says so by name rather than
    /// by having nothing to say.
    /// </summary>
    /// <remarks>
    /// The set is read back off the server's own record type rather than
    /// written here, so a server line adding a tenth field turns this red
    /// instead of leaving the register quietly incomplete. Two members are
    /// excused by name below because they identify the record rather than
    /// describe anybody's viewing.
    /// <para>
    /// This is a register leg and not a reachability one. That the plugin
    /// assembly cannot name the surface at all is <c>UserDataSurfaceTests</c>,
    /// and neither leg makes the other redundant: one keeps the code away from
    /// the data, this one keeps the document honest about what was decided.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryFieldOnThePerUserRecordHasARowAndNoneOfThemMove()
    {
        // The key is what the record is filed under. It says nothing about what
        // somebody watched, and there is nothing for a row to declare about a
        // record's own identity. The user was excused here too until the
        // reference moved to the supported line, where the record no longer
        // carries it: the identity of the person is on the row in the database
        // rather than on this type, so excusing a member this type does not
        // have would be an excuse nothing needs.
        var notAField = new[] { "Key" };

        var record = ResolveServerType("MediaBrowser.Controller.Entities.UserItemData");
        Assert.NotNull(record);

        var onTheServer = record.GetProperties()
            .Select(p => p.Name)
            .Where(n => !notAField.Contains(n, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(onTheServer);

        var undeclared = onTheServer.Where(n => FieldRegister.Find(n) is null).ToList();
        var moving = onTheServer.Where(n => FieldRegister.Find(n)?.Moves == true).ToList();

        Assert.Empty(undeclared);
        Assert.Empty(moving);

        var properties = record.GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.All(notAField, n => Assert.Contains(n, properties));
    }

    /// <summary>
    /// Asking to move a user-scoped field is refused when it is asked, with the
    /// row's own reason. The personal rating is the row this group exists for,
    /// so it is asserted beside the community rating that does move.
    /// </summary>
    [Theory]
    [InlineData("Played")]
    [InlineData("PlaybackPositionTicks")]
    [InlineData("IsFavorite")]
    [InlineData("Rating")]
    public void AUserScopedFieldIsRefusedWithTheReasonItsRowCarries(string field)
    {
        var row = FieldRegister.Find(field);
        Assert.NotNull(row);
        Assert.False(row.Moves);

        var refused = Assert.Throws<FieldNotDeclaredException>(
            () => FieldRegister.RequireMovable(field));

        Assert.Contains(row.Reason, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The near-miss the personal rating exists next to. A register that read
    /// the two ratings as one row would refuse the item's own community rating
    /// under the per-user reason, or move the per-user one under the item's.
    /// They are separate rows, on separate types, and only one of them is about
    /// a person.
    /// </summary>
    [Fact]
    public void ThePersonalRatingAndTheCommunityRatingAreSeparateRows()
    {
        var personal = FieldRegister.Find("Rating");
        var community = FieldRegister.Find("CommunityRating");

        Assert.NotNull(personal);
        Assert.NotNull(community);
        Assert.Equal("MediaBrowser.Controller.Entities.UserItemData", personal.DeclaredOn);
        Assert.Equal("MediaBrowser.Controller.Entities.BaseItem", community.DeclaredOn);
        Assert.NotEqual(personal.Reason, community.Reason);
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
    /// The neighbour both refusals above differ from by one thing. A field the
    /// register declares as moving is answered with its own row rather than
    /// refused, so neither refusal is passing because the register refuses
    /// everything it is asked.
    /// </summary>
    [Fact]
    public void ADeclaredFieldThatMovesIsAnsweredWithItsOwnRow()
    {
        var row = FieldRegister.RequireMovable("Overview");

        Assert.Equal("Overview", row.Field);
        Assert.True(row.Moves);
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

    private static Type? ResolveServerType(string name)
    {
        return typeof(BaseItem).Assembly.GetType(name)
            ?? typeof(BaseItemKind).Assembly.GetType(name);
    }
}
