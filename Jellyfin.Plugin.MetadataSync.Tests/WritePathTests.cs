using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The walk. Everything the write path can reach is read out of the compiled
/// assembly, and a database or repository anywhere in it is a failure.
/// </summary>
/// <remarks>
/// A source scan cannot answer this. The names that matter arrive through a
/// property on a server type, through a lambda the compiler lifts into a class
/// nobody named, and through the state machine an asynchronous method is turned
/// into, and none of those is a line somebody wrote. So this reads the
/// instructions instead: it starts at the types that carry a plan to the
/// library, follows every call into the plugin's own types, and collects every
/// member and every type named along the way.
/// <para>
/// The bounds are the reason a green run here is worth something rather than
/// everything. It follows calls into this assembly and stops at the edge of it,
/// so what the server does after the supported call is outside and is meant to
/// be: the whole point of going through that call is that the server decides
/// what happens next. It refuses a name, so a repository reached through an
/// interface with an innocent name, or through reflection, spells nothing this
/// can see. And it reads what the compiler emitted, so a call that is never
/// made at run time still counts.
/// </para>
/// </remarks>
public class WritePathTests
{
    /// <summary>
    /// Namespaces and type names that mean the write went underneath the
    /// supported call. Reaching any of these leaves the server's caches and
    /// every connected client holding the old value.
    /// </summary>
    private static readonly string[] _forbiddenTypes =
    {
        "MediaBrowser.Controller.Persistence",
        "Microsoft.EntityFrameworkCore",
        "Jellyfin.Server.Implementations",
        "Microsoft.Data.Sqlite",
        "SQLitePCL",
        "System.Data.",
    };

    /// <summary>
    /// Members that write underneath the supported call while being declared on
    /// a type the walk otherwise allows. The item itself offers one, which is
    /// why a check over types alone would pass the mistake this exists for.
    /// </summary>
    private static readonly string[] _forbiddenMembers =
    {
        "UpdateToRepositoryAsync",
        "get_ItemRepository",
        "SaveItems",
        "SaveItem",
    };

    /// <summary>
    /// The walk starts somewhere. If the start set is ever empty every
    /// assertion below passes over nothing, so it is asserted rather than
    /// assumed, and the one type that carries a plan to a library is named.
    /// </summary>
    [Fact]
    public void TheWalkStartsAtTheTypesThatCarryAPlanToTheLibrary()
    {
        var starts = WritePathRoots().ToList();

        Assert.NotEmpty(starts);
        Assert.Contains(typeof(LibraryPlanTarget), starts);
        Assert.Contains(typeof(Applier), starts);
    }

    /// <summary>
    /// The condition. Nothing a write can reach is a database, a repository or
    /// a member that writes underneath the supported call.
    /// </summary>
    [Fact]
    public void NoDatabaseOrRepositoryIsReachableFromAWritePath()
    {
        var (types, members) = EverythingTheWritePathReaches();

        var forbidden = types
            .Where(name => _forbiddenTypes.Any(bad => name.Contains(bad, StringComparison.Ordinal)))
            .Concat(members.Where(name => _forbiddenMembers.Any(bad => string.Equals(name, bad, StringComparison.Ordinal))))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            forbidden.Count == 0,
            "A write path reaches: " + string.Join("; ", forbidden));
    }

    /// <summary>
    /// The walk sees what it claims to see. A reader who trusts the assertion
    /// above is trusting that the instructions were read at all, so the one call
    /// the write path is supposed to make is asserted to be in what the walk
    /// collected. Without this a walk that resolved nothing would look clean.
    /// </summary>
    [Fact]
    public void TheWalkSeesTheSupportedCallItself()
    {
        var (types, members) = EverythingTheWritePathReaches();

        Assert.Contains("UpdateItemAsync", members);
        Assert.Contains("GetItemById", members);
        Assert.Contains(types, name => name.Contains("ILibraryManager", StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the types a plan reaches the library through. Anything implementing
    /// the one interface between the two halves of a pass, and the half that
    /// calls it.
    /// </summary>
    private static IEnumerable<Type> WritePathRoots()
    {
        return typeof(LibraryPlanTarget).Assembly
            .GetTypes()
            .Where(type => typeof(IPlanTarget).IsAssignableFrom(type) || type == typeof(Applier))
            .Where(type => !type.IsInterface);
    }

    /// <summary>
    /// Follows every call from the write path into this assembly's own types
    /// and returns every type name and every member name reached.
    /// </summary>
    private static (IReadOnlyCollection<string> Types, IReadOnlyCollection<string> Members) EverythingTheWritePathReaches()
    {
        var plugin = typeof(LibraryPlanTarget).Assembly;
        var types = new HashSet<string>(StringComparer.Ordinal);
        var members = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>();

        foreach (var root in WritePathRoots())
        {
            queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type))
            {
                continue;
            }

            // Nested types are followed because most of what a write path
            // actually does lives in one. A lambda is lifted into a class the
            // compiler names, and an asynchronous method is turned into a state
            // machine, so the call to the server is not in the method that
            // reads as though it makes it.
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                queue.Enqueue(nested);
            }

            foreach (var method in MethodsOf(type))
            {
                foreach (var reached in Reached(method))
                {
                    Record(reached, types, members);

                    var declaring = DeclaringTypeOf(reached);
                    if (declaring is not null && declaring.Assembly == plugin)
                    {
                        queue.Enqueue(declaring);
                    }
                }
            }
        }

        return (types, members);
    }

    private static void Record(MemberInfo reached, HashSet<string> types, HashSet<string> members)
    {
        members.Add(reached.Name);

        var declaring = DeclaringTypeOf(reached);
        if (declaring is not null)
        {
            types.Add(declaring.FullName ?? declaring.Name);
        }

        // The type a member hands back matters as much as the type that
        // declares it. A repository reached as a property of an item is
        // declared on the item, and only its type says what it is.
        switch (reached)
        {
            case MethodInfo method:
                types.Add(method.ReturnType.FullName ?? method.ReturnType.Name);
                foreach (var parameter in method.GetParameters())
                {
                    types.Add(parameter.ParameterType.FullName ?? parameter.ParameterType.Name);
                }

                break;

            case FieldInfo field:
                types.Add(field.FieldType.FullName ?? field.FieldType.Name);
                break;

            case Type named:
                types.Add(named.FullName ?? named.Name);
                break;

            default:
                break;
        }
    }

    private static Type? DeclaringTypeOf(MemberInfo member)
    {
        return member as Type ?? member.DeclaringType;
    }

    private static IEnumerable<MethodBase> MethodsOf(Type type)
    {
        const BindingFlags Everything = BindingFlags.DeclaredOnly
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static;

        return type.GetMethods(Everything).Cast<MethodBase>().Concat(type.GetConstructors(Everything));
    }

    /// <summary>
    /// Reads one method's instructions and returns every member its operands
    /// name.
    /// </summary>
    private static IEnumerable<MemberInfo> Reached(MethodBase method)
    {
        byte[]? instructions;

        try
        {
            instructions = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (InvalidOperationException)
        {
            // An abstract or generated member with no body to read.
            yield break;
        }

        if (instructions is null)
        {
            yield break;
        }

        var typeArguments = method.DeclaringType?.IsGenericType == true
            ? method.DeclaringType.GetGenericArguments()
            : Array.Empty<Type>();

        var methodArguments = method.IsGenericMethodDefinition
            ? method.GetGenericArguments()
            : Array.Empty<Type>();

        var position = 0;
        while (position < instructions.Length)
        {
            var code = (short)instructions[position++];
            if (code == 0xFE)
            {
                code = unchecked((short)(0xFE00 | instructions[position++]));
            }

            if (!OpCodesByValue.TryGetValue(code, out var operation))
            {
                // An instruction this table does not know is the end of what
                // can be read safely: every later position would be a guess.
                yield break;
            }

            if (NamesAMember(operation.OperandType))
            {
                var token = BitConverter.ToInt32(instructions, position);
                MemberInfo? member = null;

                try
                {
                    member = method.Module.ResolveMember(token, typeArguments, methodArguments);
                }
                catch (ArgumentException)
                {
                    // A token this module cannot resolve, which is a generic
                    // instantiation the walk does not need to follow.
                }

                if (member is not null)
                {
                    yield return member;
                }
            }

            position += OperandLength(operation.OperandType, instructions, position);
        }
    }

    private static bool NamesAMember(OperandType operand)
    {
        return operand is OperandType.InlineMethod
            or OperandType.InlineField
            or OperandType.InlineTok
            or OperandType.InlineType;
    }

    private static int OperandLength(OperandType operand, byte[] instructions, int position)
    {
        return operand switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(instructions, position)),
            _ => 4,
        };
    }

    /// <summary>
    /// Gets the instruction set, keyed by the value the compiler writes, built
    /// from the runtime's own table rather than from a list here.
    /// </summary>
    private static Dictionary<short, OpCode> OpCodesByValue { get; } = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(operation => operation.Value);
}
