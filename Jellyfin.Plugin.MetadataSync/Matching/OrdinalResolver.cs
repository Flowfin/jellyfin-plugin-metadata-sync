using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// Resolves an item whose identity is its parent plus an ordinal, in two named
/// steps, and refuses every case where the numbering does not settle it.
/// </summary>
/// <remarks>
/// This is written for the shape rather than for episodes. An episode inside a
/// series is the case that forces it, because an episode's own identifier
/// dictionary is empty on many setups, but a season inside a series is the same
/// shape and so is anything else identified as its parent plus a count. Writing
/// it once for the shape is what keeps the answer the same when the next such
/// kind arrives.
/// <para>
/// The two steps are separate because they fail separately. The parent resolves
/// by its own provider identifiers, through the same comparison every other
/// item uses, and nothing under a parent that did not resolve is looked at at
/// all. Only then is the ordinal decided, inside that parent.
/// </para>
/// <para>
/// Nothing here resolves by proximity. There is no nearest, no next, no only
/// remaining candidate and no range that contains a number. Every one of those
/// is a published matcher's fallback, and each of them writes one episode's
/// metadata onto another while reporting a successful sync.
/// </para>
/// <para>
/// This is pure. It reads the two identities it is handed and nothing else - no
/// library, no clock, no file, no transport - so every case below is decidable
/// from a table with nothing running.
/// </para>
/// </remarks>
public static class OrdinalResolver
{
    /// <summary>
    /// Resolves one item against the candidates a peer offered for it.
    /// </summary>
    /// <param name="here">The item on this server.</param>
    /// <param name="there">The candidates the peer offered, which need not share a parent.</param>
    /// <returns>What was established, and by which step.</returns>
    /// <remarks>
    /// The candidates are not required to be filtered to one parent already.
    /// Handing in a set that was filtered somewhere else would move the first
    /// step out of here, and then the register's statement that the parent
    /// resolved would be made by whichever caller did the filtering.
    /// </remarks>
    public static OrdinalResolution Resolve(OrdinalIdentity here, IReadOnlyCollection<OrdinalIdentity> there)
    {
        ArgumentNullException.ThrowIfNull(here);
        ArgumentNullException.ThrowIfNull(there);

        var underTheSameParent = new List<OrdinalIdentity>();

        foreach (var candidate in there)
        {
            if (ProviderIdentifiers.Compare(here.ParentIdentifiers, candidate.ParentIdentifiers) == ProviderIdentifierVerdict.Match)
            {
                underTheSameParent.Add(candidate);
            }
        }

        if (underTheSameParent.Count == 0)
        {
            return Refused(OrdinalVerdict.ParentDidNotResolve, OrdinalStep.Parent, ParentDidNotResolve(there.Count));
        }

        if (!here.IsNumberedWithinASeason)
        {
            return here.AbsoluteNumber is not null
                ? Refused(OrdinalVerdict.AbsoluteNumbering, OrdinalStep.Ordinal, AbsoluteNumbering(here.AbsoluteNumber.Value))
                : Refused(OrdinalVerdict.NotNumbered, OrdinalStep.Ordinal, NotNumbered());
        }

        // Before the numbers are compared, because a range that happened to
        // begin at the wanted number would otherwise be a match on its first
        // end, which is the containment rule this refuses.
        if (here.CoversARange)
        {
            return Refused(OrdinalVerdict.CoversMoreThanOneEpisode, OrdinalStep.Ordinal, CoversMoreThanOneEpisode(here));
        }

        if (here.Season!.Value == 0)
        {
            return Refused(OrdinalVerdict.SeasonZero, OrdinalStep.Ordinal, SeasonZero(here.Number!.Value));
        }

        var atThatOrdinal = new List<OrdinalIdentity>();

        foreach (var candidate in underTheSameParent)
        {
            // A candidate covering a range is excluded by what it is rather than
            // by its numbers. It is not one episode, so it is not this one, and
            // taking it because the range begins here is containment wearing an
            // equality sign.
            if (!candidate.CoversARange
                && candidate.Season == here.Season
                && candidate.Number == here.Number)
            {
                atThatOrdinal.Add(candidate);
            }
        }

        if (atThatOrdinal.Count == 1)
        {
            return new OrdinalResolution(OrdinalVerdict.Resolved, OrdinalStep.Ordinal, atThatOrdinal[0], Resolved(here));
        }

        return atThatOrdinal.Count > 1
            ? Refused(OrdinalVerdict.OrdinalHeldTwice, OrdinalStep.Ordinal, OrdinalHeldTwice(here, atThatOrdinal.Count))
            : Refused(OrdinalVerdict.NothingAtThatOrdinal, OrdinalStep.Ordinal, NothingAtThatOrdinal(here, underTheSameParent.Count));
    }

    /// <summary>
    /// Returns the step that answers for one verdict.
    /// </summary>
    /// <param name="verdict">The verdict.</param>
    /// <returns>The step it belongs to.</returns>
    public static OrdinalStep StepFor(OrdinalVerdict verdict) =>
        verdict == OrdinalVerdict.ParentDidNotResolve ? OrdinalStep.Parent : OrdinalStep.Ordinal;

    /// <summary>
    /// Returns the declared sentence for one verdict, which is what the document
    /// renders and what an operator reading the register meets.
    /// </summary>
    /// <param name="verdict">The verdict.</param>
    /// <returns>The sentence.</returns>
    /// <remarks>
    /// Declared here rather than in the document, so the document is a rendering
    /// of this and not a second copy of it. A verdict added with no sentence
    /// stops the build rather than rendering an empty cell.
    /// </remarks>
    public static string Statement(OrdinalVerdict verdict) => verdict switch
    {
        OrdinalVerdict.Resolved =>
            "The parent resolved on its own provider identifiers and exactly one item under it carries this season and this number.",
        OrdinalVerdict.ParentDidNotResolve =>
            "No candidate's parent is the same work as this item's parent, so the ordinal was never read. An ordinal counts within a series and means nothing until the series is known.",
        OrdinalVerdict.NotNumbered =>
            "The item carries no season and number pair and no absolute number either, so it has nothing to be resolved by once its own identifiers have failed to answer.",
        OrdinalVerdict.AbsoluteNumbering =>
            "The item is numbered absolutely and carries no season and number pair. An absolute number counts through a series as one provider divided it into seasons, so reading it as a position needs that provider's season lengths, which is the thing that differs between two libraries built from different providers.",
        OrdinalVerdict.CoversMoreThanOneEpisode =>
            "The item's ordinal is a range, which is what a file holding more than one episode carries. It is not one episode, so no single item on the peer is the one it is the same as, and taking the item at either end of the range would write two episodes' metadata onto one.",
        OrdinalVerdict.SeasonZero =>
            "The item is in season zero, which is the bucket for everything a provider did not place in a numbered season. A special's position inside that bucket is assigned by whichever provider each server used, so two servers agreeing on a number there is not evidence they mean one episode.",
        OrdinalVerdict.NothingAtThatOrdinal =>
            "The parent resolved and nothing under it carries this season and this number. What lies nearest to it is not consulted, and an only remaining candidate is not taken.",
        OrdinalVerdict.OrdinalHeldTwice =>
            "The parent resolved and more than one item under it carries this season and this number. Which of them a value would be written against is not decidable from the numbering, so nothing is written.",
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, NoStatement()),
    };

    /// <summary>
    /// Spells an ordinal the way the document and a register entry spell it.
    /// </summary>
    /// <param name="identity">The item.</param>
    /// <returns>The spelling.</returns>
    internal static string Spelled(OrdinalIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!identity.IsNumberedWithinASeason)
        {
            return identity.AbsoluteNumber is not null
                ? string.Format(CultureInfo.InvariantCulture, "absolute {0}", identity.AbsoluteNumber.Value)
                : "no numbering";
        }

        var written = string.Format(CultureInfo.InvariantCulture, "S{0:00}E{1:00}", identity.Season!.Value, identity.Number!.Value);

        return identity.CoversARange
            ? string.Format(CultureInfo.InvariantCulture, "{0}-E{1:00}", written, identity.LastNumber!.Value)
            : written;
    }

    private static OrdinalResolution Refused(OrdinalVerdict verdict, OrdinalStep step, string reason) =>
        new(verdict, step, null, reason);

    private static string ParentDidNotResolve(int offered) => string.Format(
        CultureInfo.InvariantCulture,
        "None of the {0} candidate(s) offered carries a parent whose provider identifiers are the same work as this item's parent, so nothing under one was read.",
        offered);

    private static string NotNumbered() =>
        "This item carries no season and number pair and no absolute number, so there is no ordinal to resolve it by.";

    private static string AbsoluteNumbering(int absolute) => string.Format(
        CultureInfo.InvariantCulture,
        "This item is numbered absolutely, at {0}, and carries no season and number pair. This plugin does not convert between the two numberings.",
        absolute);

    private static string CoversMoreThanOneEpisode(OrdinalIdentity here) => string.Format(
        CultureInfo.InvariantCulture,
        "This item's ordinal is {0}, which covers more than one episode, so there is no one item on the peer that it is the same as.",
        Spelled(here));

    private static string SeasonZero(int number) => string.Format(
        CultureInfo.InvariantCulture,
        "This item is number {0} in season zero, which is the bucket for everything a provider did not place in a numbered season, and a position inside that bucket is not comparable across two servers.",
        number);

    private static string Resolved(OrdinalIdentity here) => string.Format(
        CultureInfo.InvariantCulture,
        "The parent resolved and exactly one item under it is {0}.",
        Spelled(here));

    private static string NothingAtThatOrdinal(OrdinalIdentity here, int underTheSameParent) => string.Format(
        CultureInfo.InvariantCulture,
        "The parent resolved and none of the {0} item(s) under it is {1}. Nothing nearer was considered.",
        underTheSameParent,
        Spelled(here));

    private static string OrdinalHeldTwice(OrdinalIdentity here, int held) => string.Format(
        CultureInfo.InvariantCulture,
        "The parent resolved and {0} items under it are {1}, so the numbering does not say which one this is.",
        held,
        Spelled(here));

    private static string NoStatement() =>
        "A verdict with no declared sentence would render an empty cell in the document and an empty reason in the register.";
}
