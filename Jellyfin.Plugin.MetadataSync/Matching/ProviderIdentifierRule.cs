namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// One row of the comparison table: how one provider's identifier is compared.
/// </summary>
public sealed class ProviderIdentifierRule
{
    internal ProviderIdentifierRule(string provider, string valueComparison, bool trimmed, string normalisation, string reason)
    {
        Provider = provider;
        ValueComparison = valueComparison;
        Trimmed = trimmed;
        Normalisation = normalisation;
        Reason = reason;
    }

    /// <summary>
    /// Gets the provider this row is about, as the dictionary key spells it.
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// Gets how the identifier itself is compared, either <c>Ordinal</c> or
    /// <c>OrdinalIgnoreCase</c>.
    /// </summary>
    public string ValueComparison { get; }

    /// <summary>
    /// Gets a value indicating whether surrounding whitespace is removed before
    /// the comparison.
    /// </summary>
    public bool Trimmed { get; }

    /// <summary>
    /// Gets the per-provider normalisation, either <c>none</c> or
    /// <c>LeadingZeros</c>.
    /// </summary>
    public string Normalisation { get; }

    /// <summary>
    /// Gets the sentence this row is argued by.
    /// </summary>
    public string Reason { get; }
}
