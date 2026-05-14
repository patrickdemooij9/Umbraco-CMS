using System.Reflection;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DeliveryApi;
using Umbraco.Cms.Core.DynamicRoot.Origin;
using Umbraco.Cms.Core.DynamicRoot.QuerySteps;
using Umbraco.Cms.Core.Editors;
using Umbraco.Cms.Core.HealthChecks;
using Umbraco.Cms.Core.HealthChecks.NotificationMethods;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Media.EmbedProviders;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Cms.Core.Services.Filters;
using Umbraco.Cms.Core.Snippets;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Webhooks;
using Umbraco.Extensions;

namespace Umbraco.Cms.Core.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IUmbracoBuilder"/>
/// </summary>
public static partial class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds all core collection builders
    /// </summary>
    internal static void AddAllCoreCollectionBuilders(this IUmbracoBuilder builder)
    {
        builder.CacheRefreshers().Add(builder.TypeLoader.GetCacheRefreshers);
        builder.DataEditors().Add(builder.TypeLoader.GetDataEditors);
        builder.Actions().Add(builder.TypeLoader.GetActions);

        // all built-in finders in the correct order,
        // devs can then modify this list on application startup
        builder.ContentFinders()
            .Append<ContentFinderByPageIdQuery>()
            .Append<ContentFinderByUrl>()
            .Append<ContentFinderByKeyPath>()
            .Append<ContentFinderByUrlAlias>()
            .Append<ContentFinderByRedirectUrl>();
        builder.EditorValidators().Add(() => builder.TypeLoader.GetTypes<IEditorValidator>());
        builder.HealthChecks().Add(() => DiscoverHealthChecks(builder.TypeLoader));
        builder.HealthCheckNotificationMethods().Add(() => builder.TypeLoader.GetTypes<IHealthCheckNotificationMethod>());
        builder.UrlProviders()
            .Append<AliasUrlProvider>()
            .Append<DefaultUrlProvider>();
        builder.MediaUrlProviders()
            .Append<DefaultMediaUrlProvider>();

        builder.DynamicRootOriginFinders()
            .Append<ByKeyDynamicRootOriginFinder>()
            .Append<ParentDynamicRootOriginFinder>()
            .Append<CurrentDynamicRootOriginFinder>()
            .Append<SiteDynamicRootOriginFinder>()
            .Append<RootDynamicRootOriginFinder>()
            .Append<ContentRootDynamicRootOriginFinder>();

        builder.DynamicRootSteps()
            .Append<NearestAncestorOrSelfDynamicRootQueryStep>()
            .Append<FurthestAncestorOrSelfDynamicRootQueryStep>()
            .Append<NearestDescendantOrSelfDynamicRootQueryStep>()
            .Append<FurthestDescendantOrSelfDynamicRootQueryStep>();

        builder.Components();
        builder.PartialViewSnippets();
        builder.DataValueReferenceFactories();
        builder.PropertyValueConverters().Append(builder.TypeLoader.GetTypes<IPropertyValueConverter>());
        builder.UrlSegmentProviders().Append<DefaultUrlSegmentProvider>();
        builder.MediaUrlGenerators();
        // register OEmbed providers - no type scanning - all explicit opt-in of adding types, IEmbedProvider is not IDiscoverable
        builder.EmbedProviders()
            .Append<YouTube>()
            .Append<Vimeo>()
            .Append<DailyMotion>()
            .Append<Flickr>()
            .Append<Slideshare>()
            .Append<Kickstarter>()
            .Append<GettyImages>()
            .Append<Ted>()
            .Append<Soundcloud>()
            .Append<Issuu>()
            .Append<Hulu>()
            .Append<Giphy>()
            .Append<LottieFiles>()
            .Append<X>();
        builder.SelectorHandlers().Add(() => builder.TypeLoader.GetTypes<ISelectorHandler>());
        builder.FilterHandlers().Add(() => builder.TypeLoader.GetTypes<IFilterHandler>());
        builder.SortHandlers().Add(() => builder.TypeLoader.GetTypes<ISortHandler>());
        builder.ContentIndexHandlers().Add(() => builder.TypeLoader.GetTypes<IContentIndexHandler>());

        WebhookPayloadType webhookPayloadType = Constants.Webhooks.DefaultPayloadType;

        // IntelliSense indicates that GetSection cannot return null. However, in certain unit test setups,
        // the configuration may not be fully initialized, leading to GetSection returning null. This null
        // check ensures that the code behaves correctly in such scenarios and prevents potential null
        // reference exceptions during testing.
        if (builder.Config.GetSection(Constants.Configuration.ConfigWebhookPayloadType)?.Value is not null)
        {
            webhookPayloadType = builder.Config.GetValue<WebhookPayloadType>(Constants.Configuration.ConfigWebhookPayloadType);
        }

        builder.WebhookEvents().AddCms(true, webhookPayloadType);

        builder.ContentTypeFilters();
    }

    /// <summary>
    /// Gets the actions collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static ActionCollectionBuilder Actions(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ActionCollectionBuilder>();

    /// <summary>
    /// Gets the content finders collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static ContentFinderCollectionBuilder ContentFinders(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ContentFinderCollectionBuilder>();

    /// <summary>
    ///     Gets the event source authorizers collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="EventSourceAuthorizerCollectionBuilder" />.</returns>
    public static EventSourceAuthorizerCollectionBuilder EventSourceAuthorizers(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<EventSourceAuthorizerCollectionBuilder>();

    /// <summary>
    /// Gets the editor validators collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static EditorValidatorCollectionBuilder EditorValidators(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<EditorValidatorCollectionBuilder>();

    /// <summary>
    /// Gets the health checks collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static HealthCheckCollectionBuilder HealthChecks(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<HealthCheckCollectionBuilder>();

    /// <summary>
    ///     Gets the health check notification methods collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="HealthCheckNotificationMethodCollectionBuilder" />.</returns>
    public static HealthCheckNotificationMethodCollectionBuilder HealthCheckNotificationMethods(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<HealthCheckNotificationMethodCollectionBuilder>();

    /// <summary>
    /// Gets the URL providers collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static UrlProviderCollectionBuilder UrlProviders(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<UrlProviderCollectionBuilder>();

    /// <summary>
    /// Gets the media url providers collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static MediaUrlProviderCollectionBuilder MediaUrlProviders(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<MediaUrlProviderCollectionBuilder>();

    /// <summary>
    ///     Gets the dynamic root origin finders collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="DynamicRootOriginFinderCollectionBuilder" />.</returns>
    public static DynamicRootOriginFinderCollectionBuilder DynamicRootOriginFinders(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<DynamicRootOriginFinderCollectionBuilder>();

    /// <summary>
    ///     Gets the dynamic root query steps collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="DynamicRootQueryStepCollectionBuilder" />.</returns>
    public static DynamicRootQueryStepCollectionBuilder DynamicRootSteps(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<DynamicRootQueryStepCollectionBuilder>();

    /// <summary>
    /// Gets the backoffice sections/applications collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static WebhookEventCollectionBuilder WebhookEvents(this IUmbracoBuilder builder) => builder.WithCollectionBuilder<WebhookEventCollectionBuilder>();

    /// <summary>
    /// Gets the components collection builder.
    /// </summary>
    public static ComponentCollectionBuilder Components(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ComponentCollectionBuilder>();

    /// <summary>
    /// Gets the partial view snippets collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static PartialViewSnippetCollectionBuilder PartialViewSnippets(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<PartialViewSnippetCollectionBuilder>();

    /// <summary>
    /// Gets the cache refreshers collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static CacheRefresherCollectionBuilder CacheRefreshers(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<CacheRefresherCollectionBuilder>();

    /// <summary>
    /// Gets the map definitions collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static MapDefinitionCollectionBuilder MapDefinitions(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<MapDefinitionCollectionBuilder>();

    /// <summary>
    /// Gets the data editor collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static DataEditorCollectionBuilder DataEditors(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<DataEditorCollectionBuilder>();

    /// <summary>
    /// Gets the data value reference factory collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static DataValueReferenceFactoryCollectionBuilder DataValueReferenceFactories(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<DataValueReferenceFactoryCollectionBuilder>();

    /// <summary>
    /// Gets the property value converters collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static PropertyValueConverterCollectionBuilder PropertyValueConverters(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<PropertyValueConverterCollectionBuilder>();

    /// <summary>
    /// Gets the url segment providers collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static UrlSegmentProviderCollectionBuilder UrlSegmentProviders(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<UrlSegmentProviderCollectionBuilder>();

    /// <summary>
    /// Gets the content finders collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static MediaUrlGeneratorCollectionBuilder MediaUrlGenerators(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<MediaUrlGeneratorCollectionBuilder>();

    /// <summary>
    /// Gets the backoffice Embed Providers collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static EmbedProvidersCollectionBuilder EmbedProviders(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<EmbedProvidersCollectionBuilder>();

    /// <summary>
    ///     Gets the Delivery API selector handler collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="SelectorHandlerCollectionBuilder" />.</returns>
    public static SelectorHandlerCollectionBuilder SelectorHandlers(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<SelectorHandlerCollectionBuilder>();

    /// <summary>
    ///     Gets the Delivery API filter handler collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="FilterHandlerCollectionBuilder" />.</returns>
    public static FilterHandlerCollectionBuilder FilterHandlers(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<FilterHandlerCollectionBuilder>();

    /// <summary>
    ///     Gets the Delivery API sort handler collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="SortHandlerCollectionBuilder" />.</returns>
    public static SortHandlerCollectionBuilder SortHandlers(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<SortHandlerCollectionBuilder>();

    /// <summary>
    ///     Gets the Delivery API content index handler collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="ContentIndexHandlerCollectionBuilder" />.</returns>
    public static ContentIndexHandlerCollectionBuilder ContentIndexHandlers(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ContentIndexHandlerCollectionBuilder>();

    /// <summary>
    ///     Gets the content type filters collection builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="ContentTypeFilterCollectionBuilder" />.</returns>
    public static ContentTypeFilterCollectionBuilder ContentTypeFilters(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ContentTypeFilterCollectionBuilder>();

    /// <summary>
    ///     Discovers all <see cref="HealthCheck" /> types, preferring compile-time registrations
    ///     emitted by the source generator over a runtime reflection scan.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When the <c>Umbraco.Cms.SourceGenerators</c> analyzer is applied to an assembly it
    ///         emits one <see cref="GeneratedHealthChecksAttribute" /> per concrete
    ///         <see cref="HealthCheck" /> subclass.  Collecting those attributes at startup is
    ///         significantly faster than scanning every type in every loaded assembly.
    ///     </para>
    ///     <para>
    ///         When source-generated types are found, the runtime reflection scan is skipped
    ///         entirely for the fast path.  Third-party assemblies that ship custom
    ///         <see cref="HealthCheck" /> implementations without the source generator should
    ///         either add the <c>Umbraco.Cms.SourceGenerators</c> analyzer reference, or register
    ///         their checks explicitly via <c>builder.HealthChecks().Add&lt;T&gt;()</c>.
    ///     </para>
    ///     <para>
    ///         If no source-generated registrations are found at all (e.g. during unit tests
    ///         where only a subset of assemblies is loaded), the method falls back to the
    ///         original <see cref="TypeLoader" /> reflection scan to preserve full compatibility.
    ///     </para>
    /// </remarks>
    private static IEnumerable<Type> DiscoverHealthChecks(TypeLoader typeLoader)
    {
        // Fast path: collect types registered at compile time by the source generator.
        // Reading assembly-level attributes is orders of magnitude faster than iterating
        // all types in all assemblies.
        var generatedTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(GetGeneratedHealthCheckTypes)
            .ToList();

        if (generatedTypes.Count > 0)
        {
            // Skip the full reflection scan: all built-in HealthCheck types are registered
            // via the generated attributes.  Third-party assemblies that do not use the
            // source generator should register their checks via the builder API.
            return generatedTypes;
        }

        // No source-generated types found; fall back entirely to the reflection scan.
        // This path executes during unit tests where only partial assemblies are loaded.
        return typeLoader.GetTypes<HealthCheck>();
    }

    private static IEnumerable<Type> GetGeneratedHealthCheckTypes(Assembly assembly)
    {
        try
        {
            return assembly
                .GetCustomAttributes<GeneratedHealthChecksAttribute>()
                .Select(attr => attr.HealthCheckType);
        }
        catch
        {
            // Some assemblies may throw on attribute retrieval; skip them gracefully.
            return Enumerable.Empty<Type>();
        }
    }
}
