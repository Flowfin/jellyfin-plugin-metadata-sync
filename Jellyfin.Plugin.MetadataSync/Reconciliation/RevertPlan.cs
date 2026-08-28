using System.Linq;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// What a revert would do, and what it would leave alone, before anything
/// happens.
/// </summary>
/// <remarks>
/// It is data and changes nothing by existing, which is what makes it the
/// confirmation #64's fourth condition asks for rather than a report written
/// after the fact. A caller shows the counts, and applies <see cref="Plan"/>
/// only if somebody says yes.
/// <para>
/// The counts are not decoration. A revert against a library where the record is
/// incomplete takes back less than everything that ever arrived, and
/// <c>docs/lifecycle.md</c> makes that part of the answer rather than a footnote
/// to it: an operator being asked to confirm needs to be told how much this
/// plugin is not going to touch, and why each part of it is untouched.
/// </para>
/// </remarks>
public sealed class RevertPlan
{
    /// <summary>
    /// Gets what to write, carrying only the fields this plugin can prove it
    /// wrote and can produce the earlier value for.
    /// </summary>
    /// <remarks>
    /// An item with nothing to restore is not in it at all, so a plan that
    /// writes nothing is an empty plan rather than a list of items to pass over.
    /// It is an ordinary <see cref="Reconciliation.Plan"/> because it is carried
    /// out by the ordinary write path: an item's fields go together or not at
    /// all, an item something else has written since is deferred, and neither of
    /// those is worth a second implementation.
    /// </remarks>
    public Plan Plan { get; init; } = new();

    /// <summary>
    /// Gets how many fields the revert would put an earlier value back into.
    /// </summary>
    public int FieldsToRestore => Plan.Items.Sum(item => item.FieldsToWrite);

    /// <summary>
    /// Gets how many items the revert would write to.
    /// </summary>
    public int ItemsToWrite => Plan.Items.Count(item => item.Writes);

    /// <summary>
    /// Gets how many fields were read that this plugin has no record of
    /// writing.
    /// </summary>
    /// <remarks>
    /// Left alone, always. A missing record is evidence that the field is not
    /// this plugin's, never an invitation to assume, which is #66's rule and the
    /// thing that makes a revert allowable at all.
    /// </remarks>
    public int FieldsWithNoRecord { get; init; }

    /// <summary>
    /// Gets how many fields this plugin wrote and somebody here has changed
    /// since.
    /// </summary>
    /// <remarks>
    /// Left alone as well, and for a different reason from the count above: the
    /// record exists and says this plugin wrote something else. Putting a value
    /// back over an edit made here would delete the edit, which is the act this
    /// plan exists not to perform.
    /// </remarks>
    public int FieldsChangedSinceThisPluginWroteThem { get; init; }

    /// <summary>
    /// Gets how many fields this plugin wrote whose earlier value it cannot
    /// prove is the one that predates this pairing.
    /// </summary>
    /// <remarks>
    /// This is the not-known case with a stated outcome, and the outcome is that
    /// nothing is written. A field's history is bounded, the discard is not
    /// recorded, and a history standing at the bound is one the bound may
    /// already have taken the first write out of - so the earliest value still
    /// held may itself have come from the peer. A history shorter than the bound
    /// has had nothing discarded, which is what makes the difference decidable
    /// rather than assumed.
    /// </remarks>
    public int FieldsWhoseEarlierValueIsNotKnown { get; init; }

    /// <summary>
    /// Gets how many fields this plugin wrote on items the caller did not read.
    /// </summary>
    /// <remarks>
    /// A value cannot be put back on an item nobody read, so these are neither
    /// restored nor forgotten. A number above zero means the revert covered less
    /// than what this pairing touched, which is a fact about the caller's read
    /// rather than about the record.
    /// </remarks>
    public int FieldsOnItemsNotRead { get; init; }

    /// <summary>
    /// Gets what the counts above do not say, in a sentence an operator reads.
    /// </summary>
    /// <remarks>
    /// It travels with the counts rather than being written at whatever surface
    /// shows them, because a number shown without it reads as a complete
    /// account, and this one is complete only about what the record can see.
    /// </remarks>
    public static string WhatTheseCountsDoNotSay =>
        "These counts are about the fields that were read and the record this plugin keeps. A field this plugin wrote on an item nobody read is counted separately and is not among them, and a field written by a build that kept no record at all is in none of them.";
}
