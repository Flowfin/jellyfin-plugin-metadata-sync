using System;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The two fixture rows about a character with no glyph, and the notation they
/// are written in.
/// </summary>
/// <remarks>
/// The rows themselves are run by <see cref="ConflictResolverTests"/> with
/// every other row, so nothing here re-runs the resolver. What is held here is
/// the part those runs cannot see: that the value the rule set was handed is
/// the character the row names, that the document still names it rather than
/// carrying it, and that the premise one of the two rows rests on is the
/// runtime's answer rather than an assumption written into a table.
/// <para>
/// A row whose notation silently failed to expand would be two rows comparing
/// ordinary text, answering exactly as the document says they do, and proving
/// nothing at all. That is the failure this file exists for.
/// </para>
/// </remarks>
public class ConflictInvisibleCharacterTests
{
    /// <summary>
    /// The zero-width space, built from its codepoint rather than typed. The
    /// character cannot appear in this file either: <c>unicode-guard</c> reads
    /// tracked text and this is tracked text.
    /// </summary>
    private static readonly string _zeroWidthSpace = char.ConvertFromUtf32(0x200B);

    private const string OneCharacterCase = "the peer's value is one character with no glyph";

    private const string DifferByOneCase = "the two values differ only by a character with no glyph";

    /// <summary>
    /// The notation reaches the rule set as the character it names, in both
    /// rows: alone in one and inside a value in the other.
    /// </summary>
    [Fact]
    public void TheNotationArrivesAsTheCharacterTheRowNames()
    {
        var alone = ConflictFixtures.InputsFor(ConflictFixtures.Named(OneCharacterCase));

        Assert.Equal(_zeroWidthSpace, alone.PeerValue);

        var inside = ConflictFixtures.InputsFor(ConflictFixtures.Named(DifferByOneCase));

        Assert.Equal(inside.PeerValue + _zeroWidthSpace, inside.LocalValue);
        Assert.NotEqual(inside.PeerValue, inside.LocalValue);
    }

    /// <summary>
    /// The document names the codepoint and does not carry the character. A
    /// literal pasted in place of the name reads identically to a person and
    /// reddens a required check on the server, so it is refused here too, where
    /// whoever pasted it is already running the suite.
    /// </summary>
    [Fact]
    public void TheDocumentNamesTheCodepointRatherThanCarryingIt()
    {
        foreach (var caseName in new[] { OneCharacterCase, DifferByOneCase })
        {
            var row = ConflictFixtures.Named(caseName);
            var cells = string.Concat(row.Skip(1).Take(3));

            Assert.Contains("<U+200B>", cells, StringComparison.Ordinal);
            Assert.DoesNotContain(_zeroWidthSpace, cells, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A notation the reader does not understand is refused rather than passed
    /// through as text.
    /// </summary>
    /// <remarks>
    /// This is the near miss worth spending the effort on. Three digits instead
    /// of four is the mistake somebody makes writing the next one of these
    /// rows, and a reader that let it through would hand the rule set two values
    /// differing by an angle bracket while the row beside them said they differ
    /// by a character nobody can see. Both refuse, so the row would stay green
    /// and stop being about anything.
    /// </remarks>
    [Theory]
    [InlineData("<U+20B>")]
    [InlineData("<U+200b>")]
    [InlineData("<U+>")]
    [InlineData("A description<U+200")]
    public void ANotationTheReaderDoesNotUnderstandIsRefused(string written)
    {
        Assert.ThrowsAny<Exception>(() => ConflictFixtures.Expand(written));
    }

    /// <summary>
    /// The notation the reader does understand is expanded and nothing else in
    /// the value is touched, so the guard above refuses a malformed escape
    /// rather than refusing the notation.
    /// </summary>
    [Fact]
    public void TheNotationTheReaderUnderstandsIsExpanded()
    {
        Assert.Equal("A description" + _zeroWidthSpace, ConflictFixtures.Expand("A description<U+200B>"));
        Assert.Equal("A description", ConflictFixtures.Expand("A description"));
    }

    /// <summary>
    /// The premise one of the two rows rests on, pinned rather than assumed.
    /// </summary>
    /// <remarks>
    /// The row where the peer's value is one zero-width space expects a refusal,
    /// and it expects one because the rule that answers an absent peer value
    /// asks the runtime whether the value is whitespace and is told it is not.
    /// If that answer ever moves, that row's outcome moves with it, and the row
    /// alone would report the change as the resolver being wrong. This leg says
    /// where the answer came from.
    /// </remarks>
    [Fact]
    public void TheRuntimeReadsAZeroWidthSpaceAsTextRatherThanAsWhitespace()
    {
        Assert.False(string.IsNullOrWhiteSpace(_zeroWidthSpace));
        Assert.True(string.IsNullOrWhiteSpace(" "));
    }
}
