using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Fields;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Every row in the register is a case, and the register is where the cases
/// come from.
/// </summary>
/// <remarks>
/// Every other rule in this tree is argued by a table of cases in the document
/// that states it, and a reader in this suite turns those rows into theory data.
/// The register has no such table, and this file is the reason rather than the
/// absence: the register already is one. It carries a row per field, the rows
/// are held against the item type and against the server's per-user record so a
/// field with no row reds, and each row states the outcome it wants in its own
/// moves column. A table of cases beside it would be a second declaration of the
/// set the register exists to be the only declaration of.
/// <para>
/// What was missing is the half that makes that argument worth anything, which
/// is that every row reaches a theory. The rows that move are run end to end by
/// <see cref="LibraryPlanTargetTests.EveryFieldThisPathCanWriteArrivesOnTheItem"/>,
/// and the two that path refuses to spell are run by the theory beside it, off
/// the register rather than off a list. The rows that do not move were run
/// one at a time, by name, in lists written beside the four legs that use them,
/// and most of them were declared and never asked. A row nobody runs reads
/// exactly like coverage, which is the failure <see cref="FixtureTableTests"/>
/// exists for one document over.
/// </para>
/// <para>
/// So the theory below takes its cases from the register, and the leg after it
/// closes the set across both routes rather than only its own, because a split
/// that stops reaching a row leaves that row declared and never asked again.
/// </para>
/// <para>
/// The legs that name their rows stay where they are. Three of the four assert
/// something this theory does not: that the set they run is closed against what
/// the server declares, so a field the server adds later arrives with no row and
/// reds. The fourth is the near-miss for a register read leniently, and it is
/// worth reading beside the row it is about rather than as one case in a
/// generated list. This runs every row; it does not replace what those legs say
/// about theirs.
/// </para>
/// <para>
/// What this does not reach. It asks the register for one field and never runs
/// a pass, so nothing here says the right item was chosen or that a
/// refusal was recorded anywhere. It reads the refusal's text, so a row whose
/// sentence is wrong about why it refuses passes exactly like one that is right.
/// And it holds no row that moves: whether a value arrives is the leg named
/// above, and the closure leg is what stops the two halves drifting apart.
/// </para>
/// </remarks>
public class RegisterCaseTests
{
    /// <summary>
    /// Gets one case per row the register says does not move.
    /// </summary>
    public static IEnumerable<object[]> RowsThatDoNotMove =>
        FieldRegister.Rows.Where(row => !row.Moves).Select(row => new object[] { row.Field });

    /// <summary>
    /// A row that does not move is refused when something asks for it, and the
    /// refusal quotes that row's own sentence.
    /// </summary>
    /// <remarks>
    /// The quoting is the part worth asserting rather than the throw. A caller
    /// told only that a field is not declared goes looking for a row that is
    /// sitting right there saying why it refuses, and the two states need
    /// different repairs: one is a register that forgot a field and the other is
    /// a register that decided against it.
    /// </remarks>
    /// <param name="field">The field the row names.</param>
    [Theory]
    [MemberData(nameof(RowsThatDoNotMove))]
    public void ARowThatDoesNotMoveIsRefusedQuotingItsOwnReason(string field)
    {
        var row = FieldRegister.Find(field);
        Assert.NotNull(row);
        Assert.False(row.Moves);

        var refused = Assert.Throws<FieldNotDeclaredException>(
            () => FieldRegister.RequireMovable(field));

        Assert.Contains(field, refused.Message, StringComparison.Ordinal);
        Assert.Contains(row.Reason, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every row in the register reaches a case, by one of the two routes, and
    /// no row reaches both.
    /// </summary>
    /// <remarks>
    /// This is what stops the cases drifting back into a list written beside the
    /// legs. A filter narrowed above, or a row the split stops reaching, leaves
    /// rows declared and never asked, which is the state this file exists to
    /// end. The moving side is taken from the write path's own two sets rather
    /// than from the register a second time, so this leg reads the thing that
    /// actually runs those rows; those sets are held equal to the rows that move
    /// by <see cref="LibraryPlanTargetTests.TheWritersAndTheRefusedFieldsAreExactlyWhatTheRegisterLetsMove"/>,
    /// which is the leg that would catch the two disagreeing.
    /// <para>
    /// Both sides are asserted to be non-empty before anything is concluded. A
    /// split reaching nothing at all satisfies a comparison against itself, and
    /// that is the shape a register read wrongly would produce.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryRowInTheRegisterReachesACaseByOneRouteOrTheOther()
    {
        var refusing = RowsThatDoNotMove.Select(one => (string)one[0]).ToList();
        var moving = LibraryPlanTarget.WritableFields
            .Concat(LibraryPlanTarget.FieldsWithNoSpelling)
            .ToList();

        Assert.NotEmpty(refusing);
        Assert.NotEmpty(moving);
        Assert.Empty(refusing.Intersect(moving, StringComparer.Ordinal));

        var run = refusing.Concat(moving).Order(StringComparer.Ordinal).ToList();
        var declared = FieldRegister.Rows.Select(row => row.Field).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(declared, run);
    }
}
