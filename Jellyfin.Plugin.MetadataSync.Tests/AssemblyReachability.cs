using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Answers what an assembly can reach from a named set of entry types, rather
/// than what it names anywhere.
/// </summary>
/// <remarks>
/// <see cref="AssemblyMetadata"/> is the other half of this and the difference
/// is the whole reason to pay for a second reader. That one answers which types
/// and members an assembly refers to at all, which is a good guard while a
/// subject is absent and a blunt one once it is present: a plugin that legally
/// names a type in one corner is refused for it everywhere. Several conditions
/// on this board ask instead whether something is reachable from a particular
/// path, which is a question about the call graph and cannot be answered by a
/// list of names.
/// <para>
/// The walk decodes method bodies. It starts from every method on every entry
/// type, follows each call, construction and token load to a method defined in
/// the same assembly, and collects the names of everything outside the assembly
/// that any reached method refers to. Nested types travel with the type that
/// declares them, at the seed and again wherever the walk arrives, which is what
/// carries it into an asynchronous state machine, an iterator and a lambda: each
/// of those is a nested type the compiler wrote, and the calls a reader cares
/// about are in there rather than in the method as written. Doing that only at
/// the seed reads the whole of a type the walk started at and almost none of one
/// it arrived at through a call.
/// </para>
/// <para>
/// Dispatch is followed one step further than the instruction says. A call to an
/// interface member compiles to a reference to the interface member, so a walk
/// stopping there would leave every implementation unread, and the one place
/// this plugin writes to a library is behind exactly such a call. So a reached
/// method on a type in this assembly also reaches the methods of the same name
/// on every type in the assembly that derives from it or implements it.
/// </para>
/// <para>
/// What it cannot do, stated rather than left to be found. A member reached by
/// reflection from a string spells no token at all and is invisible here, as it
/// is to every other reader in this suite. Dispatch is matched by name, so
/// overloads travel together, which over-reaches rather than under-reaches and
/// is the safe direction for a guard that refuses. A member reference whose
/// parent is a generic instantiation is counted as the type it instantiates
/// where that type is a plain reference, and skipped where it is not. And a
/// reached method in another assembly is not walked into: this answers what the
/// code in this assembly reaches, and a server method that itself touches an
/// image is the server's business rather than this plugin's.
/// </para>
/// </remarks>
internal static class AssemblyReachability
{
    /// <summary>
    /// The operand size of every opcode, read off the runtime rather than
    /// written down here, so the decoder cannot disagree with the instruction
    /// set about how long an instruction is.
    /// </summary>
    private static readonly Dictionary<short, OperandType> Operands = OperandTable();

    /// <summary>
    /// Walks an assembly from the types the caller names as entries.
    /// </summary>
    /// <param name="assembly">The assembly to walk.</param>
    /// <param name="isEntryType">Answers whether a type's full name is an entry. A nested type is offered under the name of the type it is declared in, so a compiler-generated body travels with its method.</param>
    /// <returns>What the walk reached.</returns>
    /// <exception cref="ArgumentNullException">There is nothing to walk, or nothing to decide the entries with.</exception>
    public static Reached From(Assembly assembly, Func<string, bool> isEntryType)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(isEntryType);

        var location = assembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            throw new InvalidOperationException("The assembly " + assembly.FullName + " has no file on disk to read.");
        }

        using var file = File.OpenRead(location);
        using var portableExecutable = new PEReader(file);

        return Walk(portableExecutable, portableExecutable.GetMetadataReader(), isEntryType);
    }

    private static Reached Walk(PEReader portableExecutable, MetadataReader metadata, Func<string, bool> isEntryType)
    {
        var entryTypes = new SortedSet<string>(StringComparer.Ordinal);
        var subtypes = Subtypes(metadata);
        var pending = new Queue<MethodDefinitionHandle>();
        var reached = new HashSet<MethodDefinitionHandle>();
        var members = new HashSet<string>(StringComparer.Ordinal);
        var types = new HashSet<string>(StringComparer.Ordinal);
        var walked = 0;

        foreach (var handle in metadata.TypeDefinitions)
        {
            var name = RootName(metadata, handle);
            if (!isEntryType(name))
            {
                continue;
            }

            entryTypes.Add(name);
            foreach (var method in metadata.GetTypeDefinition(handle).GetMethods())
            {
                Enqueue(method);
            }
        }

        var expanded = new HashSet<TypeDefinitionHandle>();

        while (pending.Count > 0)
        {
            var handle = pending.Dequeue();
            var definition = metadata.GetMethodDefinition(handle);

            var declaring = definition.GetDeclaringType();

            // A nested type travels with the type that declares it, here as
            // well as at the seed. The body of an asynchronous method is in a
            // state machine the compiler nested in the declaring type, and the
            // method as written only starts it, so a walk that entered nested
            // types only where it started would read the whole of a seeded type
            // and almost none of one it arrived at through a call.
            if (expanded.Add(declaring))
            {
                foreach (var nested in Nested(metadata, declaring))
                {
                    foreach (var method in metadata.GetTypeDefinition(nested).GetMethods())
                    {
                        Enqueue(method);
                    }
                }
            }

            // A method of the same name on a type that derives from or
            // implements the one declaring this method is reachable through it,
            // because the call site names the declaration and the runtime picks
            // the override.
            if (subtypes.TryGetValue(declaring, out var below))
            {
                var wanted = metadata.GetString(definition.Name);
                foreach (var subtype in below)
                {
                    foreach (var candidate in metadata.GetTypeDefinition(subtype).GetMethods())
                    {
                        if (string.Equals(metadata.GetString(metadata.GetMethodDefinition(candidate).Name), wanted, StringComparison.Ordinal))
                        {
                            Enqueue(candidate);
                        }
                    }
                }
            }

            if (definition.RelativeVirtualAddress == 0)
            {
                continue;
            }

            walked++;
            foreach (var token in Tokens(portableExecutable.GetMethodBody(definition.RelativeVirtualAddress).GetILContent()))
            {
                Resolve(metadata, token, Enqueue, members, types);
            }
        }

        return new Reached(members, types, entryTypes, walked);

        void Enqueue(MethodDefinitionHandle handle)
        {
            if (reached.Add(handle))
            {
                pending.Enqueue(handle);
            }
        }
    }

    /// <summary>
    /// Turns one metadata token into either a method in this assembly to walk
    /// into, or the name of something outside it.
    /// </summary>
    private static void Resolve(
        MetadataReader metadata,
        int token,
        Action<MethodDefinitionHandle> enqueue,
        HashSet<string> members,
        HashSet<string> types)
    {
        var handle = MetadataTokens.EntityHandle(token);

        switch (handle.Kind)
        {
            case HandleKind.MethodDefinition:
                enqueue((MethodDefinitionHandle)handle);
                break;

            case HandleKind.MethodSpecification:
                // A generic method call names the instantiation; what it stands
                // for is the method itself.
                Resolve(metadata, MetadataTokens.GetToken(metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method), enqueue, members, types);
                break;

            case HandleKind.MemberReference:
                var reference = metadata.GetMemberReference((MemberReferenceHandle)handle);
                var parent = DeclaringName(metadata, reference.Parent);
                if (parent is not null)
                {
                    types.Add(parent);
                    members.Add(parent + "." + metadata.GetString(reference.Name));
                }

                break;

            case HandleKind.TypeReference:
                types.Add(TypeReferenceName(metadata, (TypeReferenceHandle)handle));
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// The full name of whatever declares a member reference, where that is a
    /// plain type outside this assembly. A generic instantiation is unwrapped to
    /// the type it instantiates; anything else answers null and is skipped
    /// rather than guessed at.
    /// </summary>
    private static string? DeclaringName(MetadataReader metadata, EntityHandle parent)
    {
        if (parent.Kind == HandleKind.TypeReference)
        {
            return TypeReferenceName(metadata, (TypeReferenceHandle)parent);
        }

        if (parent.Kind != HandleKind.TypeSpecification)
        {
            return null;
        }

        var signature = metadata.GetBlobReader(metadata.GetTypeSpecification((TypeSpecificationHandle)parent).Signature);
        if (signature.RemainingBytes == 0 || signature.ReadByte() != (byte)SignatureTypeCode.GenericTypeInstance)
        {
            return null;
        }

        // The element type byte, then the coded handle of the type being
        // instantiated. Anything the reader cannot make a type reference of is
        // not one this suite watches.
        if (signature.RemainingBytes == 0)
        {
            return null;
        }

        signature.ReadByte();
        var instantiated = signature.ReadTypeHandle();

        return instantiated.Kind == HandleKind.TypeReference
            ? TypeReferenceName(metadata, (TypeReferenceHandle)instantiated)
            : null;
    }

    private static string TypeReferenceName(MetadataReader metadata, TypeReferenceHandle handle)
    {
        var reference = metadata.GetTypeReference(handle);
        var name = metadata.GetString(reference.Name);
        var space = metadata.GetString(reference.Namespace);

        // A type nested in another names its declaring type as its scope rather
        // than a namespace, so the enclosing name is asked for instead.
        if (string.IsNullOrEmpty(space) && reference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return TypeReferenceName(metadata, (TypeReferenceHandle)reference.ResolutionScope) + "." + name;
        }

        return string.IsNullOrEmpty(space) ? name : space + "." + name;
    }

    /// <summary>
    /// The full name of the outermost type a definition sits in, so that a
    /// compiler-generated body is offered to the caller under the name of the
    /// type whose source it was written in.
    /// </summary>
    private static string RootName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var definition = metadata.GetTypeDefinition(handle);
        while (definition.IsNested)
        {
            definition = metadata.GetTypeDefinition(definition.GetDeclaringType());
        }

        var space = metadata.GetString(definition.Namespace);
        var name = metadata.GetString(definition.Name);

        return string.IsNullOrEmpty(space) ? name : space + "." + name;
    }

    /// <summary>
    /// Every type nested inside one, to any depth.
    /// </summary>
    /// <param name="metadata">The assembly's metadata.</param>
    /// <param name="handle">The type to look inside.</param>
    /// <returns>The nested types.</returns>
    private static IEnumerable<TypeDefinitionHandle> Nested(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        foreach (var inner in metadata.GetTypeDefinition(handle).GetNestedTypes())
        {
            yield return inner;

            foreach (var deeper in Nested(metadata, inner))
            {
                yield return deeper;
            }
        }
    }

    /// <summary>
    /// For each type in this assembly, the types in this assembly that derive
    /// from it or implement it, transitively.
    /// </summary>
    private static Dictionary<TypeDefinitionHandle, List<TypeDefinitionHandle>> Subtypes(MetadataReader metadata)
    {
        var above = new Dictionary<TypeDefinitionHandle, HashSet<TypeDefinitionHandle>>();

        foreach (var handle in metadata.TypeDefinitions)
        {
            var definition = metadata.GetTypeDefinition(handle);
            var direct = new HashSet<TypeDefinitionHandle>();

            if (definition.BaseType.Kind == HandleKind.TypeDefinition)
            {
                direct.Add((TypeDefinitionHandle)definition.BaseType);
            }

            foreach (var implementation in definition.GetInterfaceImplementations())
            {
                var declared = metadata.GetInterfaceImplementation(implementation).Interface;
                if (declared.Kind == HandleKind.TypeDefinition)
                {
                    direct.Add((TypeDefinitionHandle)declared);
                }
            }

            above[handle] = direct;
        }

        var below = new Dictionary<TypeDefinitionHandle, List<TypeDefinitionHandle>>();
        foreach (var (type, _) in above)
        {
            var seen = new HashSet<TypeDefinitionHandle>();
            var queue = new Queue<TypeDefinitionHandle>(above[type]);
            while (queue.Count > 0)
            {
                var super = queue.Dequeue();
                if (!seen.Add(super))
                {
                    continue;
                }

                if (!below.TryGetValue(super, out var list))
                {
                    list = new List<TypeDefinitionHandle>();
                    below[super] = list;
                }

                list.Add(type);

                if (above.TryGetValue(super, out var higher))
                {
                    foreach (var next in higher)
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        return below;
    }

    /// <summary>
    /// Every metadata token an instruction stream refers to.
    /// </summary>
    /// <remarks>
    /// A decode that meets an instruction the table does not know cannot skip it
    /// without knowing its length, and carrying on from the wrong offset would
    /// read argument bytes as opcodes and report tokens that are not there. So
    /// it throws. A guard that quietly stopped reading half way through a body
    /// would pass for a reason nobody could see.
    /// </remarks>
    private static IEnumerable<int> Tokens(ImmutableArray<byte> il)
    {
        var position = 0;

        while (position < il.Length)
        {
            var value = (short)il[position];
            position++;

            if (value == 0xFE)
            {
                value = (short)(0xFE00 | il[position]);
                position++;
            }

            if (!Operands.TryGetValue(value, out var operand))
            {
                throw new InvalidOperationException(
                    "The instruction 0x" + value.ToString("X4", CultureInfo.InvariantCulture) + " at offset " + position.ToString(CultureInfo.InvariantCulture) + " is not one this decoder knows the length of.");
            }

            switch (operand)
            {
                case OperandType.InlineNone:
                    break;

                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    position += 1;
                    break;

                case OperandType.InlineVar:
                    position += 2;
                    break;

                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    yield return BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(position, 4));
                    position += 4;
                    break;

                case OperandType.InlineBrTarget:
                case OperandType.InlineI:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.ShortInlineR:
                    position += 4;
                    break;

                case OperandType.InlineI8:
                case OperandType.InlineR:
                    position += 8;
                    break;

                case OperandType.InlineSwitch:
                    position += 4 + (4 * BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(position, 4)));
                    break;

                default:
                    throw new InvalidOperationException("The operand kind " + operand + " has no length in this decoder.");
            }
        }
    }

    private static Dictionary<short, OperandType> OperandTable()
    {
        var table = new Dictionary<short, OperandType>();

        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode code)
            {
                table[code.Value] = code.OperandType;
            }
        }

        return table;
    }

    /// <summary>
    /// What one walk found.
    /// </summary>
    internal sealed class Reached
    {
        internal Reached(HashSet<string> members, HashSet<string> types, SortedSet<string> entryTypes, int methodsRead)
        {
            Members = members;
            Types = types;
            EntryTypes = entryTypes;
            MethodsRead = methodsRead;
        }

        /// <summary>
        /// Gets the members outside the assembly that a reached method names,
        /// each as its declaring type's full name, a full stop and the member's
        /// name, which is the shape <see cref="AssemblyMetadata"/> uses.
        /// </summary>
        public IReadOnlyCollection<string> Members { get; }

        /// <summary>
        /// Gets the types outside the assembly that a reached method names.
        /// </summary>
        public IReadOnlyCollection<string> Types { get; }

        /// <summary>
        /// Gets the entry types the walk actually started from. A walk that
        /// started from none reaches nothing and passes every rule asked of it,
        /// so this is here to be asserted rather than assumed.
        /// </summary>
        public IReadOnlyCollection<string> EntryTypes { get; }

        /// <summary>
        /// Gets the number of method bodies decoded, for the same reason.
        /// </summary>
        public int MethodsRead { get; }

        /// <summary>
        /// The members of a watched set that this walk reached, sorted so a
        /// failure reads the same way twice.
        /// </summary>
        /// <param name="watched">The set to look for.</param>
        /// <returns>The watched members reached.</returns>
        public IReadOnlyList<string> MembersAmong(IEnumerable<string> watched)
        {
            ArgumentNullException.ThrowIfNull(watched);

            return watched.Where(Members.Contains).Order(StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// The types of a watched set that this walk reached, sorted for the
        /// same reason.
        /// </summary>
        /// <param name="watched">The set to look for.</param>
        /// <returns>The watched types reached.</returns>
        public IReadOnlyList<string> TypesAmong(IEnumerable<string> watched)
        {
            ArgumentNullException.ThrowIfNull(watched);

            return watched.Where(Types.Contains).Order(StringComparer.Ordinal).ToList();
        }
    }
}
