using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.HealthChecks;

namespace Umbraco.Tests.Benchmarks;

/// <summary>
///     Compares two strategies for discovering all <see cref="HealthCheck" /> subclasses
///     at Umbraco startup:
///     <list type="bullet">
///         <item>
///             <term>Reflection scan (baseline)</term>
///             <description>
///                 The original approach: <see cref="TypeLoader.GetTypes{T}" /> iterates every
///                 type in every loaded assembly and checks assignability, which is the dominant
///                 cost of the Umbraco cold-start reflection phase.
///             </description>
///         </item>
///         <item>
///             <term>Source-generator attribute lookup</term>
///             <description>
///                 The new approach: the <c>Umbraco.Cms.SourceGenerators.HealthCheckSourceGenerator</c>
///                 emits <c>[assembly: GeneratedHealthChecksAttribute(typeof(...))]</c> at compile time.
///                 At startup we only read those assembly-level attributes — no type enumeration at all.
///             </description>
///         </item>
///     </list>
/// </summary>
[MediumRunJob]
[MemoryDiagnoser]
public class HealthCheckDiscoveryBenchmarks
{
    private TypeLoader _typeLoader = null!;

    /// <summary>
    ///     Prepares a <see cref="TypeLoader" /> that scans all assemblies referenced by
    ///     <c>Umbraco.Cms</c> — the same set that is scanned on a real Umbraco startup.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var typeFinder = new TypeFinder(
            new NullLogger<TypeFinder>(),
            new DefaultUmbracoAssemblyProvider(GetType().Assembly, NullLoggerFactory.Instance),
            null);

        _typeLoader = new TypeLoader(typeFinder, NullLogger<TypeLoader>.Instance);
    }

    /// <summary>
    ///     Baseline: discover <see cref="HealthCheck" /> types via a full assembly reflection scan.
    ///     This is the approach used before source generators were introduced.
    /// </summary>
    [Benchmark(Baseline = true, Description = "TypeLoader reflection scan")]
    public int ReflectionScan()
    {
        // Pass cache:false so every benchmark iteration pays the full scan cost.
        IEnumerable<Type> types = _typeLoader.GetTypes<HealthCheck>(cache: false);
        return types.Count();
    }

    /// <summary>
    ///     New approach: collect <see cref="HealthCheck" /> types from the
    ///     <see cref="GeneratedHealthChecksAttribute" /> assembly-level attributes emitted by the
    ///     Umbraco Health Check source generator.  No type scanning occurs at all.
    /// </summary>
    [Benchmark(Description = "Source-generator attribute lookup")]
    public int SourceGeneratorLookup()
    {
        int count = 0;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            IEnumerable<GeneratedHealthChecksAttribute> attributes;
            try
            {
                attributes = assembly.GetCustomAttributes<GeneratedHealthChecksAttribute>();
            }
            catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or BadImageFormatException)
            {
                // Skip assemblies that cannot expose their custom attributes
                // (e.g. native or partially-loaded assemblies).
                continue;
            }

            foreach (GeneratedHealthChecksAttribute attr in attributes)
            {
                _ = attr.HealthCheckType;
                count++;
            }
        }

        return count;
    }
}
