using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// What happens to this plugin's rows when the library their items were in is
/// gone.
/// </summary>
/// <remarks>
/// #42 asks for that to be a stated state rather than whatever falls out, and
/// the state is that the rows stay. <c>docs/storage.md</c> is where the choice
/// and its reason are written; this file is what holds the shape the choice
/// rests on.
/// <para>
/// The shape is that a row is filed under a pairing, an item and a field, and
/// under nothing else. A library is not a component of the key and this store
/// never asks the server anything, so a library disappearing is not an event it
/// can see, let alone act on. That is what makes leaving the rows the answer
/// rather than the absence of one: the alternative is not a cheaper cleanup, it
/// is a store that would have to be keyed differently to know what to clean.
/// </para>
/// <para>
/// So the case below is about the key rather than about a deletion. It reads the
/// questions this store takes and refuses one that has grown a fourth thing to
/// be filed under, because that is the change that would turn a disappearing
/// library into rows this plugin could prune - and pruning them is a plugin
/// destroying its own proof that it wrote a value, which is the direction #66
/// refuses.
/// </para>
/// <para>
/// THE COMPARISON IS A PREFIX OF THE KEY AND IT WAS THE WHOLE OF IT. A question
/// about one pairing arrived with #64 - the fields a pairing touched, which have
/// to be enumerable without reading a library - and a guard demanding all three
/// components of every question would have refused it for being filed under
/// less rather than under more. What it refuses is unchanged: a fourth thing to
/// be filed under is not a prefix of three however it is ordered, and the two
/// theories below run that in both directions rather than leaving the widening
/// to be trusted.
/// </para>
/// <para>
/// What it does not reach. It judges the parameters of the questions and never
/// what is done with them, so a store that filed rows by library inside a
/// parameter it already takes would pass. And the second case is about
/// persistence rather than about libraries: it says a row outlives the instance
/// that wrote it whatever became of the library, which is the property an
/// operator's report depends on and not a second reading of the key.
/// </para>
/// </remarks>
public class OrphanedRowTests
{
    private const string Field = "Overview";

    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid _otherPairing = new("cccccccc-0000-0000-0000-000000000009");
    private static readonly Guid _item = new("aaaaaaaa-0000-0000-0000-000000000001");

    /// <summary>
    /// What a question about a written value is filed under. Three things, and a
    /// library is not among them.
    /// </summary>
    private static readonly string[] _filedUnder =
    {
        "pairingId",
        "itemId",
        "field",
    };

    /// <summary>
    /// Every question this store takes about a written value is filed under a
    /// pairing, an item and a field, and under nothing else. A fourth component
    /// reddens this, which is the moment the answer in <c>docs/storage.md</c> -
    /// that a library disappearing leaves the rows where they are - has to be
    /// read again.
    /// </summary>
    [Fact]
    public void AWrittenValueIsFiledUnderAPairingAnItemAndAFieldAndNothingElse()
    {
        foreach (var question in Questions())
        {
            Assert.True(
                IsPartOfTheKey(FiledUnder(question)),
                question.Name + " is filed under " + string.Join(", ", FiledUnder(question)));
        }
    }

    /// <summary>
    /// The comparison this guard makes is a prefix of the key rather than the
    /// whole of it, and the two legs below are why that is not a weakening.
    /// </summary>
    /// <remarks>
    /// It compared the whole key until a question about one pairing arrived,
    /// which is <c>Fields</c>, and #64 needs it: the set of fields a pairing
    /// touched has to be enumerable without reading a library, so a question
    /// filed under the pairing alone is the point rather than a lapse. A prefix
    /// is the widest reading that still refuses what this guard exists against,
    /// because a fourth thing to be filed under is not a prefix of three however
    /// it is ordered.
    /// </remarks>
    /// <param name="parameters">A parameter list a question might have.</param>
    [Theory]
    [InlineData("pairingId,itemId,field,libraryId")]
    [InlineData("libraryId")]
    [InlineData("pairingId,libraryId")]
    [InlineData("itemId,field")]
    public void AQuestionFiledUnderSomethingElseIsRefused(string parameters)
    {
        Assert.False(IsPartOfTheKey(parameters.Split(',')));
    }

    /// <summary>
    /// The neighbour. These are the prefixes of the key, and a guard refusing
    /// them would refuse the questions this store already answers.
    /// </summary>
    /// <param name="parameters">A parameter list a question might have.</param>
    [Theory]
    [InlineData("pairingId")]
    [InlineData("pairingId,itemId")]
    [InlineData("pairingId,itemId,field")]
    public void AQuestionFiledUnderPartOfTheKeyIsAccepted(string parameters)
    {
        Assert.True(IsPartOfTheKey(parameters.Split(',')));
    }

    /// <summary>
    /// The reading above is over something rather than over nothing. A question
    /// list that came back empty would satisfy the loop without asking anything.
    /// </summary>
    [Fact]
    public void TheReadingFindsTheQuestionsRatherThanNone()
    {
        Assert.Equal(4, Questions().Count);
    }

    /// <summary>
    /// A row whose item is in no library any more is still held, still counted
    /// in the report an operator is shown, and goes when its pairing's rows go
    /// rather than before.
    /// </summary>
    /// <remarks>
    /// The store cannot be told a library has gone, which is the point rather
    /// than a limitation of the case: it is asked the same questions afterwards
    /// and answers the same way, across the instance that wrote the row and the
    /// one that read it back. A report that quietly stopped counting those rows
    /// would tell an operator this plugin holds less about them than it does.
    /// </remarks>
    [Fact]
    public void ARowOutlivesTheLibraryAndGoesWithThePairing()
    {
        using var directory = new TemporaryDirectory();

        new WrittenValues(directory.Path).Record(_pairing, _item, Field, "what the peer said", "what was there");

        var afterRestart = new WrittenValues(directory.Path);

        Assert.Single(afterRestart.History(_pairing, _item, Field));
        Assert.Equal(1, afterRestart.Holding(_pairing).Count);

        // Another pairing's removal is not this row's.
        Assert.Equal(0, afterRestart.Remove(_otherPairing));
        Assert.Equal(1, afterRestart.Holding(_pairing).Count);

        Assert.Equal(1, afterRestart.Remove(_pairing));
        Assert.Empty(afterRestart.History(_pairing, _item, Field));
    }

    /// <summary>
    /// What one question is filed under, with the values a write carries taken
    /// out, since those are what is recorded rather than what it is filed under.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The names it is filed under, in the order it takes them.</returns>
    private static IReadOnlyList<string?> FiledUnder(MethodBase question) =>
        question.GetParameters()
            .Select(parameter => parameter.Name)
            .Where(name => !string.Equals(name, "value", StringComparison.Ordinal)
                           && !string.Equals(name, "previousValue", StringComparison.Ordinal))
            .ToList();

    /// <summary>
    /// Whether a list of names is the key or the beginning of it.
    /// </summary>
    /// <param name="names">The names.</param>
    /// <returns>Whether every name is the key's own, in the key's own order, from the start.</returns>
    private static bool IsPartOfTheKey(IReadOnlyList<string?> names) =>
        names.Count > 0
        && names.Count <= _filedUnder.Length
        && names.Select((name, at) => string.Equals(name, _filedUnder[at], StringComparison.Ordinal)).All(same => same);

    /// <summary>
    /// The questions this store takes.
    /// </summary>
    /// <returns>The methods.</returns>
    private static IReadOnlyList<MethodInfo> Questions() =>
        typeof(IWrittenValues)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// A directory that exists for one case and is gone afterwards.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "metadata-sync-store-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
