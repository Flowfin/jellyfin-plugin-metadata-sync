using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// What this plugin wrote, per pairing, per item and per field.
/// </summary>
/// <remarks>
/// This is the record that separates an update from a conflict, and it is the
/// reason this plan needs no clock. A local value equal to the newest value
/// here is one nobody on this server has expressed an opinion on since this
/// plugin put it there; a local value that differs was changed by somebody
/// here, however old it looks. <see cref="Conflicts.ConflictInputs"/> takes it
/// as <c>LastWrittenByThisPlugin</c> and the rules decide from that.
/// <para>
/// The key is the pairing, the item and the field, and the pairing component is
/// enough on its own. A pairing identifier is derived from the two servers'
/// public keys and a revocation is terminal, so two servers that pair again
/// after a revocation carry a different identifier and rows written under the
/// old one cannot be read as rows of the new one. That reading is recorded on
/// #26 with the documents it rests on, and it is what lets one pairing's rows be
/// found and removed by the key alone, which is #61.
/// </para>
/// <para>
/// It is an interface so a caller can be given one that keeps nothing, and so
/// the half of a pass that writes can be arranged in a test with no disk in the
/// room. There is exactly one implementation that persists, and it is
/// <see cref="WrittenValues"/>.
/// </para>
/// </remarks>
public interface IWrittenValues
{
    /// <summary>
    /// Records a value this plugin has just written.
    /// </summary>
    /// <param name="pairingId">The pairing the value arrived on.</param>
    /// <param name="itemId">The item on this server that was written.</param>
    /// <param name="field">The field, named as the register names it.</param>
    /// <param name="value">The value that was written, or null where the write cleared the field.</param>
    /// <param name="previousValue">The value that was on the item before this write, or null where the field held nothing.</param>
    /// <remarks>
    /// The previous value is a parameter rather than something this store works
    /// out, and that is the point of it. A store deriving it from its own newest
    /// entry would answer with the value this plugin wrote last time, which on
    /// the field this whole record exists for is not what was replaced: an
    /// operator edited it in between, and the edit is exactly what would be lost.
    /// The caller is the write path, it has both values on the plan row, and it
    /// is the only place that knows what the library held at the moment of the
    /// write.
    /// </remarks>
    void Record(Guid pairingId, Guid itemId, string field, string? value, string? previousValue);

    /// <summary>
    /// The newest value this plugin wrote for one field on one item, or null
    /// where it has never written one.
    /// </summary>
    /// <param name="pairingId">The pairing to ask about.</param>
    /// <param name="itemId">The item on this server.</param>
    /// <param name="field">The field, named as the register names it.</param>
    /// <returns>The value last written, or null where there is no record of one.</returns>
    /// <remarks>
    /// A null answer means no record rather than a recorded null. The two are
    /// different states and a caller that needs to tell them apart asks
    /// <see cref="History"/>, whose empty list is the first of them.
    /// </remarks>
    string? LastWritten(Guid pairingId, Guid itemId, string field);

    /// <summary>
    /// Every write this plugin still holds for one field on one item, oldest
    /// first, each carrying the value written and the value it replaced.
    /// </summary>
    /// <param name="pairingId">The pairing to ask about.</param>
    /// <param name="itemId">The item on this server.</param>
    /// <param name="field">The field, named as the register names it.</param>
    /// <returns>The writes still held, oldest first, empty where there is no record.</returns>
    /// <remarks>
    /// Bounded, and the bound is <see cref="WrittenValues.Bound"/>. A caller
    /// reading this as a full history is reading it wrong: what a bound has
    /// discarded is gone, and #66 is where a surface reporting on it has to say
    /// so rather than report a clean number.
    /// </remarks>
    IReadOnlyList<WrittenValue> History(Guid pairingId, Guid itemId, string field);

    /// <summary>
    /// Every field on every item this plugin has a record of writing for one
    /// pairing.
    /// </summary>
    /// <param name="pairingId">The pairing to ask about.</param>
    /// <returns>The keys held, ordered by item and then by field, empty where there is no record.</returns>
    /// <remarks>
    /// This is what makes the set of fields one pairing touched enumerable
    /// without reading a library, which is #64's first condition and is a
    /// property of the key rather than of an index: the pairing is a component
    /// of every key, so the answer is a filter over what this store holds and
    /// never a walk over items.
    /// <para>
    /// It answers keys and never counts. A caller learns which fields to ask
    /// about and asks <see cref="History"/> about each one, so nothing here
    /// separates a field whose earliest write the bound discarded from a field
    /// written exactly <see cref="WrittenValues.Bound"/> times - which is the
    /// disclosure in <c>docs/storage.md</c> and is unchanged by this member
    /// existing.
    /// </para>
    /// </remarks>
    IReadOnlyList<WrittenField> Fields(Guid pairingId);
}
