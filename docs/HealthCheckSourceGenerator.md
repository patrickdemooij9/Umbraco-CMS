# HealthCheck Source Generator

## Overview

`Umbraco.Cms.SourceGenerators` is a Roslyn incremental source generator that eliminates the runtime assembly-scan overhead of discovering built-in `HealthCheck` implementations.

### Problem

On every cold start, `UmbracoBuilderExtensions.AddAllCoreCollectionBuilders` calls:

```csharp
builder.HealthChecks().Add(() => builder.TypeLoader.GetTypes<HealthCheck>());
```

`TypeLoader.GetTypes<HealthCheck>()` uses `TypeFinder.FindClassesOfType<HealthCheck>()`, which iterates every loaded assembly, calls `Assembly.GetTypes()` on each, and filters by assignability. For a full Umbraco application with hundreds of referenced assemblies this is measurable startup overhead.

### Solution

The source generator (`HealthCheckSourceGenerator`) runs at **compile time** inside every project that references it as an analyzer. For each non-abstract class that inherits (directly or transitively) from `HealthCheck`, it emits one assembly-level attribute into the target project's output DLL:

```csharp
// HealthCheckRegistrations.g.cs  (auto-generated, do not edit)
[assembly: global::Umbraco.Cms.Core.HealthChecks.GeneratedHealthChecksAttribute(
    typeof(global::Umbraco.Cms.Core.HealthChecks.Checks.Services.SmtpCheck))]
// … one line per discovered HealthCheck type …
```

At startup, `DiscoverHealthChecks()` reads these attributes from all loaded assemblies:

```csharp
AppDomain.CurrentDomain
    .GetAssemblies()
    .SelectMany(a => a.GetCustomAttributes<GeneratedHealthChecksAttribute>())
    .Select(attr => attr.HealthCheckType)
```

Reading assembly-level attributes is **orders of magnitude faster** than enumerating every type in every assembly, because the CLR keeps custom attribute metadata indexed by type.

Third-party assemblies that do **not** use the source generator still work: `DiscoverHealthChecks()` falls back to `TypeLoader.GetTypes<HealthCheck>()` and deduplicates against the generated list.

---

## Project Structure

```
src/
  Umbraco.Cms.SourceGenerators/
    Umbraco.Cms.SourceGenerators.csproj   ← netstandard2.0, IsRoslynComponent=true
    HealthCheckSourceGenerator.cs          ← IIncrementalGenerator implementation

  Umbraco.Core/
    HealthChecks/
      GeneratedHealthChecksAttribute.cs    ← assembly-level attribute read at startup
    DependencyInjection/
      UmbracoBuilder.Collections.cs        ← DiscoverHealthChecks() replaces TypeLoader call
```

---

## Applying the Generator to a Project

Add the source generator as an analyzer reference (not a normal reference) so MSBuild runs it at compile time without adding a runtime dependency:

```xml
<ItemGroup>
  <ProjectReference
      Include="..\Umbraco.Cms.SourceGenerators\Umbraco.Cms.SourceGenerators.csproj"
      OutputItemType="Analyzer"
      ReferenceOutputAssembly="false" />
</ItemGroup>
```

The generator is already wired to `Umbraco.Core` and `Umbraco.Infrastructure` — the two Umbraco assemblies that ship built-in `HealthCheck` types.

### Third-party / user HealthChecks

If you implement a custom `HealthCheck` in your own project and want it to benefit from the fast attribute-lookup path:

1. Add `Umbraco.Cms.SourceGenerators` as an analyzer reference to your project (see above).
2. Rebuild. The generator emits `[assembly: GeneratedHealthChecksAttribute(typeof(YourCheck))]` into your DLL automatically.
3. At Umbraco startup, `DiscoverHealthChecks()` collects this attribute along with all others.

If you do **not** add the generator, your check is still discovered via the `TypeLoader` fallback — no change in functionality, just no startup-time saving for your assembly.

---

## Inspecting the Generated Code

To see the generated file on disk (useful for debugging):

```bash
dotnet build src/Umbraco.Core/Umbraco.Core.csproj -p:EmitCompilerGeneratedFiles=true
```

The file appears at:

```
src/Umbraco.Core/obj/Debug/net10.0/generated/
  Umbraco.Cms.SourceGenerators/
    Umbraco.Cms.SourceGenerators.HealthCheckSourceGenerator/
      HealthCheckRegistrations.g.cs
```

---

## Measuring the Performance Improvement

### Running the Benchmark

A `HealthCheckDiscoveryBenchmarks` class is provided in `Umbraco.Tests.Benchmarks`. It compares:

| Benchmark | Description |
|-----------|-------------|
| `TypeLoader reflection scan` (baseline) | `TypeLoader.GetTypes<HealthCheck>(cache: false)` — full assembly scan every call |
| `Source-generator attribute lookup` | Reads `GeneratedHealthChecksAttribute` from all loaded assemblies |

**Steps:**

```bash
cd tests/Umbraco.Tests.Benchmarks
dotnet run -c Release -- --filter "*HealthCheckDiscovery*"
```

BenchmarkDotNet prints a summary table with mean time, allocated bytes, and the speedup ratio vs the baseline.

**Tip:** Run the full benchmark suite to compare against the pre-existing `TypeLoaderBenchmarks` and `TypeFinderBenchmarks`:

```bash
dotnet run -c Release
```

### Interpreting Results

The key metrics to compare are:

- **Mean**: average time per iteration. The source-generator path should be significantly lower because `Assembly.GetCustomAttributes<T>()` is a direct metadata lookup, not a type scan.
- **Allocated**: the source-generator path allocates fewer objects because it never creates `Type[]` results from `GetTypes()`.
- **Ratio**: BenchmarkDotNet computes this automatically relative to the `[Baseline]` method.

### Manual Timing (without BenchmarkDotNet)

If you want a quick sanity-check without running the full benchmark harness, add a simple `Stopwatch` measurement in a test or a small console program:

```csharp
using System.Diagnostics;
using System.Reflection;
using Umbraco.Cms.Core.HealthChecks;

// --- Source-generator path ---
var sw = Stopwatch.StartNew();
var generated = AppDomain.CurrentDomain
    .GetAssemblies()
    .SelectMany(a => a.GetCustomAttributes<GeneratedHealthChecksAttribute>())
    .Select(attr => attr.HealthCheckType)
    .ToList();
sw.Stop();
Console.WriteLine($"Source-generator: {generated.Count} types in {sw.ElapsedMilliseconds} ms");

// --- TypeLoader path ---
// (set up TypeFinder / TypeLoader as in the benchmark class)
```

---

## Extending the Generator

The current generator only handles `HealthCheck`. To apply the same pattern to other types (e.g. `IHealthCheckNotificationMethod`, `IEditorValidator`, etc.):

1. Add a new `IIncrementalGenerator` class (or extend `HealthCheckSourceGenerator`) in `Umbraco.Cms.SourceGenerators`.
2. Change `HealthCheckFullName` / `AttributeFullName` to the target type and a new attribute.
3. Define the new attribute in `Umbraco.Core`.
4. Replace the matching `TypeLoader.GetTypes<T>()` call in `UmbracoBuilder.Collections.cs` with an attribute-lookup equivalent.

Each additional type moved to the source-generator path reduces the number of assemblies that must be fully scanned on startup.

---

## Security & Compatibility Notes

- **No runtime overhead**: the source generator executes only at build time. The generated `.cs` file is compiled into the target assembly; there is no generator DLL shipped to production.
- **Backward compatibility**: assemblies without the generator attribute are still handled by `TypeLoader`, so existing NuGet packages with custom `HealthCheck` types continue to work without modification.
- **Deterministic output**: the generator sorts discovered types alphabetically so the generated file is identical across incremental builds with the same source.
