using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps the plugin's own store to what it is allowed to hold, and out of
/// anything that names the server on the other end.
/// </summary>
/// <remarks>
/// #20's rule is that this plugin holds nothing that would let it work without
/// the pairing plugin present: no key material, no peer address, no credential,
/// no pairing state. Its fourth condition asks that the plugin stores nothing
/// identifying a peer beyond the pairing identity the contract hands it, and
/// that condition was true for a reason that has expired. It held because
/// nothing here wrote anything to a disk, which is a stronger statement than the
/// condition and a weaker guard: it stopped holding the moment a store existed.
/// One does now, so the condition is answered by a row in
/// <c>docs/storage.md</c> - key material, a peer address, a credential, a
/// pairing secret, kept in neither place - and by nothing that would refuse a
/// change to it.
/// <para>
/// Three legs, because no one of them covers what the other two do. The lines
/// this store writes are allow-listed member by member, so a member added to a
/// line fails until somebody puts it in the set with its reason; that is the
/// only one of the three that catches a peer address spelled as a string, which
/// is the shape it will actually arrive in. The bytes on the disk are read back
/// after a real write, so a member the type does not declare - a serialiser
/// setting, a second writer, a name chosen at the site rather than on the type -
/// is caught where the allow-list cannot see it. And every member of every type
/// in the store is refused for being MADE OF an address or a transport, which
/// catches the ones that never reach a line at all.
/// </para>
/// <para>
/// It is a different question from <see cref="TransportReachabilityTests"/>,
/// whose vocabulary this shares rather than copies. That walk follows the calls
/// a pass makes and refuses arriving at a transport; this reads what the store's
/// types are DECLARED to be made of, which a call graph does not answer. A field
/// of type <c>Uri</c> on a line nobody has serialised yet is reached by no call
/// and is refused here.
/// </para>
/// <para>
/// What none of the three can do, stated rather than left to be found. A peer
/// address is a string, and no reading of a type says which string is one: the
/// allow-list refuses a member NAMED for a peer arriving at all, and it cannot
/// refuse an address stuffed into <c>Value</c>, which is indistinguishable from
/// the library value that member exists for. What makes that bound narrower than
/// it sounds is that this plugin has nowhere to obtain a peer address from,
/// which is #20's first condition rather than this one. The disk leg reads the
/// store this plugin writes and nothing another route writes into the same
/// directory. And a nested type that is not a line - a helper somebody adds
/// inside a store - is refused by the first leg until it is named, which is a
/// false positive that fails safe and costs one edit.
/// </para>
/// </remarks>
public class StoreShapeTests
{
    /// <summary>
    /// What each line the store writes may carry, and why each member is here.
    /// Every one of the six is either this plugin's own vocabulary, an
    /// identifier this server already holds, or a value out of this server's own
    /// library.
    /// <list type="bullet">
    /// <item><description>
    /// Pairing. The pairing identity the contract hands over, which is the one
    /// thing about the other server #20 permits this plugin to hold. It is a
    /// digest of two public keys, it names no person and no address, and a
    /// revocation is terminal so it cannot be read as the pairing that replaced
    /// it.
    /// </description></item>
    /// <item><description>
    /// Item. An item on THIS server, under the identifier this server already
    /// files it under. Nothing about the peer's copy is filed here.
    /// </description></item>
    /// <item><description>
    /// Field. A field named as the register names it, which is this plugin's own
    /// vocabulary rather than anything out of a library.
    /// </description></item>
    /// <item><description>
    /// Value. What this plugin wrote to this server's library. It is library
    /// data, which is what the store exists to hold and what the split with the
    /// configuration is for.
    /// </description></item>
    /// <item><description>
    /// Previous. What that write replaced, for the same reason. It is the one
    /// thing a revert has to put back and the one thing a conflict log entry has
    /// to show.
    /// </description></item>
    /// <item><description>
    /// Format. Which shape the files in this directory are written in. Nobody
    /// chooses it and it says nothing about anybody; what it buys is that a
    /// store written by a newer build is refused rather than dropped to what this
    /// build understands.
    /// </description></item>
    /// </list>
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> _allowedLineMembers = new(StringComparer.Ordinal)
    {
        ["Jellyfin.Plugin.MetadataSync.Store.WrittenValues+Row"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Pairing",
            "Item",
            "Field",
            "Value",
            "Previous",
        },
        ["Jellyfin.Plugin.MetadataSync.Store.StoreFormat+Declaration"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Format",
        },

        // The progress record carries two of the five above and nothing else.
        // Pairing is the same pairing identity, for the same reason; Item is an
        // item on this server, under the identifier this server already files it
        // under. What is deliberately absent is the rest: this line says an item
        // was finished with and never what was written to it, so a value out of
        // a library cannot reach this file at all.
        ["Jellyfin.Plugin.MetadataSync.Store.PassProgress+Row"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Pairing",
            "Item",
        },

        // The account of a decision carries the three above and the columns that
        // make a decision arguable. Local and Peer are the two values as the row
        // shows them, cut, which is library data on this server and library data
        // the peer sent about a work rather than about anybody; LocalCut and
        // PeerCut say whether the cut took anything, which is a flag and names
        // nothing. Rule and Outcome are this plugin's own vocabulary, declared in
        // its own table. Direction is which way the pairing moves, which is a
        // choice an operator made. At is a moment on this server's clock, never
        // one the peer produced. Reached is a position in this pairing's own log,
        // which is how the bound's losses are counted without keeping what was
        // lost; it names nothing and nobody. What is absent is the same thing
        // absent everywhere else here: nothing about the peer's copy of the item
        // and nothing about where the peer is.
        ["Jellyfin.Plugin.MetadataSync.Store.ConflictLog+Row"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Pairing",
            "Reached",
            "Item",
            "Field",
            "Local",
            "LocalCut",
            "Peer",
            "PeerCut",
            "Rule",
            "Outcome",
            "Direction",
            "At",
        },
    };

    /// <summary>
    /// The namespace the store is declared in. A nested type carries the
    /// namespace of the type it is declared in, so this reaches the lines as
    /// well as the stores.
    /// </summary>
    private const string StoreNamespace = "Jellyfin.Plugin.MetadataSync.Store";

    /// <summary>
    /// The first rule. Every line this store writes is one somebody has written
    /// an allowed set for, in both directions, so a second file shape added
    /// later is refused until its members are argued rather than silently
    /// admitted.
    /// </summary>
    [Fact]
    public void EveryLineTheStoreWritesHasAnAllowedSet()
    {
        Assert.Equal(
            _allowedLineMembers.Keys.Order(StringComparer.Ordinal).ToList(),
            LineTypes().Select(Named).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The second rule, and the one that catches the shape a peer address will
    /// actually arrive in. A member on a line that nobody put in the allowed set
    /// fails, whatever its type, so admitting one is a decision with a reason
    /// beside it rather than a diff nobody reads.
    /// </summary>
    [Fact]
    public void NoLineCarriesAMemberOutsideItsAllowedSet()
    {
        var outside = new List<string>();

        foreach (var line in LineTypes())
        {
            if (!_allowedLineMembers.TryGetValue(Named(line), out var allowed))
            {
                continue;
            }

            outside.AddRange(DeclaredMemberNames(line)
                .Where(member => !allowed.Contains(member))
                .Select(member => Named(line) + "." + member));
        }

        Assert.Empty(outside.Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The third rule, read off the disk rather than off a type. A store is
    /// written by a serialiser, and what a serialiser puts on the disk is not
    /// obliged to be the member set of the type it was handed: a name chosen at
    /// the writing site, a second writer into the same file, or a setting that
    /// adds a member all produce bytes the allow-list above cannot see.
    /// </summary>
    /// <remarks>
    /// The comparison is case-insensitive because one store writes its stamp in a
    /// different case from the member that declares it and reads it back
    /// case-insensitively. A guard refusing on that difference would be refusing
    /// a spelling rather than the thing this is about.
    /// </remarks>
    [Fact]
    public void TheBytesOnTheDiskCarryNothingOutsideTheAllowedSets()
    {
        using var directory = new TemporaryDirectory();

        var store = new WrittenValues(directory.Path);
        store.Record(
            new Guid("cccccccc-0000-0000-0000-000000000003"),
            new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            "Overview",
            "what the peer said",
            "what was here before");

        new PassProgress(directory.Path).Completed(
            new Guid("cccccccc-0000-0000-0000-000000000003"),
            new Guid("aaaaaaaa-0000-0000-0000-000000000001"));

        new ConflictLog(directory.Path).Record(
            new Guid("cccccccc-0000-0000-0000-000000000003"),
            new ConflictEntry
            {
                Item = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
                Field = "Overview",
                LocalValue = ShownValue.Of("what was here before"),
                PeerValue = ShownValue.Of("what the peer said"),
                Rule = "peer-field-locked",
                Outcome = ConflictOutcome.Refuse,
                Direction = SyncDirection.TwoWay,
                At = DateTimeOffset.UnixEpoch,
            });

        var read = 0;
        var outside = new List<string>();

        foreach (var (file, line) in FilesTheStoresWrite())
        {
            var path = Path.Combine(directory.Path, file);

            if (!File.Exists(path) || !_allowedLineMembers.TryGetValue(line, out var allowed))
            {
                continue;
            }

            foreach (var text in File.ReadAllLines(path).Where(text => text.Length > 0))
            {
                using var document = JsonDocument.Parse(text);
                read++;

                outside.AddRange(document.RootElement.EnumerateObject()
                    .Select(member => member.Name)
                    .Where(member => !allowed.Contains(member))
                    .Select(member => file + ": " + member));
            }
        }

        Assert.Empty(outside.Order(StringComparer.Ordinal).ToList());

        // A comparison over no lines agrees with anything. Four files are
        // written by the three calls above, so a run that read fewer than four
        // read a store that stopped writing one of them.
        Assert.Equal(4, read);
    }

    /// <summary>
    /// The fourth rule, and the one that reaches what never becomes a line. No
    /// member of any type in the store is made of a way off this machine or of a
    /// type an address is spelled as, so a peer address is refused by what it is
    /// made of rather than by what it is called.
    /// </summary>
    [Fact]
    public void NoMemberOfTheStoreIsMadeOfAnAddressOrATransport()
    {
        var vocabulary = TransportReachabilityTests.TransportVocabulary().ToHashSet(StringComparer.Ordinal);

        var refused = StoreMembers()
            .Where(member => MadeOf(member.Type).Any(vocabulary.Contains))
            .Select(member => member.Site + " is " + (member.Type.FullName ?? member.Type.Name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(refused);
    }

    /// <summary>
    /// Each of the four rules reads something. A scan that found no store, no
    /// line and no member passes all of them on any tree at all, which is the one
    /// way this file can report success without having looked.
    /// </summary>
    [Fact]
    public void TheScanReadsTheStoreThatIsInTheTree()
    {
        var stores = StoreTypes().Select(type => type.FullName).ToList();

        Assert.Contains("Jellyfin.Plugin.MetadataSync.Store.WrittenValues", stores, StringComparer.Ordinal);
        Assert.Contains("Jellyfin.Plugin.MetadataSync.Store.PassProgress", stores, StringComparer.Ordinal);
        Assert.Contains("Jellyfin.Plugin.MetadataSync.Store.ConflictLog", stores, StringComparer.Ordinal);
        Assert.Contains("Jellyfin.Plugin.MetadataSync.Store.StoreFormat", stores, StringComparer.Ordinal);

        Assert.NotEmpty(LineTypes());
        Assert.All(LineTypes(), line => Assert.NotEmpty(DeclaredMemberNames(line)));
        Assert.NotEmpty(StoreMembers().ToList());
    }

    /// <summary>
    /// The bite for the fourth rule, run rather than argued. These are the shapes
    /// a store acquires when somebody decides it should remember where the peer
    /// was, and the last two are the ones a guard reading only the property's own
    /// type would walk past.
    /// </summary>
    /// <param name="offered">A type a member might have.</param>
    [Theory]
    [InlineData(typeof(Uri))]
    [InlineData(typeof(UriBuilder))]
    [InlineData(typeof(System.Net.IPAddress))]
    [InlineData(typeof(System.Net.Http.HttpClient))]
    [InlineData(typeof(Uri[]))]
    [InlineData(typeof(Dictionary<Guid, Uri>))]
    public void AnAddressIsRefusedByItsShape(Type offered)
    {
        var vocabulary = TransportReachabilityTests.TransportVocabulary().ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(MadeOf(offered).Where(vocabulary.Contains).ToList());
    }

    /// <summary>
    /// The neighbour. These are what a stored row is made of, and a rule that
    /// refused them would refuse the store this plugin already has. A collection
    /// of identifiers sits one type argument away from the collection of
    /// addresses refused above.
    /// </summary>
    /// <param name="offered">A type a member might have.</param>
    [Theory]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(WrittenValue))]
    [InlineData(typeof(IReadOnlyList<string>))]
    [InlineData(typeof(Dictionary<Guid, List<WrittenValue>>))]
    public void WhatALineIsMadeOfIsAccepted(Type offered)
    {
        var vocabulary = TransportReachabilityTests.TransportVocabulary().ToHashSet(StringComparer.Ordinal);

        Assert.Empty(MadeOf(offered).Where(vocabulary.Contains).ToList());
    }

    /// <summary>
    /// The stores this plugin declares, found rather than listed, which is the
    /// same reading <see cref="ReadmeStatementTests"/> makes for its own list.
    /// </summary>
    /// <returns>The concrete types implementing the store shape.</returns>
    private static IReadOnlyList<Type> StoreTypes() =>
        typeof(Plugin).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IPairingStore).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The types that decide what a line on the disk carries. Each store keeps
    /// one, declared inside it so the member names on the disk are decided at the
    /// store rather than at whichever site happens to write a line.
    /// </summary>
    /// <returns>The line types.</returns>
    private static IReadOnlyList<Type> LineTypes() =>
        StoreTypes()
            .SelectMany(type => type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            .Where(type => !Generated(type.Name))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Each store's file, beside the type that says what one of its lines
    /// carries.
    /// </summary>
    /// <returns>The file name and the line type's name, per store.</returns>
    private static IEnumerable<(string File, string Line)> FilesTheStoresWrite()
    {
        foreach (var store in StoreTypes())
        {
            var file = store.GetField("FileName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetRawConstantValue() as string;

            var line = store.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(type => !Generated(type.Name));

            if (file is not null && line is not null)
            {
                yield return (file, Named(line));
            }
        }
    }

    /// <summary>
    /// Every member every type in the store is made of: what each type declares
    /// as a field, as a property, and as what a method takes and hands back.
    /// </summary>
    /// <returns>The site and the type at it.</returns>
    private static IEnumerable<(string Site, Type Type)> StoreMembers()
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in typeof(Plugin).Assembly.GetTypes()
                     .Where(type => string.Equals(type.Namespace, StoreNamespace, StringComparison.Ordinal))
                     .Where(type => !Generated(type.Name))
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            foreach (var field in type.GetFields(Declared).Where(field => !Generated(field.Name)))
            {
                yield return (Named(type) + "." + field.Name, field.FieldType);
            }

            foreach (var property in type.GetProperties(Declared))
            {
                yield return (Named(type) + "." + property.Name, property.PropertyType);
            }

            foreach (var method in type.GetMethods(Declared).Where(method => !Generated(method.Name)))
            {
                yield return (Named(type) + "." + method.Name + "()", method.ReturnType);

                foreach (var parameter in method.GetParameters())
                {
                    yield return (Named(type) + "." + method.Name + "(" + parameter.Name + ")", parameter.ParameterType);
                }
            }

            foreach (var constructor in type.GetConstructors(Declared))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    yield return (Named(type) + ".new(" + parameter.Name + ")", parameter.ParameterType);
                }
            }
        }
    }

    /// <summary>
    /// What a type is made of: itself, what it is a collection of, and what it
    /// is a collection of after that. A member holding a map of identifiers to
    /// addresses is made of an address, and a reader stopping at the map's own
    /// name would not say so.
    /// </summary>
    /// <param name="type">The type at a member.</param>
    /// <returns>The names of every type it is made of, itself included.</returns>
    private static IEnumerable<string> MadeOf(Type type)
    {
        var seen = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(type);

        while (pending.Count > 0)
        {
            var next = pending.Pop();

            if (!seen.Add(next))
            {
                continue;
            }

            yield return next.FullName ?? next.Name;

            if (next.HasElementType && next.GetElementType() is { } element)
            {
                pending.Push(element);
            }

            foreach (var argument in next.GetGenericArguments())
            {
                pending.Push(argument);
            }
        }
    }

    /// <summary>
    /// The members a line type declares, with the fields the compiler writes
    /// behind a property left out: those carry the property's own type and would
    /// be counted twice under a name nobody chose.
    /// </summary>
    /// <param name="line">The line type.</param>
    /// <returns>The member names.</returns>
    private static IReadOnlyList<string> DeclaredMemberNames(Type line)
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return line.GetProperties(Declared).Select(property => property.Name)
            .Concat(line.GetFields(Declared).Where(field => !Generated(field.Name)).Select(field => field.Name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// A name the compiler wrote rather than a person. A lambda, an iterator and
    /// a backing field all arrive as members nobody declared, and refusing them
    /// would be refusing the language.
    /// </summary>
    /// <param name="name">The member or type name.</param>
    /// <returns>Whether the compiler wrote it.</returns>
    private static bool Generated(string name) => name.Contains('<', StringComparison.Ordinal);

    /// <summary>
    /// A type as this file names it, which is the runtime's own spelling with the
    /// declaring type and a plus sign in front of a nested one.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The name.</returns>
    private static string Named(Type type) => type.FullName ?? type.Name;

    /// <summary>
    /// A directory of its own per case, removed afterwards. A store is a file,
    /// and a suite that shared one would have cases that pass in the order
    /// somebody ran them.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "metadata-sync-store-shape-" + Guid.NewGuid().ToString("n", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A case that has proved what it came to prove is not failed by a
                // directory the operating system is still holding open.
            }
        }
    }
}
