using System.Reflection;
using Tare.Core;
using Tare.Http;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// The no-IO promise, asserted instead of remembered. The core defines the citation
/// questions and what the answers mean; reaching a network is the adapters' job, and the
/// only thing keeping that true over time is a test that fails the moment a transport type
/// creeps across the line.
/// <para>
/// The promise is about doing IO, not about the word "Net" in an assembly name, so the two
/// halves are pinned separately. Inside, the core may use a networking <em>value</em> type
/// where the work is parsing - <see cref="CitationPolicy"/> reads a URL's host with one, and
/// re-implementing IPv6 literals by hand to avoid it would trade a real risk for a cosmetic
/// one. On its public surface it may use none at all, because a networking type in a
/// signature is how a transport concern gets into a caller.
/// </para>
/// </summary>
public class CorePurityTests
{
    private static readonly Assembly Core = typeof(Analyzer).Assembly;
    private static readonly Assembly Adapters = typeof(HttpClaimSource).Assembly;

    /// <summary>
    /// The one networking assembly the core may link. It holds addresses, address families
    /// and their parsers, and no way to open a connection. The list is an allowlist rather
    /// than a list of banned assemblies so the test fails closed: a new networking reference
    /// has to be argued for here before it can land.
    /// </summary>
    private static readonly string[] ParsingOnly = { "System.Net.Primitives" };

    [Fact]
    public void The_core_links_against_nothing_that_can_open_a_connection()
    {
        var transport = Core.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(IsNetworking)
            .Where(name => !ParsingOnly.Contains(name))
            .ToList();

        Assert.Empty(transport);
    }

    [Fact]
    public void The_core_does_not_link_against_the_adapters()
    {
        Assert.DoesNotContain(
            Adapters.GetName().Name,
            Core.GetReferencedAssemblies().Select(reference => reference.Name));
    }

    [Fact]
    public void The_adapters_link_against_the_core()
    {
        // The arrow points inward, which is the half that is easy to invert by accident: an
        // adapter may know the core's vocabulary, never the reverse.
        Assert.Contains(
            Core.GetName().Name,
            Adapters.GetReferencedAssemblies().Select(reference => reference.Name));
    }

    [Fact]
    public void No_transport_type_reaches_the_public_surface_of_the_core()
    {
        var leaked = Core.GetExportedTypes()
            .SelectMany(Surface)
            .SelectMany(Flatten)
            .Where(type => IsNetworking(type.Assembly.GetName().Name))
            .Select(type => type.FullName)
            .Distinct()
            .ToList();

        Assert.Empty(leaked);
    }

    // No allowlist here: a status number crosses the seam, the type that carried it does not.
    private static bool IsNetworking(string? assembly) =>
        assembly is not null
        && (assembly.StartsWith("System.Net", StringComparison.Ordinal) || assembly == "Tare.Http");

    private static IEnumerable<Type> Surface(Type type)
    {
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        yield return type;

        foreach (var contract in type.GetInterfaces())
        {
            yield return contract;
        }

        foreach (var member in type.GetMembers(Declared))
        {
            switch (member)
            {
                case PropertyInfo property:
                    yield return property.PropertyType;
                    break;
                case FieldInfo field:
                    yield return field.FieldType;
                    break;
                case MethodInfo method:
                    yield return method.ReturnType;
                    foreach (var parameter in method.GetParameters())
                    {
                        yield return parameter.ParameterType;
                    }

                    break;
                case ConstructorInfo constructor:
                    foreach (var parameter in constructor.GetParameters())
                    {
                        yield return parameter.ParameterType;
                    }

                    break;
            }
        }
    }

    // A leak can hide one level down - inside a Task<T>, a list, or an array - so unwrap.
    private static IEnumerable<Type> Flatten(Type type)
    {
        var element = type.HasElementType ? type.GetElementType()! : type;
        yield return element;

        foreach (var argument in element.GetGenericArguments())
        {
            foreach (var nested in Flatten(argument))
            {
                yield return nested;
            }
        }
    }
}
