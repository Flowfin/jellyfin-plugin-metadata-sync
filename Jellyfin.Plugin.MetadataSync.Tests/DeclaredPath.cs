using System;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Resolves a path that a committed document declares against a directory this
/// run copied.
///
/// The three legs that read a document this way each built the path with
/// <c>Path.Combine</c>, which discards everything before a rooted later
/// argument. A document naming an absolute path therefore dropped the base
/// directory silently, and the leg then answered about a file somewhere else on
/// the machine rather than about this tree. Two of the three decide whether a
/// guard passes, so the failure is a guard that can be made to answer the wrong
/// question by an edit that reviews as prose.
///
/// A document is committed text rather than input from a stranger, so this is
/// not a remote attack surface. It is fixed as a pattern rather than at each
/// call site so that the fourth reading written later does not have to
/// remember.
///
/// The join is <c>Path.Join</c>, which keeps both parts whatever the second one
/// looks like, so the discard cannot happen even if a refusal below were
/// removed. NO LEG ASSERTS THAT, and the absence is deliberate rather than an
/// oversight: the refusals stop a rooted path before it reaches the join, so a
/// leg written for the join could only hand it a path the refusals allow, on
/// which <c>Path.Combine</c> and <c>Path.Join</c> return the same string. It
/// would be a leg that cannot fail. What holds the join is the choice recorded
/// here.
///
/// The refusals are what this type is for: a declared path that leaves the base
/// is a defect in the document, and answering about a file outside the tree is
/// worse than saying so.
///
/// A backslash counts as a separator here whatever platform the run is on. A
/// document in this tree writes its paths with forward slashes, so a backslash
/// segment is a Windows-flavoured path in a document rather than a file name,
/// and reading it as one segment on Linux would let <c>..\secrets.md</c>
/// through the climb check on one runner and not the other.
/// </summary>
internal static class DeclaredPath
{
    /// <summary>
    /// Resolves a path a document declares, inside the base it is declared
    /// relative to.
    /// </summary>
    /// <param name="document">The document the path was read out of, named in
    /// the refusal so a reader knows which file to edit.</param>
    /// <param name="baseDirectory">The directory the declared path is relative
    /// to.</param>
    /// <param name="declared">The path exactly as the document writes it, with
    /// forward slashes.</param>
    /// <returns>The file beside the test binary.</returns>
    /// <exception cref="InvalidOperationException">The declared path is rooted
    /// or climbs out of the base.</exception>
    public static string Resolve(string document, string baseDirectory, string declared)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        ArgumentNullException.ThrowIfNull(declared);

        if (Path.IsPathRooted(declared))
        {
            throw new InvalidOperationException(
                $"{document} declares the rooted path '{declared}'. A path a document declares is resolved inside {baseDirectory}, and a rooted one would answer about a file elsewhere on this machine.");
        }

        // The filter is explicit rather than a loop that breaks on a match.
        // `cs/linq/missed-where` raised the loop this replaces, on the run of
        // the change that landed it, and a note the scan raises against a file
        // written the day before is fixed rather than dismissed.
        if (declared.Split('/', '\\').Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{document} declares the path '{declared}', which climbs out of {baseDirectory}. A path a document declares is resolved inside that directory and nowhere else.");
        }

        return Path.Join(baseDirectory, declared.Replace('/', Path.DirectorySeparatorChar));
    }
}
