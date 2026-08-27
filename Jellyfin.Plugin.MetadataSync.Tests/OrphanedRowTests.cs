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
            Assert.Equal(
                _filedUnder,
                question.GetParameters()
                    .Select(parameter => parameter.Name)
                    .Where(name => !string.Equals(name, "value", StringComparison.Ordinal)
                                   && !string.Equals(name, "previousValue", StringComparison.Ordinal))
                    .ToArray());
        }
    }

    /// <summary>
    /// The reading above is over something rather than over nothing. A question
    /// list that came back empty would satisfy the loop without asking anything.
    /// </summary>
    [Fact]
    public void TheReadingFindsTheQuestionsRatherThanNone()
    {
        Assert.Equal(3, Questions().Count);
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
    /// The questions this store takes about one written value.
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
