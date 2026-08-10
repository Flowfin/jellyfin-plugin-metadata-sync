namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// One reason a configuration cannot be acted on.
/// </summary>
/// <remarks>
/// The property is carried beside the sentence rather than only inside it, so
/// a page can put the message next to the control that caused it. A validator
/// that answers only with prose leaves whoever renders it matching strings to
/// work out which field to point at.
/// </remarks>
public sealed class ConfigurationProblem
{
    internal ConfigurationProblem(string property, string message)
    {
        Property = property;
        Message = message;
    }

    /// <summary>
    /// Gets the configuration property this is about, spelled as the property
    /// is spelled.
    /// </summary>
    public string Property { get; }

    /// <summary>
    /// Gets the sentence an operator reads. It names the property and says what
    /// is wrong with the value, never what the operator should have done.
    /// </summary>
    public string Message { get; }
}
