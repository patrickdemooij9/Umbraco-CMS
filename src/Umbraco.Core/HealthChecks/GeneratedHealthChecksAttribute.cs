namespace Umbraco.Cms.Core.HealthChecks;

/// <summary>
///     Assembly-level attribute emitted by the Umbraco Health Check source generator.
///     Each instance registers one concrete <see cref="HealthCheck" /> type so the
///     startup code can collect all built-in health checks without a full assembly scan.
/// </summary>
/// <remarks>
///     This attribute is generated automatically; do not apply it by hand.
///     Apply the <c>Umbraco.Cms.SourceGenerators</c> analyzer to your project instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedHealthChecksAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GeneratedHealthChecksAttribute" /> class.
    /// </summary>
    /// <param name="healthCheckType">The concrete <see cref="HealthCheck" /> type being registered.</param>
    public GeneratedHealthChecksAttribute(Type healthCheckType)
        => HealthCheckType = healthCheckType;

    /// <summary>
    ///     Gets the concrete <see cref="HealthCheck" /> type registered by this attribute.
    /// </summary>
    public Type HealthCheckType { get; }
}
