using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// The decisions one rule took to one end, collected so that four thousand of
/// them read as one line somebody can open rather than four thousand lines
/// nobody does.
/// </summary>
/// <remarks>
/// The failure this shape is against is a log that is complete and unreadable.
/// A pass over a library where one declared rule is missing produces an entry
/// per item, every one of them saying the same thing, and an operator scrolling
/// that list learns the thing that is wrong last. Grouped, the same account says
/// it once and carries the items underneath it.
/// <para>
/// WHAT SEPARATES TWO GROUPS IS THE RULE AND THE OUTCOME, AND BOTH HALVES ARE
/// LOAD-BEARING. The rule alone would put a field this plugin took from the peer
/// beside one it refused, which are the two answers an operator is trying to
/// tell apart. The outcome alone would put every refusal in the tree into one
/// line, and the question an operator asks is which rule refused, not how many
/// refusals there were.
/// </para>
/// <para>
/// NO RULE AT ALL IS ITS OWN GROUP AND IS NOT AN EMPTY NAME.
/// <see cref="ConflictEntry.Rule"/> is null where the table ran out and nothing
/// decided, which is the state #45 is about and the one an operator most wants
/// collected. A grouping that spelled it as an empty string would file it beside
/// a rule somebody named with one, and the two say opposite things about whether
/// this plan has an answer.
/// </para>
/// <para>
/// THERE IS NO COUNT HERE, AND ITS ABSENCE IS THE PROPERTY RATHER THAN AN
/// OMISSION. How many decisions a group holds is how many entries it holds, so
/// there is nothing for a second number to disagree with. A count kept beside
/// the members is the shape #48 asks a check to refuse: it is written once, it
/// is read afterwards, and the day a member is added or dropped without it the
/// group reports a total that no list under it adds up to.
/// </para>
/// <para>
/// Nothing holds one of these. They are derived from the entries whenever
/// somebody asks and are gone afterwards, which is why no store type carries
/// one and the suite refuses the day one does.
/// </para>
/// </remarks>
public sealed class ConflictGroup
{
    /// <summary>
    /// Gets the declared conflict rule every decision in this group was taken
    /// by, or null where the table ran out and none was.
    /// </summary>
    public required string? Rule { get; init; }

    /// <summary>
    /// Gets what happened to every field in this group.
    /// </summary>
    public required ConflictOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the decisions themselves, in the order the account holds them.
    /// </summary>
    /// <remarks>
    /// The members and not a summary of them. A group an operator cannot expand
    /// answers how much rather than what, and the field they came to argue about
    /// is one of the rows underneath.
    /// </remarks>
    public required IReadOnlyList<ConflictEntry> Entries { get; init; }
}
