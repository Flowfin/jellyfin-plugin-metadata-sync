using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Reads what an assembly's metadata says it names.
/// </summary>
/// <remarks>
/// Two guards in this suite ask the same question about different vocabularies:
/// which types and which members of another assembly does this one name. The
/// reader is here once rather than in each of them, because two copies of a
/// metadata walk are two copies that drift, and the one that drifts is the one
/// that stops finding anything while still passing.
///
/// A metadata read rather than a source scan, and the difference is the whole
/// reason to pay for it. A source scan cannot see a reference that arrives
/// through a transitive helper, an extension method or a generic argument
/// written elsewhere. A reference is emitted into the metadata by whichever file
/// names the thing.
///
/// The bound is the same for every caller and is stated once here. A type or a
/// member reached only by reflection from a string spells no reference at all,
/// and neither does a value obtained from a call that returns
/// <see cref="object"/>. This answers what an assembly names, never what it can
/// reach.
/// </remarks>
internal static class AssemblyMetadata
{
    /// <summary>
    /// The full names of the types an assembly refers to in another assembly,
    /// together with the ones it declares itself. The first is what a use of a
    /// server type produces; the second is here so a type declared under a
    /// watched name inside this plugin is caught as well.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The type names, compared ordinally.</returns>
    public static HashSet<string> TypeNames(Assembly assembly)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        Read(assembly, metadata =>
        {
            foreach (var handle in metadata.TypeReferences)
            {
                var reference = metadata.GetTypeReference(handle);
                names.Add(FullName(metadata.GetString(reference.Namespace), metadata.GetString(reference.Name)));
            }

            foreach (var handle in metadata.TypeDefinitions)
            {
                var definition = metadata.GetTypeDefinition(handle);
                names.Add(FullName(metadata.GetString(definition.Namespace), metadata.GetString(definition.Name)));
            }
        });

        return names;
    }

    /// <summary>
    /// The members of other assemblies that an assembly names, each as its
    /// declaring type's full name, a full stop and the member's name.
    /// </summary>
    /// <remarks>
    /// Overloads collapse to one entry, because the signature is not part of
    /// the key. That is deliberate: a guard that named one overload would be
    /// walked around by calling another, and there is no case here where one
    /// overload of a watched member is allowed and another is not.
    ///
    /// A member whose parent is not a plain type reference, which is what a
    /// generic instantiation or an array produces, is skipped rather than
    /// guessed at. None of the members this suite watches is declared on one.
    /// </remarks>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The member names, compared ordinally.</returns>
    public static HashSet<string> MemberNames(Assembly assembly)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        Read(assembly, metadata =>
        {
            foreach (var handle in metadata.MemberReferences)
            {
                var reference = metadata.GetMemberReference(handle);
                if (reference.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                var parent = metadata.GetTypeReference((TypeReferenceHandle)reference.Parent);
                var declaring = FullName(metadata.GetString(parent.Namespace), metadata.GetString(parent.Name));

                names.Add(declaring + "." + metadata.GetString(reference.Name));
            }
        });

        return names;
    }

    private static void Read(Assembly assembly, Action<MetadataReader> read)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(read);

        var location = assembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            throw new InvalidOperationException("The assembly " + assembly.FullName + " has no file on disk to read.");
        }

        using var file = File.OpenRead(location);
        using var portableExecutable = new PEReader(file);
        read(portableExecutable.GetMetadataReader());
    }

    private static string FullName(string @namespace, string name)
    {
        return string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
    }
}
