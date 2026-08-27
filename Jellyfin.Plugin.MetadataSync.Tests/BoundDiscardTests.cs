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
/// What the bound throws away, and what this store can be asked about it.
/// </summary>
/// <remarks>
/// The bound keeps ten values per item and per field and drops the oldest
/// first, so the eleventh write on a field destroys the one value an operator
/// would care most about: what was on that field before this plugin ever
/// touched it. <c>docs/storage.md</c> states that cost in prose. What it did not
/// state, and what the cases below hold, is that the loss is not only
/// unrecoverable but unaskable: afterwards the store answers exactly as it would
/// have answered had it never held the value at all.
/// <para>
/// That matters to two issues rather than to this file. #66 owes a surface
/// saying attribution is incomplete where the bound has discarded records,
/// instead of reporting a clean number, and #64 owes a confirmation stating
/// counts before a revert touches anything. Neither can derive its number from
/// this store as it stands, and the first case below is what says so in a form
/// that stops being true the day somebody changes it.
/// </para>
/// <para>
/// It is a negative disclosure and it is held as one. The second case reads the
/// store's answering surface and compares it with the set the disclosure rests
/// on, so a member added to answer this question reddens here and the sentence
/// in the document is re-read rather than quietly falsified. What it cannot do
/// is judge a member that answers the question under a name the set already
/// carries.
/// </para>
/// <para>
/// It is a snapshot of a surface and it is wider than its subject, which is the
/// price of holding the disclosure at all: any public member added to the store
/// or to either interface reddens it, whether or not it has anything to do with
/// the bound. The repair is one line in the declared set, taken after reading
/// whether the paragraph in the document is still true.
/// </para>
/// </remarks>
public class BoundDiscardTests
{
    private const string Field = "Overview";

    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid _item = new("aaaaaaaa-0000-0000-0000-000000000001");

    /// <summary>
    /// The members this store answers questions through. The disclosure in
    /// <c>docs/storage.md</c> is a statement about this set: none of them
    /// separates a field whose earliest write the bound discarded from a field
    /// written exactly <see cref="WrittenValues.Bound"/> times.
    /// </summary>
    /// <remarks>
    /// Both interfaces are named as well as the class, because a caller holding
    /// an <see cref="IWrittenValues"/> or an <see cref="IPairingStore"/> asks
    /// through those and would meet a new member there first.
    /// </remarks>
    private static readonly string[] _answers =
    {
        "Held",
        "History",
        "Holding",
        "LastWritten",
        "Location",
        "Record",
        "Remove",
        "ToString",
        "Unreadable",
    };

    /// <summary>
    /// A field this plugin wrote eleven times and a field it wrote ten times,
    /// arranged so the ten values still held are the same ten values, are two
    /// different histories and one answer. Every question this store takes gives
    /// the same reply on both, before a restart and after one.
    /// </summary>
    /// <remarks>
    /// The first store lost its earliest write to the bound and the second never
    /// made one, which is the difference between a field whose earlier value
    /// this plugin cannot produce and a field it was never asked about that far
    /// back. An operator being told how much of the attribution is missing needs
    /// exactly that difference, and this case is what says the store does not
    /// carry it.
    /// </remarks>
    [Fact]
    public void AWriteTheBoundDiscardedLeavesTheStoreAnsweringAsThoughItNeverHappened()
    {
        using var lost = new TemporaryDirectory();
        using var never = new TemporaryDirectory();

        var discarded = new WrittenValues(lost.Path);
        var complete = new WrittenValues(never.Path);

        // Eleven writes, so the first is dropped and values two to eleven are
        // what is left.
        for (var n = 1; n <= WrittenValues.Bound + 1; n++)
        {
            discarded.Record(_pairing, _item, Field, Value(n), Value(n - 1));
        }

        // Ten writes, beginning where the first store's surviving history
        // begins, so the two hold the same ten rows by different routes.
        for (var n = 2; n <= WrittenValues.Bound + 1; n++)
        {
            complete.Record(_pairing, _item, Field, Value(n), Value(n - 1));
        }

        AnswersAgree(discarded, complete);
        AnswersAgree(new WrittenValues(lost.Path), new WrittenValues(never.Path));
    }

    /// <summary>
    /// The set the disclosure rests on is the set the store carries. A member
    /// added to either interface or to the store itself reddens this, which is
    /// the moment the sentence in <c>docs/storage.md</c> saying the loss cannot
    /// be asked about has to be read again.
    /// </summary>
    [Fact]
    public void TheStoreAnswersThroughTheMembersTheDisclosureIsAbout()
    {
        Assert.Equal(
            _answers.OrderBy(name => name, StringComparer.Ordinal).ToList(),
            Answering().OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The reading above is over something rather than over nothing. A member
    /// list that came back empty would satisfy a comparison against an empty
    /// declared set and say nothing at all.
    /// </summary>
    [Fact]
    public void TheReadingFindsTheMembersRatherThanNone()
    {
        Assert.NotEmpty(Answering());
        Assert.Contains("History", Answering(), StringComparer.Ordinal);
    }

    /// <summary>
    /// The public members a caller reaches this store through, by name, across
    /// the class and the two interfaces it answers as.
    /// </summary>
    /// <returns>The names, without duplicates.</returns>
    private static IReadOnlyList<string> Answering()
    {
        var types = new[] { typeof(WrittenValues), typeof(IWrittenValues), typeof(IPairingStore) };

        return types
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(member => member.MemberType is MemberTypes.Method or MemberTypes.Property)
            .Select(Named)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// One member, under the name a caller writes rather than the name the
    /// runtime files a property's accessor under.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The name.</returns>
    private static string Named(MemberInfo member) =>
        member is MethodInfo method && method.IsSpecialName
            ? method.Name[(method.Name.IndexOf('_', StringComparison.Ordinal) + 1)..]
            : member.Name;

    /// <summary>
    /// Every question this store takes, asked of both stores and compared.
    /// </summary>
    /// <param name="discarded">The store whose earliest write the bound dropped.</param>
    /// <param name="complete">The store that never made that write.</param>
    private static void AnswersAgree(WrittenValues discarded, WrittenValues complete)
    {
        Assert.Equal(complete.LastWritten(_pairing, _item, Field), discarded.LastWritten(_pairing, _item, Field));
        Assert.Equal(complete.Unreadable, discarded.Unreadable);
        Assert.Equal(complete.Held, discarded.Held);

        Assert.Equal(
            complete.History(_pairing, _item, Field).Select(Said).ToList(),
            discarded.History(_pairing, _item, Field).Select(Said).ToList());

        Assert.Equal(
            complete.Holding(_pairing).Rows.ToList(),
            discarded.Holding(_pairing).Rows.ToList());

        Assert.Equal(complete.Holding(_pairing).Count, discarded.Holding(_pairing).Count);

        // The file's own name is the same on both and the directory around it is
        // not, so the sentence is compared with the path taken out of it. What is
        // asked here is whether the store says anything different about what it
        // holds, not where the case put it.
        Assert.Equal(
            complete.ToString().Replace(complete.Location, string.Empty, StringComparison.Ordinal),
            discarded.ToString().Replace(discarded.Location, string.Empty, StringComparison.Ordinal));
    }

    /// <summary>
    /// One held value, said in full so a comparison sees both halves.
    /// </summary>
    /// <param name="written">The value.</param>
    /// <returns>The sentence.</returns>
    private static string Said(WrittenValue written) => string.Format(
        CultureInfo.InvariantCulture,
        "{0}|{1}",
        written.Value ?? "(nothing)",
        written.Previous ?? "(nothing)");

    /// <summary>
    /// The nth value, as this case spells one.
    /// </summary>
    /// <param name="n">Which write it is.</param>
    /// <returns>The value.</returns>
    private static string Value(int n) => string.Format(CultureInfo.InvariantCulture, "value {0}", n);

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
