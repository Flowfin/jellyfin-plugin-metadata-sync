using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// The one route from a plan to this server's library.
/// </summary>
/// <remarks>
/// There is one supported way to change an item and have the server notice it,
/// and this type calls it and nothing else. Writing underneath it, into the item
/// repository or into the database file, leaves the server's own caches and
/// every connected client holding the old value; <c>docs/reconciliation.md</c>
/// argues that at length and the suite refuses a repository type reachable from
/// here rather than trusting the paragraph.
/// <para>
/// It obeys a plan and asks nothing again. It does not read the field register,
/// does not ask the conflict rules and does not re-check a lock, for the reason
/// the applier does not: every one of those answered while the plan was being
/// made, and a second answer is how two halves of one pass come to disagree
/// about one field. The cost is stated rather than hidden - a lock an operator
/// sets between the plan and the write is not seen, and that window is #41.
/// </para>
/// <para>
/// What it will not do is guess. A row naming a field it has no writer for, a
/// year that is not a number and a date in a spelling nothing declared are all
/// refused by name. The two set-valued fields the register lets move are refused
/// outright, because a plan row carries one string and no separator is safe
/// inside a tag somebody typed.
/// </para>
/// </remarks>
public sealed class LibraryPlanTarget : IPlanTarget
{
    /// <summary>
    /// The reason every write from this plugin is made under.
    /// </summary>
    /// <remarks>
    /// Argued rather than copied from a call site. What this plugin does is a
    /// deliberate change made on an authority the operator chose, which is what
    /// the server means by an edit; recording it as a download would be false in
    /// the one field a later reader uses to tell a provider's work from a
    /// person's. The cost is real and is in <c>docs/reconciliation.md</c>: this
    /// is the highest of the five values, so it clears every threshold the
    /// server has, and two of those reach a disk an operator looks at.
    /// </remarks>
    public const ItemUpdateType UpdateReason = ItemUpdateType.MetadataEdit;

    /// <summary>
    /// The fields the register lets move that one string cannot carry.
    /// </summary>
    /// <remarks>
    /// Both are sets of strings on the item. A plan row holds one string, and
    /// any character chosen to separate two entries inside it is a character an
    /// operator may have typed inside one entry, so a value read back would
    /// silently become two tags or one. Nothing in this tree declares an
    /// escaping for that, and inventing one here would bind whoever later reads
    /// items into observations to a spelling they never agreed to. Refused until
    /// it is declared where both halves can read it.
    /// </remarks>
    private static readonly ReadOnlyCollection<string> _setValued =
        new(new[] { "Tags", "ProductionLocations" });

    /// <summary>
    /// One writer per field this path can spell, keyed by the name the server
    /// gives the field.
    /// </summary>
    /// <remarks>
    /// The suite holds this set and the set above against the register, in both
    /// directions, so a tenth field declared to move is refused by the suite
    /// until somebody has decided which of the two it belongs in.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, Action<BaseItem, string?>> _writers =
        new ReadOnlyDictionary<string, Action<BaseItem, string?>>(
            new Dictionary<string, Action<BaseItem, string?>>(StringComparer.Ordinal)
            {
                ["Name"] = static (item, value) => item.Name = value!,
                ["Overview"] = static (item, value) => item.Overview = value!,
                ["Tagline"] = static (item, value) => item.Tagline = value!,
                ["OfficialRating"] = static (item, value) => item.OfficialRating = value!,
                ["PremiereDate"] = static (item, value) => item.PremiereDate = AsDate("PremiereDate", value),
                ["EndDate"] = static (item, value) => item.EndDate = AsDate("EndDate", value),
                ["ProductionYear"] = static (item, value) => item.ProductionYear = AsYear(value),
            });

    private readonly ILibraryManager _library;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryPlanTarget"/> class.
    /// </summary>
    /// <param name="library">The server's library, which is the only thing this type holds.</param>
    /// <exception cref="ArgumentNullException">There is no library to write to.</exception>
    public LibraryPlanTarget(ILibraryManager library)
    {
        ArgumentNullException.ThrowIfNull(library);

        _library = library;
    }

    /// <summary>
    /// Gets the fields this path can write, which is every field the register
    /// declares as moving apart from the set-valued ones.
    /// </summary>
    public static IReadOnlyCollection<string> WritableFields => (IReadOnlyCollection<string>)_writers.Keys;

    /// <summary>
    /// Gets the fields the register lets move that this path refuses, because a
    /// plan row cannot carry a set of strings.
    /// </summary>
    public static IReadOnlyCollection<string> FieldsWithNoSpelling => _setValued;

    /// <summary>
    /// The spelling a date is read in, which is the round-trip form.
    /// </summary>
    /// <remarks>
    /// Strict on purpose. A value in any other spelling is refused loudly here
    /// rather than reinterpreted under this machine's locale, which is the
    /// failure where a European day-first date becomes a different month on a
    /// server set to English.
    /// </remarks>
    /// <param name="field">The field being read, so a refusal names it.</param>
    /// <param name="value">The plan row's value, or null where the peer holds none.</param>
    /// <returns>The date, or null where the peer holds none.</returns>
    /// <exception cref="WriteRefusedException">The value is not a round-trip date.</exception>
    private static DateTime? AsDate(string field, string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (!DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new WriteRefusedException(NotADate(field, value));
        }

        return parsed;
    }

    /// <summary>
    /// The spelling a year is read in, which is digits and nothing else.
    /// </summary>
    /// <param name="value">The plan row's value, or null where the peer holds none.</param>
    /// <returns>The year, or null where the peer holds none.</returns>
    /// <exception cref="WriteRefusedException">The value is not a plain number.</exception>
    private static int? AsYear(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new WriteRefusedException(NotAYear(value));
        }

        return parsed;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">There is no item plan to carry out.</exception>
    /// <exception cref="ItemNotInLibraryException">The item the plan names has gone.</exception>
    /// <exception cref="WriteRefusedException">A row does not describe a value this path can write.</exception>
    /// <remarks>
    /// The refusal on a missing argument is in this method and the work is in
    /// the one below it, for the reason the applier splits the same way: an
    /// argument check inside an asynchronous method is handed back as a faulted
    /// task, which a caller that forgot to await never sees.
    /// </remarks>
    public Task WriteAsync(ItemPlan item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        return Write(item, cancellationToken);
    }

    private static string NoWriterFor(string field) => string.Format(
        CultureInfo.InvariantCulture,
        "The plan says to write '{0}' and this path has no writer for it. Either the register grew a row that moves and nothing was decided about writing it, or the plan was made by a newer version of this plugin.",
        field);

    private static string ASetInOneString(string field) => string.Format(
        CultureInfo.InvariantCulture,
        "The plan says to write '{0}', which the server holds as a set of strings. A plan row carries one string and no separator is safe inside an entry an operator typed, so nothing is written until that spelling is declared where both halves of a pass can read it.",
        field);

    private static string NotADate(string field, string value) => string.Format(
        CultureInfo.InvariantCulture,
        "The plan says to write '{0}' as '{1}', which is not a date in the round-trip spelling this path reads. Nothing is written, because reading it under this machine's locale is how a day becomes a month.",
        field,
        value);

    private static string NotAYear(string value) => string.Format(
        CultureInfo.InvariantCulture,
        "The plan says to write 'ProductionYear' as '{0}', which is not a plain number. Nothing is written.",
        value);

    private static string NoSuchItem(Guid id) => string.Format(
        CultureInfo.InvariantCulture,
        "The library holds no item '{0}'. It was there when the plan was made, so something removed it since, and nothing on it is written.",
        id);

    private async Task Write(ItemPlan item, CancellationToken cancellationToken)
    {
        // Fetched again rather than carried on the plan. A plan holds
        // identifiers and values and never an item, so there is no stale copy
        // here to write back over somebody else's edit, and the two halves of a
        // pass can be days apart without this one caring.
        var found = _library.GetItemById(item.LocalItemId) ?? throw new ItemNotInLibraryException(NoSuchItem(item.LocalItemId));

        foreach (var change in item.Changes)
        {
            if (!change.Writes)
            {
                continue;
            }

            Set(found, change);
        }

        // One call, on the item as a whole, after every field on it is set. A
        // call per field would make one operator edit into several events, and
        // every consumer of those events would see the item in states no plan
        // ever described.
        await _library.UpdateItemAsync(found, found.GetParent(), UpdateReason, cancellationToken).ConfigureAwait(false);
    }

    private static void Set(BaseItem found, PlannedChange change)
    {
        if (_setValued.Contains(change.Field))
        {
            throw new WriteRefusedException(ASetInOneString(change.Field));
        }

        if (!_writers.TryGetValue(change.Field, out var writer))
        {
            throw new WriteRefusedException(NoWriterFor(change.Field));
        }

        writer(found, change.ValueToWrite);
    }
}
