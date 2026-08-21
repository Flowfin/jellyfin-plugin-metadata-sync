using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Configuration;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The moment the configuration is read back. The rules that decide whether a
/// configuration can be acted on are covered in
/// <see cref="ConfigurationValidationTests"/>; what is covered here is that
/// something asks them, and what a caller gets when the answer is not empty.
/// </summary>
public class ConfigurationLoadTests
{
    private static readonly Guid _film = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _series = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _pairing = new("44444444-4444-4444-4444-444444444444");

    /// <summary>
    /// The ordinary case. A configuration nothing is wrong with is handed over
    /// as it is, and not a copy of it.
    /// </summary>
    [Fact]
    public void AConfigurationThatCanBeActedOnIsHandedOver()
    {
        var configuration = new PluginConfiguration { PairingId = _pairing };
        configuration.ParticipatingLibraries.Add(_film);

        var provider = ProviderOver(configuration, _film);

        Assert.Same(configuration, provider.Require());
        Assert.Empty(provider.Problems());
    }

    /// <summary>
    /// The case this seam exists for. The server deserialises a file it does
    /// not know the rules for, so a hand-edited file naming a library the
    /// server no longer holds arrives as a well-formed object, and asking for
    /// it to act on is refused.
    /// </summary>
    [Fact]
    public void AConfigurationThatCannotBeActedOnIsRefusedWhenItIsAskedFor()
    {
        var configuration = new PluginConfiguration { PairingId = _pairing };
        configuration.ParticipatingLibraries.Add(_series);

        var provider = ProviderOver(configuration, _film);

        var refusal = Assert.Throws<ConfigurationRefusedException>(() => provider.Require());

        var problem = Assert.Single(refusal.Problems);
        Assert.Equal(nameof(PluginConfiguration.ParticipatingLibraries), problem.Property);
        Assert.Contains(_series.ToString(), problem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refusal carries every reason. An operator repairing a file by hand,
    /// told one thing at a time, is refused once per repair, and the run of
    /// refusals is where they stop believing the plugin knows what it wants.
    /// </summary>
    [Fact]
    public void TheRefusalCarriesEveryReasonRatherThanTheFirst()
    {
        var configuration = new PluginConfiguration();
        configuration.ExcludedFields.Add("NoSuchField");

        var provider = ProviderOver(configuration);

        var refusal = Assert.Throws<ConfigurationRefusedException>(() => provider.Require());

        Assert.Equal(
            new[] { nameof(PluginConfiguration.ExcludedFields), nameof(PluginConfiguration.PairingId) },
            refusal.Problems.Select(problem => problem.Property));
    }

    /// <summary>
    /// The problems are readable while every action is refused. A page has to
    /// say what is wrong at the same moment nothing may be acted on, so the
    /// reading and the refusal are two members rather than one.
    /// </summary>
    [Fact]
    public void TheProblemsAreReadableWhileTheConfigurationIsRefused()
    {
        var configuration = new PluginConfiguration { PairingId = _pairing };
        configuration.ParticipatingLibraries.Add(_series);

        var provider = ProviderOver(configuration, _film);

        Assert.Single(provider.Problems());
        Assert.Throws<ConfigurationRefusedException>(() => provider.Require());
    }

    /// <summary>
    /// A repair is seen without a restart. A verdict kept from the first read
    /// would go on refusing every action after the operator had put the library
    /// back, which is the failure a cached answer produces and the reason
    /// nothing here caches one.
    /// </summary>
    [Fact]
    public void ARepairIsSeenWithoutARestart()
    {
        var configuration = new PluginConfiguration { PairingId = _pairing };
        configuration.ParticipatingLibraries.Add(_series);

        var held = new List<Guid> { _film };
        var provider = new PluginConfigurationProvider(() => configuration, () => held);

        Assert.Throws<ConfigurationRefusedException>(() => provider.Require());

        held.Add(_series);

        Assert.Same(configuration, provider.Require());
    }

    /// <summary>
    /// The range is what the server's own library administration lists, and
    /// reading it asks the server for that and for nothing else.
    /// </summary>
    [Fact]
    public void TheRangeIsTheLibrariesTheServerLists()
    {
        var (library, folders) = LibraryFolders.Listing(_film, _series);

        Assert.Equal(new[] { _film, _series }, ServerLibraries.Held(library));
        Assert.Equal("GetVirtualFolders", Assert.Single(folders.Called));
    }

    /// <summary>
    /// A library the server describes with no identifier this plugin can hold
    /// is left out of the range, so a configuration naming it is refused rather
    /// than acted on against a library nothing can be matched to.
    /// </summary>
    [Fact]
    public void ALibraryWithNoUsableIdentifierIsNotInTheRange()
    {
        var (library, folders) = LibraryFolders.Listing(_film);
        folders.Folders.Add(new VirtualFolderInfo { Name = "Music", ItemId = "not an identifier" });

        Assert.Equal(new[] { _film }, ServerLibraries.Held(library));
    }

    /// <summary>
    /// Reading the range with no server is refused where the mistake was made.
    /// </summary>
    [Fact]
    public void ReadingTheLibrariesWithNoServerIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ServerLibraries.Held(null!));
    }

    /// <summary>
    /// A refusal built from no list of problems at all is refused. The list is
    /// what a page renders, so a refusal carrying nothing would disable every
    /// action and say why to nobody.
    /// </summary>
    [Fact]
    public void ARefusalBuiltFromNoProblemsAtAllIsRefused()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ConfigurationRefusedException(null!));
    }

    /// <summary>
    /// The sentence a server log holds names how many reasons there were and
    /// carries each of them, because a log line saying only that something is
    /// wrong sends the reader to a page they may not be able to open.
    /// </summary>
    [Fact]
    public void TheSentenceCarriesEveryReasonAsWell()
    {
        var configuration = new PluginConfiguration();
        configuration.ExcludedFields.Add("NoSuchField");

        var refusal = Assert.Throws<ConfigurationRefusedException>(() => ProviderOver(configuration).Require());

        foreach (var problem in refusal.Problems)
        {
            Assert.Contains(problem.Message, refusal.Message, StringComparison.Ordinal);
        }

        Assert.Contains(
            refusal.Problems.Count.ToString(CultureInfo.InvariantCulture),
            refusal.Message,
            StringComparison.Ordinal);
    }

    private static PluginConfigurationProvider ProviderOver(PluginConfiguration configuration, params Guid[] held)
    {
        return new PluginConfigurationProvider(() => configuration, () => held);
    }
}
