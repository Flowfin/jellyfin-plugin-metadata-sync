using System;
using Jellyfin.Plugin.MetadataSync.Configuration;

namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// One line of this plugin's account of itself: what it decided about one field
/// on one item, and everything somebody would need to argue with the decision.
/// </summary>
/// <remarks>
/// An operator asking why a description did not change is asking a question
/// nothing else in this plugin can answer. The plan says what would happen and
/// is thrown away when the pass ends; the store says what was written and says
/// nothing about a field where nothing was; the library shows the result and
/// not the reason. This is the row that survives the pass and carries the
/// reason.
/// <para>
/// Every column here is one the question needs. The item and the field say
/// which value is being talked about. The two values say what the disagreement
/// was. The rule says which declared row decided it, or that the table ran out
/// and none did. The outcome says what happened. The direction says whether the
/// field was ever eligible to move that way, without which a row saying nothing
/// was written is unreadable. And the moment says when, so two entries about
/// one field can be told apart.
/// </para>
/// <para>
/// The peer's item is deliberately not here. A resolution is true of two
/// libraries as they stood when it was computed and nothing keeps one past the
/// pass that derived it, which is #33, so a log outliving the pass is exactly
/// the slot that rule refuses. What an operator gets instead is this server's
/// item, which is the one they can open.
/// </para>
/// <para>
/// What this type is not is a log. Nothing here holds entries, bounds them,
/// reads them under the lines they fall into or shows them. The register and its
/// bound are <see cref="Store.ConflictLog"/>, the lines are
/// <see cref="ConflictGrouping"/>, and the surface that would show either of
/// them is the rest of #48 and is not built.
/// </para>
/// </remarks>
public sealed class ConflictEntry
{
    /// <summary>
    /// Gets the item on this server the field belongs to.
    /// </summary>
    public required Guid Item { get; init; }

    /// <summary>
    /// Gets the field, named as the server names it.
    /// </summary>
    public required string Field { get; init; }

    /// <summary>
    /// Gets what this server held, as the entry shows it.
    /// </summary>
    public required ShownValue LocalValue { get; init; }

    /// <summary>
    /// Gets what the peer held, as the entry shows it.
    /// </summary>
    public required ShownValue PeerValue { get; init; }

    /// <summary>
    /// Gets the declared conflict rule that decided the field, by the name it
    /// is declared under, or null where the table ran out and no rule fired.
    /// </summary>
    /// <remarks>
    /// Null is a state an entry is allowed to be in and it is the one worth
    /// reading. It says the two servers disagreed and this plan has no declared
    /// answer for that disagreement, which is a different thing from a rule
    /// that chose to write nothing. An entry that spelled it as an empty name
    /// would collapse the two.
    /// </remarks>
    public required string? Rule { get; init; }

    /// <summary>
    /// Gets what happened to the field.
    /// </summary>
    public required ConflictOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the direction in force for the pairing when the decision was made.
    /// </summary>
    public required SyncDirection Direction { get; init; }

    /// <summary>
    /// Gets the moment the decision was made, on this server's clock.
    /// </summary>
    /// <remarks>
    /// This server's clock and no other. It is written down so two entries
    /// about one field can be ordered against each other, and it is never
    /// compared with anything the peer produced: nothing establishes that the
    /// two clocks agree, which is #46. It arrives as an argument rather than
    /// being read here, so a caller decides what a run's moment is and a test
    /// can hand one over.
    /// </remarks>
    public required DateTimeOffset At { get; init; }
}
