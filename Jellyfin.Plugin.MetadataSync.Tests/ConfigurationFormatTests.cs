using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The reading of a configuration's shape stamp, on its own and away from the
/// validator that reports it.
/// </summary>
/// <remarks>
/// The validator's cases assert what an operator is told. These assert what the
/// number means, which is the half a migration step will read: a step is written
/// against the shape a number had on the day it shipped, so the mapping from
/// stamp to shape is the thing that has to stay still, not the sentence beside
/// it.
/// </remarks>
public class ConfigurationFormatTests
{
    /// <summary>
    /// The condition on #59 in its own words: a missing version is treated as
    /// the earliest one. Absent is what the server's serialiser leaves for an
    /// element that is not in the file, and it is the state every configuration
    /// in existence is in today.
    /// </summary>
    [Fact]
    public void NoStampIsTheEarliestShape()
    {
        Assert.Equal(ConfigurationFormat.Earliest, ConfigurationFormat.Declared(ConfigurationFormat.Absent));
    }

    /// <summary>
    /// A shape this build reads answers with itself rather than with the
    /// earliest one. A reader that folded every placeable stamp to
    /// <see cref="ConfigurationFormat.Earliest"/> would run every migration step
    /// from the beginning on the day there is a second shape.
    /// </summary>
    /// <remarks>
    /// **This leg cannot fail today and it was run to find that out.** Replacing
    /// the answer with <see cref="ConfigurationFormat.Earliest"/> leaves the
    /// suite green on both targets, because the two constants hold one value
    /// while one shape has existed and the assertion cannot tell them apart. It
    /// is kept rather than deleted because it starts biting on the day a second
    /// shape is declared, which is the day the fold it is about becomes a
    /// migration silently skipped. Until then it is a statement of the contract
    /// and not a guard, and calling it one would be the claim this suite exists
    /// against.
    /// </remarks>
    [Fact]
    public void AShapeThisBuildReadsAnswersWithItself()
    {
        Assert.Equal(ConfigurationFormat.Current, ConfigurationFormat.Declared(ConfigurationFormat.Current));
    }

    /// <summary>
    /// A shape newer than this build cannot be placed. Answering with anything
    /// at all here is the downgrade that reads a newer file under older rules.
    /// </summary>
    [Fact]
    public void AShapeNewerThanThisBuildCannotBePlaced()
    {
        Assert.Null(ConfigurationFormat.Declared(ConfigurationFormat.Current + 1));
    }

    /// <summary>
    /// A number below the absent stamp cannot be placed either. It is not a
    /// shape anything ever wrote, and it is refused rather than read as the
    /// earliest for the same reason the store's stamp refuses a damaged one.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ANumberNoShapeEverHadCannotBePlaced(int stamp)
    {
        Assert.Null(ConfigurationFormat.Declared(stamp));
    }

    /// <summary>
    /// The two constants answer different questions and are named separately for
    /// it. They hold one value today, which is why a reader can take them for
    /// one constant and not notice until the first migration, so what is
    /// asserted is that both are declared rather than that they differ.
    /// </summary>
    [Fact]
    public void TheEarliestShapeAndTheCurrentOneAreBothDeclared()
    {
        Assert.True(ConfigurationFormat.Earliest <= ConfigurationFormat.Current);
        Assert.True(ConfigurationFormat.Absent < ConfigurationFormat.Earliest);
    }

    /// <summary>
    /// The configuration and the store are stamped separately, and this is what
    /// stops the two numbers being quietly collapsed into one. They hold the
    /// same value today and they describe two artefacts that survive an upgrade
    /// independently: a configuration restored from a backup beside a store that
    /// was not is a state an operator reaches without doing anything unusual.
    /// </summary>
    /// <remarks>
    /// What this asserts is that the two are separate declarations, not that
    /// they differ. A change that deleted one and pointed its readers at the
    /// other would compile and would pass every case above.
    /// </remarks>
    [Fact]
    public void TheConfigurationsShapeAndTheStoresAreTwoDeclarations()
    {
        Assert.NotEqual(typeof(ConfigurationFormat), typeof(StoreFormat));

        Assert.NotNull(typeof(ConfigurationFormat).GetField(nameof(ConfigurationFormat.Current)));
        Assert.NotNull(typeof(StoreFormat).GetField(nameof(StoreFormat.Current)));
    }
}
