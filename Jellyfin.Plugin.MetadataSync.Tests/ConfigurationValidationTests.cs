using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The configuration is refused when it says something this plugin cannot act
/// on, and every refusal names the property it is about.
/// </summary>
/// <remarks>
/// A configuration that can hold a nonsensical combination is a plugin that
/// acts on one, and the acting is against somebody's library. The failures
/// these cases are written for are the quiet ones: a library identifier left
/// behind after the library was removed, a field name that stopped being a
/// field, a direction read out of a file an older version wrote. None of those
/// looks wrong on the page, and each one makes a pass do less than the page
/// says it does.
/// <para>
/// The check is constructed here out of values alone. There is no library, no
/// server, no file and no substitute for any of them, because the set of
/// libraries is a parameter rather than something the validator goes and asks
/// for.
/// </para>
/// </remarks>
public class ConfigurationValidationTests
{
    private static readonly Guid AFilmLibrary = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AMusicLibrary = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ALibraryThatWasRemoved = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid APairing = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly IReadOnlyCollection<Guid> WhatTheServerHolds = new[] { AFilmLibrary, AMusicLibrary };

    /// <summary>
    /// A plugin installed and not yet configured. Nothing is chosen, nothing is
    /// acted on, and there is nothing to complain about. This is the neighbour
    /// every case below differs from by one thing.
    /// </summary>
    [Fact]
    public void ADefaultConfigurationHasNothingToRefuse()
    {
        Assert.Empty(ConfigurationValidation.Validate(new PluginConfiguration(), WhatTheServerHolds));
    }

    /// <summary>
    /// A configuration that is not there.
    /// </summary>
    [Fact]
    public void ValidatingAConfigurationThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ConfigurationValidation.Validate(null!, WhatTheServerHolds));
    }

    /// <summary>
    /// The range a participating library is checked against, absent. An empty
    /// server holds no libraries, which is a different statement from nobody
    /// having asked, and the second one is a caller that has not been written
    /// yet rather than a configuration that is wrong.
    /// </summary>
    [Fact]
    public void ValidatingAgainstALibrarySetThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ConfigurationValidation.Validate(new PluginConfiguration(), null!));
    }

    /// <summary>
    /// The case this rule exists for. Nobody edited the configuration; the
    /// library was removed and its identifier stayed behind.
    /// </summary>
    [Fact]
    public void ALibraryTheServerDoesNotHoldIsRefusedByName()
    {
        var configuration = Configured();
        configuration.ParticipatingLibraries.Add(ALibraryThatWasRemoved);

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.ParticipatingLibraries), problem.Property);
        Assert.Contains(ALibraryThatWasRemoved.ToString(), problem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The neighbour. One identifier different, and it is one the server holds.
    /// </summary>
    [Fact]
    public void ALibraryTheServerHoldsIsAccepted()
    {
        var configuration = Configured();
        configuration.ParticipatingLibraries.Add(AFilmLibrary);

        Assert.Empty(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));
    }

    /// <summary>
    /// The same library twice. It is what a page that appends a selection
    /// produces, and a pass that read it would visit the library twice.
    /// </summary>
    [Fact]
    public void TheSameLibraryTwiceIsRefused()
    {
        var configuration = Configured();
        configuration.ParticipatingLibraries.Add(AFilmLibrary);
        configuration.ParticipatingLibraries.Add(AFilmLibrary);

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.ParticipatingLibraries), problem.Property);
        Assert.Contains("more than once", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A direction this plugin does not declare. No page can produce it, which
    /// is the point: it arrives from a file somebody edited or from a version
    /// that declared more than this one does.
    /// </summary>
    /// <summary>
    /// A configuration written by a build that came after this one. It is the
    /// case #59 exists for and the only one here an operator cannot cause by
    /// editing anything: they downgraded, and the file on disk is a shape this
    /// build has never been told how to read.
    /// </summary>
    [Fact]
    public void AConfigurationShapeNewerThanThisBuildIsRefused()
    {
        var configuration = Configured();
        configuration.Format = ConfigurationFormat.Current + 1;

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.Format), problem.Property);
        Assert.Contains(
            (ConfigurationFormat.Current + 1).ToString(CultureInfo.InvariantCulture),
            problem.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A stamp naming no shape at all. It cannot arrive from the serialiser
    /// filling in a default, so something typed it, and reading it as the
    /// earliest shape is the generous assumption that acts on a file under rules
    /// it was not written under.
    /// </summary>
    [Fact]
    public void AConfigurationShapeThisBuildCannotPlaceIsRefused()
    {
        var configuration = Configured();
        configuration.Format = -1;

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.Format), problem.Property);
        Assert.Contains("-1", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The neighbour, and it is the state every configuration in existence is
    /// in: no stamp at all. It is read as the earliest shape rather than
    /// refused, because a plugin that has never been configured and a file
    /// written before the stamp existed both hold that shape.
    /// </summary>
    [Fact]
    public void AConfigurationCarryingNoStampHasNothingToRefuse()
    {
        var configuration = Configured();
        configuration.Format = ConfigurationFormat.Absent;

        Assert.Empty(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));
    }

    /// <summary>
    /// The other neighbour: the shape this build writes, named explicitly. A
    /// rule that refused it would refuse every configuration the first
    /// migration step produces.
    /// </summary>
    [Fact]
    public void TheShapeThisBuildReadsIsAccepted()
    {
        var configuration = Configured();
        configuration.Format = ConfigurationFormat.Current;

        Assert.Empty(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));
    }

    [Fact]
    public void ADirectionThisPluginDoesNotDeclareIsRefused()
    {
        var configuration = Configured();
        configuration.Direction = (SyncDirection)7;

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.Direction), problem.Property);
        Assert.Contains("7", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The neighbour, one step away: the value the enum actually declares.
    /// </summary>
    [Fact]
    public void TheDeclaredDirectionIsAccepted()
    {
        var configuration = Configured();
        configuration.Direction = SyncDirection.TwoWay;

        Assert.Empty(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));
    }

    /// <summary>
    /// A field name the register does not carry. The near miss is deliberate:
    /// the server calls it Overview and this is the name somebody reaches for.
    /// </summary>
    [Fact]
    public void AFieldTheRegisterDoesNotDeclareIsRefused()
    {
        var configuration = Configured();
        configuration.ExcludedFields.Add("Description");

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.ExcludedFields), problem.Property);
        Assert.Contains("Description", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A field the register carries and already refuses to move. Excluding it
    /// changes nothing, and a setting that changes nothing while reading like
    /// one that does is how an operator comes to believe a field is theirs
    /// because they turned it off.
    /// </summary>
    [Fact]
    public void AFieldTheRegisterAlreadyRefusesToMoveIsRefused()
    {
        var configuration = Configured();
        configuration.ExcludedFields.Add("Genres");

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.ExcludedFields), problem.Property);
        Assert.Contains("Genres", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The neighbour for both field cases. A declared field that does move is
    /// exactly what this setting is for.
    /// </summary>
    [Fact]
    public void AFieldTheRegisterMovesMayBeExcluded()
    {
        var configuration = Configured();
        configuration.ExcludedFields.Add("Overview");

        Assert.Empty(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));
    }

    /// <summary>
    /// The same field twice.
    /// </summary>
    [Fact]
    public void TheSameFieldTwiceIsRefused()
    {
        var configuration = Configured();
        configuration.ExcludedFields.Add("Overview");
        configuration.ExcludedFields.Add("Overview");

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.ExcludedFields), problem.Property);
        Assert.Contains("more than once", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Settings that express work, with nobody to do it with. An operator who
    /// picked libraries and never finished choosing a pairing has a
    /// configuration that looks complete on the page.
    /// </summary>
    [Fact]
    public void NamingLibrariesWithNoPairingChosenIsRefused()
    {
        var configuration = new PluginConfiguration();
        configuration.ParticipatingLibraries.Add(AFilmLibrary);

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.PairingId), problem.Property);
    }

    /// <summary>
    /// The same for the other half of what counts as expressing work, so the
    /// rule is not read as one about libraries alone.
    /// </summary>
    [Fact]
    public void NamingExcludedFieldsWithNoPairingChosenIsRefused()
    {
        var configuration = new PluginConfiguration();
        configuration.ExcludedFields.Add("Overview");

        var problem = Assert.Single(ConfigurationValidation.Validate(configuration, WhatTheServerHolds));

        Assert.Equal(nameof(PluginConfiguration.PairingId), problem.Property);
    }

    /// <summary>
    /// Every problem says which property it is about, twice: once as a field a
    /// page can read, and once inside the sentence an operator reads. A message
    /// that leaves the property out is one an operator cannot act on, which is
    /// the whole of what this issue asks for.
    /// </summary>
    [Fact]
    public void EveryProblemNamesItsPropertyInTheSentenceAsWell()
    {
        foreach (var problem in EveryProblemThisPluginCanReport())
        {
            Assert.Contains(problem.Property, problem.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The rule that survives the configuration growing. Every property the
    /// type declares is reachable by at least one refusal, so a property added
    /// with no validation rule fails here rather than shipping as a surface an
    /// operator can set into a state nothing checks.
    /// </summary>
    /// <remarks>
    /// It is derived from the type rather than written as a list beside it. A
    /// list would go on passing the day a sixth property is added, which is the
    /// only day it matters.
    /// </remarks>
    [Fact]
    public void EveryConfigurationPropertyIsCarriedByARuleThatCanRefuseIt()
    {
        var refusable = EveryProblemThisPluginCanReport()
            .Select(problem => problem.Property)
            .ToHashSet(StringComparer.Ordinal);

        var uncovered = typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .Where(name => !refusable.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(uncovered);
    }

    /// <summary>
    /// A configuration wrong in every way at once answers with every problem
    /// rather than the first. An operator fixing one thing per save is an
    /// operator who saves seven times.
    /// </summary>
    [Fact]
    public void AConfigurationWrongInSeveralWaysReportsAllOfThem()
    {
        var configuration = new PluginConfiguration();
        configuration.Format = ConfigurationFormat.Current + 1;
        configuration.Direction = (SyncDirection)7;
        configuration.ParticipatingLibraries.Add(ALibraryThatWasRemoved);
        configuration.ExcludedFields.Add("Description");

        var problems = ConfigurationValidation.Validate(configuration, WhatTheServerHolds);

        Assert.Equal(5, problems.Count);
        Assert.Equal(
            new[]
            {
                nameof(PluginConfiguration.Format),
                nameof(PluginConfiguration.Direction),
                nameof(PluginConfiguration.ParticipatingLibraries),
                nameof(PluginConfiguration.ExcludedFields),
                nameof(PluginConfiguration.PairingId),
            },
            problems.Select(problem => problem.Property).ToArray());
    }

    /// <summary>
    /// Every arrangement this suite knows how to make wrong, run through the
    /// validator once. It is the input to the two derived assertions above, and
    /// it is one list rather than one per assertion so the two cannot disagree
    /// about what the plugin can report.
    /// </summary>
    private static IReadOnlyList<ConfigurationProblem> EveryProblemThisPluginCanReport()
    {
        var arrangements = new List<PluginConfiguration>();

        var shapeFromTheFuture = Configured();
        shapeFromTheFuture.Format = ConfigurationFormat.Current + 1;
        arrangements.Add(shapeFromTheFuture);

        var shapeThatIsNoShape = Configured();
        shapeThatIsNoShape.Format = -1;
        arrangements.Add(shapeThatIsNoShape);

        var wrongDirection = Configured();
        wrongDirection.Direction = (SyncDirection)7;
        arrangements.Add(wrongDirection);

        var absentLibrary = Configured();
        absentLibrary.ParticipatingLibraries.Add(ALibraryThatWasRemoved);
        arrangements.Add(absentLibrary);

        var repeatedLibrary = Configured();
        repeatedLibrary.ParticipatingLibraries.Add(AFilmLibrary);
        repeatedLibrary.ParticipatingLibraries.Add(AFilmLibrary);
        arrangements.Add(repeatedLibrary);

        var undeclaredField = Configured();
        undeclaredField.ExcludedFields.Add("Description");
        arrangements.Add(undeclaredField);

        var fieldThatDoesNotMove = Configured();
        fieldThatDoesNotMove.ExcludedFields.Add("Genres");
        arrangements.Add(fieldThatDoesNotMove);

        var repeatedField = Configured();
        repeatedField.ExcludedFields.Add("Overview");
        repeatedField.ExcludedFields.Add("Overview");
        arrangements.Add(repeatedField);

        var noPairing = new PluginConfiguration();
        noPairing.ParticipatingLibraries.Add(AFilmLibrary);
        arrangements.Add(noPairing);

        return arrangements
            .SelectMany(configuration => ConfigurationValidation.Validate(configuration, WhatTheServerHolds))
            .ToList();
    }

    /// <summary>
    /// A configuration with a pairing chosen and nothing else said, so a case
    /// below differs from a working one by exactly the thing it is about.
    /// </summary>
    private static PluginConfiguration Configured()
    {
        return new PluginConfiguration { PairingId = APairing };
    }
}
