using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using NetArchTest.Rules;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.Extensions;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.AI;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.ClassroomClient;
using OgmaLibrary.Infrastructure.LanHost;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Reader.Annotations;
using OgmaLibrary.Reader.Navigation;
using OgmaLibrary.Workers;
using Xunit;

namespace OgmaLibrary.Tests.Architecture;

/// <summary>
/// Enforces the bounded-context dependency rules from HLD §2.2 as executable tests.
/// A reference that violates the inward-pointing direction fails the build.
/// </summary>
public sealed class ArchitectureTests
{
    private const string Application = "OgmaLibrary.Application";
    private const string Infrastructure = "OgmaLibrary.Infrastructure";
    private const string Reader = "OgmaLibrary.Reader";
    private const string Bookshelf3D = "OgmaLibrary.Bookshelf3D";
    private const string Workers = "OgmaLibrary.Workers";
    private const string App = "OgmaLibrary.App";
    private const string Http = "System.Net.Http";
    private const string DependencyInjection = "Microsoft.Extensions.DependencyInjection";
    private const string EntityFrameworkCore = "Microsoft.EntityFrameworkCore";

    /// <summary>The Domain project must depend on no other project (strict isolation).</summary>
    [Fact]
    public void Architecture_DomainProject_HasNoOutwardDependencies()
    {
        var result = Types.InAssembly(typeof(Book).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Reader, Bookshelf3D, Workers, App)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>The Domain and Application layers must make no direct HTTP calls (egress chokepoint, SI-1).</summary>
    [Fact]
    public void Architecture_OnlyInfrastructureUsesHttpClient()
    {
        var domain = Types.InAssembly(typeof(Book).Assembly)
            .ShouldNot().HaveDependencyOn(Http).GetResult();
        var application = Types.InAssembly(typeof(IBenchmarkContext).Assembly)
            .ShouldNot().HaveDependencyOn(Http).GetResult();

        Assert.True(domain.IsSuccessful, Describe(domain));
        Assert.True(application.IsSuccessful, Describe(application));
    }

    /// <summary>Only the App composition root may reference the DI container (HLD §2.3).</summary>
    [Fact]
    public void Architecture_OnlyAppBindsImplementations()
    {
        var domain = Types.InAssembly(typeof(Book).Assembly)
            .ShouldNot().HaveDependencyOn(DependencyInjection).GetResult();
        var application = Types.InAssembly(typeof(IBenchmarkContext).Assembly)
            .ShouldNot().HaveDependencyOn(DependencyInjection).GetResult();

        Assert.True(domain.IsSuccessful, Describe(domain));
        Assert.True(application.IsSuccessful, Describe(application));
    }

    /// <summary>
    /// Only the Infrastructure assembly may reference Microsoft.EntityFrameworkCore.
    /// Domain, Application, Reader, and Bookshelf3D are projection consumers only.
    /// </summary>
    [Fact]
    public void Architecture_OnlyInfrastructureUsesEntityFrameworkCore()
    {
        var domain = Types.InAssembly(typeof(Book).Assembly)
            .ShouldNot().HaveDependencyOnAny(EntityFrameworkCore).GetResult();
        var application = Types.InAssembly(typeof(IBenchmarkContext).Assembly)
            .ShouldNot().HaveDependencyOnAny(EntityFrameworkCore).GetResult();
        var reader = Types.InAssembly(typeof(NavigationHistory).Assembly)
            .ShouldNot().HaveDependencyOnAny(EntityFrameworkCore).GetResult();

        // Verify that Infrastructure DOES contain types that use EF Core (sanity check).
        // CatalogueDbContext itself must depend on EF Core.
        var dbContextDependsOnEf = Types.InAssembly(typeof(CatalogueDbContext).Assembly)
            .That().HaveName("CatalogueDbContext")
            .Should().HaveDependencyOnAny(EntityFrameworkCore).GetResult();

        Assert.True(domain.IsSuccessful, Describe(domain));
        Assert.True(application.IsSuccessful, Describe(application));
        Assert.True(reader.IsSuccessful, Describe(reader));
        Assert.True(dbContextDependsOnEf.IsSuccessful, "CatalogueDbContext should depend on EF Core: " + Describe(dbContextDependsOnEf));
    }

    /// <summary>
    /// The Workers project must not reference the App project — workers are headless
    /// background services and must not depend on Avalonia UI (HLD §2.3, Phase 05).
    /// </summary>
    [Fact]
    public void Architecture_WorkersProject_HasNoDependencyOnAppProject()
    {
        var result = Types.InAssembly(typeof(BookIngestionWorker).Assembly)
            .ShouldNot()
            .HaveDependencyOn(App)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// Provider HTTP calls must live only in Infrastructure (the egress chokepoint, SI-1,
    /// Phase 07 FR-META-002). The Application and Domain layers must not reference
    /// System.Net.Http (enforced by the existing test, this test adds explicit naming).
    /// </summary>
    [Fact]
    public void Architecture_MetadataProviderHttpClients_OnlyInInfrastructure()
    {
        // Application must not directly use HttpClient.
        var appResult = Types.InAssembly(typeof(IBenchmarkContext).Assembly)
            .ShouldNot().HaveDependencyOn(Http).GetResult();

        // Domain must not directly use HttpClient.
        var domainResult = Types.InAssembly(typeof(Book).Assembly)
            .ShouldNot().HaveDependencyOn(Http).GetResult();

        // Providers live in Infrastructure — verify by asserting Infrastructure
        // has types in the Metadata.Providers namespace.
        var infraAssembly = typeof(CatalogueDbContext).Assembly;
        bool hasProviders = infraAssembly.GetTypes()
            .Any(t => t.Namespace?.Contains("Metadata.Providers") == true);

        Assert.True(appResult.IsSuccessful, "Application must not use HttpClient: " + Describe(appResult));
        Assert.True(domainResult.IsSuccessful, "Domain must not use HttpClient: " + Describe(domainResult));
        Assert.True(hasProviders, "Expected metadata provider types in Infrastructure.Metadata.Providers namespace");
    }

    /// <summary>
    /// Deterministic metadata enrichment must not depend on AI gateways, OpenAI SDKs,
    /// or future token-consuming model namespaces.
    /// </summary>
    [Fact]
    public void Architecture_MetadataEnrichment_DoesNotDependOnAiOrOpenAi()
    {
        string[] forbidden =
        [
            "OgmaLibrary.AI",
            "OgmaLibrary.Application.AI",
            "OgmaLibrary.Infrastructure.AI",
            "OpenAI",
        ];

        var applicationMetadata = Types.InAssembly(typeof(IMetadataProvider).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Application.Metadata")
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();
        var infrastructureMetadata = Types.InAssembly(typeof(BookMetadataEnrichmentService).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Infrastructure.Metadata")
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();
        var appBookDetail = Types.InAssembly(typeof(BookDetailViewModel).Assembly)
            .That()
            .HaveName("BookDetailViewModel")
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();
        var worker = Types.InAssembly(typeof(BookIngestionWorker).Assembly)
            .That()
            .HaveName("BookIngestionWorker")
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(applicationMetadata.IsSuccessful, Describe(applicationMetadata));
        Assert.True(infrastructureMetadata.IsSuccessful, Describe(infrastructureMetadata));
        Assert.True(appBookDetail.IsSuccessful, Describe(appBookDetail));
        Assert.True(worker.IsSuccessful, Describe(worker));
    }

    /// <summary>
    /// The Reader project must not depend directly on Infrastructure
    /// (it may only depend on Domain + Application per HLD §2.2).
    /// </summary>
    [Fact]
    public void Architecture_Reader_DoesNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(NavigationHistory).Assembly)
            .ShouldNot()
            .HaveDependencyOn(Infrastructure)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// The Reader project must not depend on the Search bounded context.
    /// </summary>
    [Fact]
    public void Architecture_Reader_DoesNotDependOnSearch()
    {
        var result = Types.InAssembly(typeof(NavigationHistory).Assembly)
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Search")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// The Reader project must not depend on the AI bounded context.
    /// </summary>
    [Fact]
    public void Architecture_Reader_DoesNotDependOnAI()
    {
        var result = Types.InAssembly(typeof(NavigationHistory).Assembly)
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.AI")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// The Reader Application contracts must not depend on Infrastructure.
    /// </summary>
    [Fact]
    public void Architecture_Reader_ApplicationContracts_DoNotDependOnInfrastructure()
    {
        // IPdfRenderer and friends live in Application.Reader namespace.
        var result = Types.InAssembly(typeof(IPdfRenderer).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Application.Reader")
            .ShouldNot()
            .HaveDependencyOn(Infrastructure)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// Phase 10 Search application contracts must stay independent from Reader,
    /// AI, Infrastructure, and EF Core so extraction/search can remain a
    /// LAN-projectable bounded context.
    /// </summary>
    [Fact]
    public void Architecture_Search_ApplicationContracts_StayBounded()
    {
        var searchContracts = Types.InAssembly(typeof(IExtractedTextStore).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Application.Search");

        var reader = searchContracts
            .ShouldNot()
            .HaveDependencyOn(Reader)
            .GetResult();
        var ai = searchContracts
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.AI")
            .GetResult();
        var infrastructure = searchContracts
            .ShouldNot()
            .HaveDependencyOn(Infrastructure)
            .GetResult();
        var efCore = searchContracts
            .ShouldNot()
            .HaveDependencyOn(EntityFrameworkCore)
            .GetResult();

        Assert.True(reader.IsSuccessful, Describe(reader));
        Assert.True(ai.IsSuccessful, Describe(ai));
        Assert.True(infrastructure.IsSuccessful, Describe(infrastructure));
        Assert.True(efCore.IsSuccessful, Describe(efCore));
    }

    /// <summary>
    /// Phase 11 semantic search may depend on the Application AI gateway
    /// contract, but it must not call the Infrastructure Ollama adapter directly.
    /// </summary>
    [Fact]
    public void Architecture_SemanticSearch_DoesNotDependOnInfrastructureAi()
    {
        var result = Types.InAssembly(typeof(IOllamaEmbeddingProvider).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Application.Search")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Infrastructure.AI")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// The local Ollama adapter must remain internal to Infrastructure.AI so
    /// feature code consumes only Application contracts.
    /// </summary>
    [Fact]
    public void Architecture_OllamaAdapter_IsInternalInfrastructureDetail()
    {
        Type? adapter = typeof(CatalogueDbContext).Assembly
            .GetType("OgmaLibrary.Infrastructure.AI.Ollama.OllamaEmbeddingAdapter");

        Assert.NotNull(adapter);
        Assert.False(adapter!.IsPublic);
    }

    /// <summary>
    /// HTTP clients must remain in infrastructure adapter namespaces, never in
    /// feature, UI, domain, or application code.
    /// </summary>
    [Fact]
    public void Architecture_AiProviderHttpClients_StayInAdapterNamespaces()
    {
        Type[] httpClientTypes = typeof(CatalogueDbContext).Assembly.GetTypes()
            .Where(type => HasHttpClientDependency(type) || HasHttpRequestDependency(type))
            .ToArray();

        Assert.All(httpClientTypes, type =>
        {
            string ns = type.Namespace ?? string.Empty;
            Assert.True(
                ns.StartsWith("OgmaLibrary.Infrastructure.AI.Providers", StringComparison.Ordinal) ||
                ns.StartsWith("OgmaLibrary.Infrastructure.AI.Ollama", StringComparison.Ordinal) ||
                ns.StartsWith("OgmaLibrary.Infrastructure.Metadata.Providers", StringComparison.Ordinal) ||
                ns.StartsWith("OgmaLibrary.Infrastructure.ClassroomClient", StringComparison.Ordinal),
                $"{type.FullName} must not own provider HTTP egress outside adapter namespaces.");
        });
    }

    /// <summary>The AI bounded context must not depend directly on Reader types.</summary>
    [Fact]
    public void Architecture_AiContext_DoesNotDependOnReader()
    {
        var application = Types.InAssembly(typeof(IAiGateway).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Application.Ai")
            .ShouldNot()
            .HaveDependencyOn(Reader)
            .GetResult();
        var infrastructure = Types.InAssembly(typeof(AiGateway).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Infrastructure.AI")
            .ShouldNot()
            .HaveDependencyOn(Reader)
            .GetResult();

        Assert.True(application.IsSuccessful, Describe(application));
        Assert.True(infrastructure.IsSuccessful, Describe(infrastructure));
    }

    /// <summary>Advisor application code must consume gateway/pipeline contracts only, never provider adapters.</summary>
    [Fact]
    public void Architecture_AdvisorService_UsesOnlyAiGateway()
    {
        var result = Types.InAssembly(typeof(AdvisorService).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Application.Ai")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Infrastructure.AI.Providers")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>Phase 13 extension markers must remain internal until Phase 23 publishes the SDK.</summary>
    [Fact]
    public void ExtensionPoints_AreInternal_In_Phase13()
    {
        Assembly applicationAssembly = typeof(AdvisorService).Assembly;
        string[] typeNames =
        [
            "OgmaLibrary.Application.Ai.Extensions.IRecommendationSource",
            "OgmaLibrary.Application.Ai.Extensions.IAiCatalogueReader",
        ];

        foreach (string typeName in typeNames)
        {
            Type? type = applicationAssembly.GetType(typeName, throwOnError: true);
            Assert.NotNull(type);

            Assert.True(type!.IsInterface);
            Assert.False(type.IsPublic);
            Assert.True(type.IsNotPublic);
            Assert.Contains(type.GetCustomAttributes(inherit: false), attribute => attribute is ExtensionPointAttribute);
        }

        string[] friendAssemblies = applicationAssembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();

        Assert.Contains("OgmaLibrary.Infrastructure", friendAssemblies);
        Assert.Contains("OgmaLibrary.Tests", friendAssemblies);
        Assert.Contains("OgmaLibrary.Tests.Architecture", friendAssemblies);
    }

    /// <summary>Phase 15 OCR provider remains an internal extension candidate until Phase 23.</summary>
    [Fact]
    public void OcrExtensionPoint_IsInternal_In_Phase15()
    {
        Assembly applicationAssembly = typeof(AdvisorService).Assembly;
        Type? type = applicationAssembly.GetType("OgmaLibrary.Application.Ocr.IOcrProvider", throwOnError: true);
        Assert.NotNull(type);

        Assert.True(type!.IsInterface);
        Assert.False(type.IsPublic);
        Assert.True(type.IsNotPublic);
        Assert.Contains(type.GetCustomAttributes(inherit: false), attribute => attribute is ExtensionPointAttribute);
    }

    /// <summary>The 3D shelf context must consume catalogue data through contracts only.</summary>
    [Fact]
    public void Bookshelf3D_HasNo_DirectDependency_On_CatalogueIdentity()
    {
        var result = Types.InAssembly(typeof(IWebViewBridge).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("OgmaLibrary.Infrastructure.Catalogue", "OgmaLibrary.Domain.Catalogue")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// Phase 09 annotation services are part of Reader and must remain independent
    /// from future Search infrastructure.
    /// </summary>
    [Fact]
    public void Architecture_Annotations_DoesNotDependOnSearch()
    {
        var result = Types.InAssembly(typeof(AnnotationService).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Reader.Annotations")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Search")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// Phase 09 annotation services must not depend on AI; the AI advisor reads
    /// annotations later through contracts.
    /// </summary>
    [Fact]
    public void Architecture_Annotations_DoesNotDependOnAI()
    {
        var result = Types.InAssembly(typeof(AnnotationService).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Reader.Annotations")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.AI")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>
    /// Phase 09 annotation services access catalogue state only through
    /// Application/Domain contracts, never directly through EF or Infrastructure.
    /// </summary>
    [Fact]
    public void Architecture_Annotations_AccessesCatalogueOnlyViaContracts()
    {
        var annotations = Types.InAssembly(typeof(AnnotationService).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Reader.Annotations");

        var infrastructure = annotations
            .ShouldNot()
            .HaveDependencyOn(Infrastructure)
            .GetResult();
        var efCore = annotations
            .ShouldNot()
            .HaveDependencyOn(EntityFrameworkCore)
            .GetResult();

        Assert.True(infrastructure.IsSuccessful, Describe(infrastructure));
        Assert.True(efCore.IsSuccessful, Describe(efCore));
    }

    /// <summary>
    /// Phase 09 annotations are DB-first and must never use the Phase 07 PDF
    /// metadata write-back service or PDF mutation libraries.
    /// </summary>
    [Fact]
    public void Architecture_Phase09Annotations_DoNotDependOnPdfWriteBack()
    {
        var annotations = Types.InAssembly(typeof(AnnotationService).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Reader.Annotations");

        var applicationMetadata = annotations
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Application.Metadata")
            .GetResult();
        var infrastructureMetadata = annotations
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Infrastructure.Metadata")
            .GetResult();
        var pdfSharp = annotations
            .ShouldNot()
            .HaveDependencyOn("PdfSharp")
            .GetResult();

        Assert.True(applicationMetadata.IsSuccessful, Describe(applicationMetadata));
        Assert.True(infrastructureMetadata.IsSuccessful, Describe(infrastructureMetadata));
        Assert.True(pdfSharp.IsSuccessful, Describe(pdfSharp));
    }

    [Fact]
    public void ArchTests_LanHost_HasNoCredentialStoreOrWorkerDependency()
    {
        var security = Types.InAssembly(typeof(LanHostServiceExtensions).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Infrastructure.LanHost")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Infrastructure.Security")
            .GetResult();
        var workers = Types.InAssembly(typeof(LanHostServiceExtensions).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Infrastructure.LanHost")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Workers")
            .GetResult();

        Assert.True(security.IsSuccessful, Describe(security));
        Assert.True(workers.IsSuccessful, Describe(workers));
    }

    [Fact]
    public void ArchTests_LanHost_HasNoAiProviderDependency()
    {
        var infrastructureAi = Types.InAssembly(typeof(LanHostServiceExtensions).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Infrastructure.LanHost")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Infrastructure.AI")
            .GetResult();
        var applicationAi = Types.InAssembly(typeof(LanHostServiceExtensions).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Infrastructure.LanHost")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Application.Ai")
            .GetResult();

        Assert.True(infrastructureAi.IsSuccessful, Describe(infrastructureAi));
        Assert.True(applicationAi.IsSuccessful, Describe(applicationAi));
    }

    [Fact]
    public void ArchTests_StandaloneMode_HasNoOpenListener()
    {
        Type[] listenerTypes = typeof(CatalogueDbContext).Assembly.GetTypes()
            .Where(type => !IsLanHostType(type))
            .Where(type => ReferencesTypeName(type, "HttpListener") ||
                ReferencesTypeName(type, "Kestrel"))
            .ToArray();

        Assert.Empty(listenerTypes);
    }

    [Fact]
    public void ArchTests_ClassroomClient_HasNoLanHostServerDependency()
    {
        var application = Types.InAssembly(typeof(IClassroomModeService).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Application.ClassroomClient")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Application.LanHost")
            .GetResult();
        var infrastructure = Types.InAssembly(typeof(ClassroomClientServiceExtensions).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Infrastructure.ClassroomClient")
            .ShouldNot()
            .HaveDependencyOnAny("OgmaLibrary.Application.LanHost", "OgmaLibrary.Infrastructure.LanHost")
            .GetResult();

        Assert.True(application.IsSuccessful, Describe(application));
        Assert.True(infrastructure.IsSuccessful, Describe(infrastructure));
    }

    [Fact]
    public void ArchTests_ClassroomClient_HasNoDirectCatalogueWriteDependency()
    {
        var result = Types.InAssembly(typeof(ClassroomClientServiceExtensions).Assembly)
            .That()
            .ResideInNamespace("OgmaLibrary.Infrastructure.ClassroomClient")
            .ShouldNot()
            .HaveDependencyOn("OgmaLibrary.Infrastructure.Catalogue")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public async Task ArchTests_StandaloneMode_HasClassroomClientInactiveByDefault()
    {
        using var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddClassroomClientServices(Path.Combine(Path.GetTempPath(), $"ogma-classroom-arch-{Guid.NewGuid():N}"))
            .BuildServiceProvider();

        var modeService = services.GetRequiredService<IClassroomModeService>();
        ClassroomModeSettings settings = await modeService.GetModeAsync();

        Assert.Equal(LibraryRuntimeMode.Standalone, settings.Mode);
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? "ok"
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);

    private static bool IsLanHostType(Type type) =>
        (type.Namespace ?? string.Empty).StartsWith("OgmaLibrary.Infrastructure.LanHost", StringComparison.Ordinal) ||
        (type.Namespace ?? string.Empty).StartsWith("OgmaLibrary.Application.LanHost", StringComparison.Ordinal);

    private static bool ReferencesTypeName(Type type, string name)
    {
        static IEnumerable<Type?> ReferencedTypes(Type inspected)
        {
            foreach (Type iface in inspected.GetInterfaces())
            {
                yield return iface;
            }

            yield return inspected.BaseType;

            foreach (FieldInfo field in inspected.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                yield return field.FieldType;
            }

            foreach (PropertyInfo property in inspected.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                yield return property.PropertyType;
            }

            foreach (MethodInfo method in inspected.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                yield return method.ReturnType;
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }
            }
        }

        return ReferencedTypes(type)
            .Any(candidate =>
                candidate?.FullName?.Contains(name, StringComparison.OrdinalIgnoreCase) == true ||
                candidate?.Name.Contains(name, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool HasHttpClientDependency(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Any(field => field.FieldType == typeof(HttpClient)) ||
        type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SelectMany(ctor => ctor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(HttpClient) || parameter.ParameterType == typeof(IHttpClientFactory));

    private static bool HasHttpRequestDependency(Type type) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Any(method =>
                method.ReturnType == typeof(HttpRequestMessage) ||
                method.GetParameters().Any(parameter => parameter.ParameterType == typeof(HttpRequestMessage)));
}
