using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// Writes an account out as something an operator can take away, and reads one
/// back.
/// </summary>
/// <remarks>
/// The difference between a log and a support burden is whether the operator can
/// hand it over. A page they have to screenshot is a page somebody transcribes
/// wrongly into an issue, so the account leaves this plugin as one text with
/// everything in it.
/// <para>
/// It is the account rather than the rows. Every number
/// <see cref="ConflictAccount"/> carries goes out with them, because a file
/// carrying only the rows reads as the whole account to whoever opens it and
/// that is exactly the reading this store cannot afford.
/// </para>
/// <para>
/// The rows are written as the decision type declares them rather than as the
/// store's own line. The two carry the same columns, and a second shape written
/// out beside the first is the copy that drifts; taking the decision itself
/// means the file changes when the decision does and never on its own. What it
/// costs is that the file is not the store's file: it is nested where the line
/// is flat, and nothing reads one as the other.
/// </para>
/// <para>
/// Nothing here touches a disk. What is produced is the content of a file, and
/// where a file goes is the business of whoever hands it to an operator, which
/// is the surface #48 still owes.
/// </para>
/// </remarks>
public static class ConflictExport
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// The account as the text of a file.
    /// </summary>
    /// <param name="account">What is still held, with what is not.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentNullException">There is no account to write.</exception>
    /// <remarks>
    /// Written with line breaks and indentation rather than as one line. The
    /// reader is a person who has been asked to attach this to a bug report, and
    /// a file they can scroll is one they can also check before they send it,
    /// which is a property worth the bytes on a file bounded at five thousand
    /// rows.
    /// </remarks>
    public static string Written(ConflictAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return JsonSerializer.Serialize(account, _json);
    }

    /// <summary>
    /// An account read back out of the text of a file.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The account.</returns>
    /// <exception cref="ArgumentException">There is no text to read.</exception>
    /// <exception cref="JsonException">The text is not an account.</exception>
    /// <remarks>
    /// Reading exists so the writing can be shown to have kept everything, which
    /// is what a round trip is for. It is not a way back into the store: an
    /// account read here is a file somebody sent, and nothing merges one into
    /// what this server holds.
    /// <para>
    /// A text that is not an account is refused rather than answered with an
    /// empty one. An empty account and an unreadable file are the same object to
    /// whoever asked, and only one of them means the pairing decided nothing.
    /// </para>
    /// </remarks>
    public static ConflictAccount Read(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var account = JsonSerializer.Deserialize<ConflictAccount>(text, _json);

        if (account is null)
        {
            throw new JsonException("The text names nothing, so it is not an account.");
        }

        return account;
    }
}
