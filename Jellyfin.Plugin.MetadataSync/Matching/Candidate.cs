using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// One item offered as a possible answer to which item a work is here, which is
/// the identity it is reported by and the provider identifiers it carries.
/// </summary>
/// <remarks>
/// The identity is a string rather than this server's own item identifier, so
/// one type describes a candidate on either server. A set of candidates the peer
/// could not settle between is the same question with a different set behind it,
/// and a second type for it would be the first step towards a second answer.
/// <para>
/// What is absent is what <see cref="OrdinalIdentity"/> argues for its own shape.
/// There is no path, no filename, no size and no hash, so a rule reading one
/// could not be written against this without changing the shape first.
/// </para>
/// </remarks>
public sealed class Candidate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Candidate"/> class.
    /// </summary>
    /// <param name="id">How this candidate is named back to whoever offered it.</param>
    /// <param name="identifiers">The provider identifiers this candidate carries, as its own dictionary spells them.</param>
    public Candidate(string id, IReadOnlyDictionary<string, string> identifiers)
    {
        // An identity that is absent or nothing but space is one an ambiguity
        // could not be reported by, and an ambiguity nobody can name is the
        // silent drop this refusal exists against.
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(identifiers);

        Id = id;
        Identifiers = identifiers;
    }

    /// <summary>
    /// Gets how this candidate is named back to whoever offered it. It is also
    /// what an ambiguous answer is ordered by, so that answer does not depend on
    /// the order the candidates arrived in.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the provider identifiers this candidate carries.
    /// </summary>
    public IReadOnlyDictionary<string, string> Identifiers { get; }
}
