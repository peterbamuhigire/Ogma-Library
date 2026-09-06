using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Domain.Ai;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.LanHost;
using OgmaLibrary.Infrastructure.SchoolAdmin;
using OgmaLibrary.Infrastructure.Sidecar;
using OgmaLibrary.Tests.Reader;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 HTTPS listener endpoint tests.</summary>
public sealed class LanHostEndpointTests
{
    [Fact]
    public async Task HostListener_HealthAuthAndCatalogueProjection_WorkOverHttps()
    {
        string dataDirectory = CreateTempDirectory();
        int port = GetFreeTcpPort();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await SeedBookAsync(services);
            string assetHash = new string('d', 64);
            byte[] assetBytes = [0xFF, 0xD8, 0xFF, 0xD9];
            string assetPath = new SidecarService(dataDirectory).Resolve(assetHash, SidecarClass.Covers, "_provider");
            await File.WriteAllBytesAsync(assetPath, assetBytes);
            string unpublishedAssetPath = new SidecarService(dataDirectory).Resolve(new string('e', 64), SidecarClass.Covers);
            await File.WriteAllBytesAsync(unpublishedAssetPath, assetBytes);
            byte[] pdfBytes = "%PDF-1.7\n% Ogma LAN fixture\n"u8.ToArray();
            await File.WriteAllBytesAsync(Path.Combine(dataDirectory, "lan-endpoint-book.pdf"), pdfBytes);
            await services.GetRequiredService<IHostModeSettingsRepository>()
                .SaveAsync(new HostModeSettings(true, port, HostContentDeliveryMode.PageRender, "Ogma Endpoint Test"));

            ILibraryHostService host = services.GetRequiredService<ILibraryHostService>();
            LibraryHostStatus started = await host.StartAsync();
            string enrollmentCode = started.EnrollmentCode ?? throw new InvalidOperationException("Host did not issue an enrollment code.");

            using HttpClient http = CreatePinnedTestClient(port);
            using HttpResponseMessage health = await http.GetAsync("/api/v1/health");
            using HttpResponseMessage unauthorized = await http.GetAsync("/api/v1/catalogue?pageSize=10");
            using HttpResponseMessage unauthorizedSync = await http.PutAsync(
                "/api/v1/profile/sync",
                new ByteArrayContent([1, 2, 3]));
            using HttpResponseMessage invalidSession = await http.PostAsJsonAsync(
                "/api/v1/auth/session",
                new
                {
                    clientId = "student-1",
                    role = "Student",
                    lifetimeMinutes = 5,
                    enrollmentCode = "WRONG123",
                });
            using HttpResponseMessage rejectedAdminSession = await http.PostAsJsonAsync(
                "/api/v1/auth/session",
                new
                {
                    clientId = "student-1",
                    role = "Admin",
                    lifetimeMinutes = 5,
                    enrollmentCode,
                });
            using HttpResponseMessage session = await http.PostAsJsonAsync(
                "/api/v1/auth/session",
                new { clientId = "student-1", role = "Student", lifetimeMinutes = 5, enrollmentCode });
            string token = await ReadJsonStringAsync(session, "token");
            EnrollmentToken managedEnrollment = await services.GetRequiredService<IProfileEnrollmentService>()
                .EnrollAsync(new EnrollProfileRequest("Managed Student", "Student", BirthYear: 2014));
            using HttpResponseMessage managedSession = await http.PostAsJsonAsync(
                "/api/v1/auth/session",
                new
                {
                    profileId = managedEnrollment.ProfileId,
                    enrollmentToken = managedEnrollment.Token,
                    lifetimeMinutes = 5,
                });
            string managedToken = await ReadJsonStringAsync(managedSession, "token");
            ClientSessionSnapshot? managedSessionSnapshot = await services
                .GetRequiredService<IClientSessionService>()
                .GetActiveAsync(managedToken);
            using HttpResponseMessage replayManagedSession = await http.PostAsJsonAsync(
                "/api/v1/auth/session",
                new
                {
                    profileId = managedEnrollment.ProfileId,
                    enrollmentToken = managedEnrollment.Token,
                    lifetimeMinutes = 5,
                });
            HttpResponseMessage? rateLimitedSession = null;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                HttpResponseMessage candidate = await http.PostAsJsonAsync(
                    "/api/v1/auth/session",
                    new
                    {
                        clientId = "rate-limit-client",
                        role = "Student",
                        lifetimeMinutes = 5,
                        enrollmentCode = "WRONG123",
                    });
                if (attempt == 5)
                {
                    rateLimitedSession = candidate;
                }
                else
                {
                    candidate.Dispose();
                }
            }
            using HttpClient managedHttp = CreatePinnedTestClient(port);
            managedHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managedToken);
            var aiRequest = new
            {
                profileId = managedEnrollment.ProfileId,
                query = "LAN Endpoint",
                libraryId = "default",
                requestedTier = AiPrivacyTier.MetadataOnly,
                confirmedPayloadPreview = true,
            };
            using HttpResponseMessage aiPreview = await managedHttp.PostAsJsonAsync(
                "/api/v1/ai/search/preview",
                aiRequest);
            string aiPreviewJson = await aiPreview.Content.ReadAsStringAsync();
            using HttpResponseMessage unconfirmedAiSearch = await managedHttp.PostAsJsonAsync(
                "/api/v1/ai/search",
                aiRequest with { confirmedPayloadPreview = false });
            using HttpResponseMessage aiSearch = await managedHttp.PostAsJsonAsync(
                "/api/v1/ai/search",
                aiRequest);
            string aiSearchJson = await aiSearch.Content.ReadAsStringAsync();
            await services.GetRequiredService<IProfileEnrollmentService>()
                .RevokeAsync(managedEnrollment.ProfileId);
            using HttpResponseMessage revokedManagedRequest = await managedHttp.GetAsync(
                "/api/v1/catalogue?pageSize=1");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage studentAdminRoute = await http.PostAsync("/admin/ai/test-connection?providerId=openai", null);
            using HttpResponseMessage manualProfileAiSearch = await http.PostAsJsonAsync(
                "/api/v1/ai/search",
                aiRequest);
            using HttpResponseMessage catalogue = await http.GetAsync("/api/v1/catalogue?pageSize=10");
            string catalogueJson = await catalogue.Content.ReadAsStringAsync();
            using HttpResponseMessage pagedCatalogue = await http.GetAsync("/api/v1/catalogue?page=1&pageSize=1");
            string pagedCatalogueJson = await pagedCatalogue.Content.ReadAsStringAsync();
            using HttpResponseMessage search = await http.GetAsync("/api/v1/catalogue/search?q=Second&pageSize=1");
            string searchJson = await search.Content.ReadAsStringAsync();
            using HttpResponseMessage bookDetail = await http.GetAsync("/api/v1/catalogue/01LANENDPOINT000000000001");
            string bookDetailJson = await bookDetail.Content.ReadAsStringAsync();
            using HttpResponseMessage unpublishedDetail = await http.GetAsync("/api/v1/catalogue/01LANENDPOINT000000000003");
            byte[] syncBlob = [0x4F, 0x47, 0x4D, 0x41, 0x01, 0x02, 0x03, 0x04];
            using var syncContent = new ByteArrayContent(syncBlob);
            syncContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.ogma.classroom-sync+binary");
            using HttpResponseMessage uploadSync = await http.PutAsync("/api/v1/profile/sync", syncContent);
            using HttpResponseMessage downloadSync = await http.GetAsync("/api/v1/profile/sync");
            byte[] downloadedSyncBlob = await downloadSync.Content.ReadAsByteArrayAsync();
            using HttpResponseMessage asset = await http.GetAsync($"/api/v1/assets/covers/{assetHash}");
            byte[] servedAsset = await asset.Content.ReadAsByteArrayAsync();
            using HttpResponseMessage unsupportedVariant = await http.GetAsync(
                $"/api/v1/assets/covers/{assetHash}?variant=_unpublished");
            using HttpResponseMessage unpublishedAsset = await http.GetAsync($"/api/v1/assets/covers/{new string('e', 64)}");
            using HttpResponseMessage page = await http.GetAsync("/api/v1/books/01LANENDPOINT000000000001/page/1?widthPx=800");
            byte[] renderedPage = await page.Content.ReadAsByteArrayAsync();
            using HttpResponseMessage invalidAsset = await http.GetAsync($"/api/v1/assets/covers/{new string('z', 64)}");
            using HttpResponseMessage fileStream = await http.GetAsync("/api/v1/books/01LANENDPOINT000000000001/file");
            string fileStreamBody = await fileStream.Content.ReadAsStringAsync();
            ClientSessionResult adminSession = await services.GetRequiredService<IClientSessionService>()
                .IssueAsync(new ClientSessionRequest("host-local-admin", "admin", TimeSpan.FromMinutes(5)));
            using HttpClient adminHttp = CreatePinnedTestClient(port);
            adminHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminSession.Token);
            using HttpResponseMessage adminTestConnection = await adminHttp.PostAsync(
                "/admin/ai/test-connection?providerId=openai",
                null);
            string adminTestConnectionJson = await adminTestConnection.Content.ReadAsStringAsync();

            await host.StopAsync();
            await using CatalogueDbContext verify = services.GetRequiredService<CatalogueDbContext>();
            List<AuditEventRow> auditEvents = await verify.AuditEvents
                .Where(x => x.EventType == "LanHostRequestServed")
                .ToListAsync();

            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Equal("DENY", health.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("no-store", health.Headers.GetValues("Cache-Control").Single());
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedSync.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, invalidSession.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, rejectedAdminSession.StatusCode);
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
            Assert.Equal(HttpStatusCode.OK, managedSession.StatusCode);
            Assert.Equal("student", managedSessionSnapshot?.Role, ignoreCase: true);
            Assert.Equal(HttpStatusCode.Unauthorized, replayManagedSession.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedSession!.StatusCode);
            Assert.True(rateLimitedSession.Headers.Contains("Retry-After"));
            Assert.Equal(HttpStatusCode.OK, aiPreview.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, unconfirmedAiSearch.StatusCode);
            Assert.Equal(HttpStatusCode.OK, aiSearch.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, revokedManagedRequest.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, studentAdminRoute.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, manualProfileAiSearch.StatusCode);
            Assert.Equal(HttpStatusCode.OK, catalogue.StatusCode);
            Assert.Equal(HttpStatusCode.OK, pagedCatalogue.StatusCode);
            Assert.Equal(HttpStatusCode.OK, search.StatusCode);
            Assert.Equal(HttpStatusCode.OK, bookDetail.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, unpublishedDetail.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, uploadSync.StatusCode);
            Assert.Equal(HttpStatusCode.OK, downloadSync.StatusCode);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, unsupportedVariant.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, unpublishedAsset.StatusCode);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Equal(assetBytes, servedAsset);
            Assert.Equal(syncBlob, downloadedSyncBlob);
            Assert.Equal(
                "application/vnd.ogma.classroom-sync+binary",
                downloadSync.Content.Headers.ContentType?.MediaType);
            Assert.Equal("image/png", page.Content.Headers.ContentType?.MediaType);
            Assert.True(renderedPage.Length >= 8);
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], renderedPage[..4]);
            Assert.Equal(HttpStatusCode.BadRequest, invalidAsset.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, fileStream.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, adminTestConnection.StatusCode);
            Assert.DoesNotContain("%PDF", fileStreamBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("key_not_configured", adminTestConnectionJson, StringComparison.Ordinal);
            Assert.DoesNotContain(adminSession.Token, adminTestConnectionJson, StringComparison.Ordinal);
            Assert.Contains("LAN Endpoint Book", catalogueJson, StringComparison.Ordinal);
            Assert.DoesNotContain("Unpublished LAN Endpoint Book", catalogueJson, StringComparison.Ordinal);
            Assert.DoesNotContain("Inactive LAN Endpoint Book", catalogueJson, StringComparison.Ordinal);
            Assert.Contains("LAN Endpoint Book", aiPreviewJson, StringComparison.Ordinal);
            Assert.Contains("\"wasProviderCalled\":true", aiSearchJson, StringComparison.Ordinal);
            Assert.Contains("01LANENDPOINT000000000001", aiSearchJson, StringComparison.Ordinal);
            Assert.DoesNotContain("01FAKEBOOK000000000001", aiSearchJson, StringComparison.Ordinal);
            Assert.Contains("01LANENDPOINT000000000001", catalogueJson, StringComparison.Ordinal);
            Assert.Contains($"/api/v1/assets/cover/{assetHash}", catalogueJson, StringComparison.Ordinal);
            Assert.Contains($"/api/v1/assets/spine/{assetHash}", catalogueJson, StringComparison.Ordinal);
            Assert.DoesNotContain("CoverRelativePath", catalogueJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RelativePath", catalogueJson, StringComparison.OrdinalIgnoreCase);
            using (JsonDocument pagedDocument = JsonDocument.Parse(pagedCatalogueJson))
            {
                JsonElement root = pagedDocument.RootElement;
                Assert.Equal(1, GetJsonProperty(root, "page").GetInt32());
                Assert.Equal(1, GetJsonProperty(root, "pageSize").GetInt32());
                Assert.Equal(1, GetJsonProperty(root, "returnedCount").GetInt32());
                Assert.True(GetJsonProperty(root, "hasMore").GetBoolean());
            }

            Assert.Contains("LAN Endpoint Book", bookDetailJson, StringComparison.Ordinal);
            Assert.Contains("01LANENDPOINT000000000001", bookDetailJson, StringComparison.Ordinal);
            Assert.Contains($"/api/v1/assets/cover/{assetHash}", bookDetailJson, StringComparison.Ordinal);
            Assert.DoesNotContain("CoverRelativePath", bookDetailJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RelativePath", bookDetailJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lan-endpoint-book.pdf", bookDetailJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Second LAN Endpoint Book", searchJson, StringComparison.Ordinal);
            Assert.Contains("01LANENDPOINT000000000002", searchJson, StringComparison.Ordinal);
            Assert.True(auditEvents.Count >= 4);
            Assert.Contains(auditEvents, e => e.EntityId == "/api/v1/catalogue" && e.AfterJson?.Contains("\"statusCode\":401", StringComparison.Ordinal) == true);
            Assert.Contains(
                auditEvents,
                e => e.EntityId == "/api/v1/auth/session" &&
                     e.ActorId == "client:student-1" &&
                     e.AfterJson?.Contains("\"action\":\"IssueSession\"", StringComparison.Ordinal) == true &&
                     e.AfterJson.Contains("\"clientId\":\"student-1\"", StringComparison.Ordinal) &&
                     e.AfterJson.Contains("\"role\":\"Student\"", StringComparison.Ordinal));
            Assert.Contains(
                auditEvents,
                e => e.EntityId == "/api/v1/auth/session" &&
                     e.AfterJson?.Contains("\"statusCode\":403", StringComparison.Ordinal) == true &&
                     e.AfterJson.Contains("\"action\":\"IssueSession\"", StringComparison.Ordinal));
            Assert.Contains(
                auditEvents,
                e => e.EntityId == "/admin/ai/test-connection" &&
                     e.ActorId == "client:student-1" &&
                     e.AfterJson?.Contains("\"statusCode\":403", StringComparison.Ordinal) == true &&
                     e.AfterJson.Contains("\"action\":\"TestSchoolAiConnection\"", StringComparison.Ordinal) &&
                     e.AfterJson.Contains("\"role\":\"Student\"", StringComparison.Ordinal));
            Assert.Contains(
                auditEvents,
                e => e.EntityId == "/admin/ai/test-connection" &&
                     e.ActorId == "client:host-local-admin" &&
                     e.AfterJson?.Contains("\"statusCode\":503", StringComparison.Ordinal) == true &&
                     e.AfterJson.Contains("\"action\":\"TestSchoolAiConnection\"", StringComparison.Ordinal) &&
                     e.AfterJson.Contains("\"role\":\"admin\"", StringComparison.Ordinal));
            Assert.Contains(
                auditEvents,
                e => e.EntityId == "/api/v1/catalogue" &&
                     e.ActorId == "client:student-1" &&
                     e.AfterJson?.Contains("\"action\":\"ListCatalogue\"", StringComparison.Ordinal) == true &&
                     e.AfterJson.Contains("\"resourceType\":\"Catalogue\"", StringComparison.Ordinal) &&
                     e.AfterJson.Contains("\"sessionFingerprint\":", StringComparison.Ordinal));
            Assert.Contains(
                auditEvents,
                e => e.EntityId == "/api/v1/ai/search" &&
                     e.ActorId == $"client:{managedEnrollment.ProfileId:D}" &&
                     e.AfterJson?.Contains("\"action\":\"SearchSchoolAi\"", StringComparison.Ordinal) == true &&
                     e.AfterJson.Contains("\"statusCode\":200", StringComparison.Ordinal));
            Assert.Contains(
                auditEvents,
                e => e.EntityId == "/api/v1/profile/sync" &&
                     e.ActorId == "client:student-1" &&
                     e.AfterJson?.Contains("\"action\":\"UploadProfileSync\"", StringComparison.Ordinal) == true &&
                     e.AfterJson.Contains("\"resourceType\":\"ProfileSync\"", StringComparison.Ordinal));
            string[] authenticatedAuditPaths =
            [
                "/api/v1/ai/search/preview",
                "/api/v1/ai/search",
                "/admin/ai/test-connection",
                "/api/v1/catalogue",
                "/api/v1/catalogue/search",
                "/api/v1/catalogue/01LANENDPOINT000000000001",
                "/api/v1/profile/sync",
                $"/api/v1/assets/covers/{assetHash}",
                "/api/v1/books/01LANENDPOINT000000000001/page/1",
                "/api/v1/books/01LANENDPOINT000000000001/file",
            ];
            Assert.All(authenticatedAuditPaths, path =>
                Assert.Contains(
                    auditEvents,
                    audit => audit.EntityId == path &&
                             audit.AfterJson?.Contains("\"authenticated\":true", StringComparison.Ordinal) == true));
            Assert.DoesNotContain(auditEvents, e => e.AfterJson?.Contains(token, StringComparison.Ordinal) == true);
            Assert.DoesNotContain(auditEvents, e => e.AfterJson?.Contains(adminSession.Token, StringComparison.Ordinal) == true);
            Assert.DoesNotContain(auditEvents, e => e.AfterJson?.Contains(managedToken, StringComparison.Ordinal) == true);
            rateLimitedSession.Dispose();

            await services.GetRequiredService<IHostModeSettingsRepository>()
                .SaveAsync(new HostModeSettings(true, port, HostContentDeliveryMode.FileStream, "Ogma Endpoint Test"));
            LibraryHostStatus fileStreamStarted = await host.StartAsync();
            string fileStreamEnrollmentCode = fileStreamStarted.EnrollmentCode ??
                                              throw new InvalidOperationException("Host did not issue an enrollment code.");
            using HttpClient fileStreamHttp = CreatePinnedTestClient(port);
            using HttpResponseMessage fileStreamSession = await fileStreamHttp.PostAsJsonAsync(
                "/api/v1/auth/session",
                new
                {
                    clientId = "teacher-1",
                    role = "Teacher",
                    lifetimeMinutes = 5,
                    enrollmentCode = fileStreamEnrollmentCode,
                });
            string fileStreamToken = await ReadJsonStringAsync(fileStreamSession, "token");
            fileStreamHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fileStreamToken);
            using HttpResponseMessage disabledPageRender = await fileStreamHttp.GetAsync("/api/v1/books/01LANENDPOINT000000000001/page/1");
            using HttpResponseMessage enabledFileStream = await fileStreamHttp.GetAsync("/api/v1/books/01LANENDPOINT000000000001/file");
            byte[] streamedPdf = await enabledFileStream.Content.ReadAsByteArrayAsync();
            await host.StopAsync();

            Assert.Equal(HttpStatusCode.Forbidden, disabledPageRender.StatusCode);
            Assert.Equal(HttpStatusCode.OK, enabledFileStream.StatusCode);
            Assert.Equal("application/pdf", enabledFileStream.Content.Headers.ContentType?.MediaType);
            Assert.Equal(pdfBytes, streamedPdf);

            await using CatalogueDbContext verifyFileStream = services.GetRequiredService<CatalogueDbContext>();
            List<AuditEventRow> fileStreamAuditEvents = await verifyFileStream.AuditEvents
                .Where(x => x.EventType == "LanHostRequestServed")
                .ToListAsync();

            Assert.Contains(
                fileStreamAuditEvents,
                e => e.EntityId == "/api/v1/books/01LANENDPOINT000000000001/file" &&
                     e.ActorId == "client:teacher-1" &&
                     e.AfterJson?.Contains("\"statusCode\":200", StringComparison.Ordinal) == true &&
                     e.AfterJson.Contains("\"contentMode\":\"FileStream\"", StringComparison.Ordinal) &&
                     e.AfterJson.Contains("\"action\":\"StreamFile\"", StringComparison.Ordinal) &&
                     e.AfterJson.Contains("\"resourceType\":\"BookFile\"", StringComparison.Ordinal) &&
                     e.AfterJson.Contains("\"resourceId\":\"01LANENDPOINT000000000001\"", StringComparison.Ordinal));
            Assert.DoesNotContain(fileStreamAuditEvents, e => e.AfterJson?.Contains(fileStreamToken, StringComparison.Ordinal) == true);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task HostListener_StudentTeacherAndGuest_AuthenticateWithReaderPermissions()
    {
        string dataDirectory = CreateTempDirectory();
        int port = GetFreeTcpPort();

        try
        {
            await using ServiceProvider services = await CreateServicesAsync(dataDirectory);
            await services.GetRequiredService<IHostModeSettingsRepository>()
                .SaveAsync(new HostModeSettings(true, port, HostContentDeliveryMode.PageRender, "Ogma Role Test"));

            ILibraryHostService host = services.GetRequiredService<ILibraryHostService>();
            LibraryHostStatus started = await host.StartAsync();
            string enrollmentCode = started.EnrollmentCode ??
                                    throw new InvalidOperationException("Host did not issue an enrollment code.");

            try
            {
                foreach (string role in new[] { "student", "teacher", "guest" })
                {
                    using HttpClient http = CreatePinnedTestClient(port);
                    using HttpResponseMessage session = await http.PostAsJsonAsync(
                        "/api/v1/auth/session",
                        new
                        {
                            clientId = $"{role}-role-test",
                            role,
                            lifetimeMinutes = 5,
                            enrollmentCode,
                        });
                    string token = await ReadJsonStringAsync(session, "token");
                    ClientSessionSnapshot? activeSession = await services
                        .GetRequiredService<IClientSessionService>()
                        .GetActiveAsync(token);

                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    using HttpResponseMessage catalogue = await http.GetAsync("/api/v1/catalogue?pageSize=1");
                    using HttpResponseMessage hostAdmin = await http.PostAsync(
                        "/admin/ai/test-connection?providerId=openai",
                        null);

                    Assert.Equal(HttpStatusCode.OK, session.StatusCode);
                    Assert.Equal(role, activeSession?.Role, ignoreCase: true);
                    Assert.Equal(HttpStatusCode.OK, catalogue.StatusCode);
                    Assert.Equal(HttpStatusCode.Forbidden, hostAdmin.StatusCode);
                }
            }
            finally
            {
                await host.StopAsync();
            }
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string dataDirectory)
    {
        ServiceProvider services = new ServiceCollection()
            .AddCatalogueContext(dataDirectory, dataDirectory)
            .AddSingleton<IPdfRendererFactory>(new MockPdfRendererFactory(pageCount: 3))
            .AddLanHostServices(dataDirectory)
            .AddSingleton<IAiProvider>(new FakeAiProvider())
            .AddSchoolAdminServices(dataDirectory)
            .AddSingleton<ILanBindAddressSelector>(new StaticLanBindAddressSelector(IPAddress.Loopback))
            .BuildServiceProvider();

        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        await context.Database.MigrateAsync();
        return services;
    }

    private static async Task SeedBookAsync(ServiceProvider services)
    {
        await using CatalogueDbContext context = services.GetRequiredService<CatalogueDbContext>();
        context.Books.AddRange(
            new BookRow
            {
                BookId = "01LANENDPOINT000000000001",
                Title = "LAN Endpoint Book",
                RelativePath = "lan-endpoint-book.pdf",
                Sha256Hash = new string('d', 64),
                SizeBytes = 128,
                MtimeTicks = DateTimeOffset.UtcNow.UtcTicks,
                Status = 0,
                IndexStatus = 0,
                EmbeddingStatus = 0,
                IsOcrDerived = false,
                IsPasswordProtected = false,
                Year = 2026,
                BookFiles =
                [
                    new BookFileRow
                    {
                        RelativePath = "lan-endpoint-book.pdf",
                        FileStatus = 0,
                        LastSeenUtc = DateTimeOffset.UtcNow,
                    },
                ],
            },
            new BookRow
            {
                BookId = "01LANENDPOINT000000000002",
                Title = "Second LAN Endpoint Book",
                RelativePath = "second-lan-endpoint-book.pdf",
                Sha256Hash = new string('c', 64),
                SizeBytes = 256,
                MtimeTicks = DateTimeOffset.UtcNow.UtcTicks,
                Status = 0,
                IndexStatus = 0,
                EmbeddingStatus = 0,
                IsOcrDerived = false,
                IsPasswordProtected = false,
                Year = 2026,
                BookFiles =
                [
                    new BookFileRow
                    {
                        RelativePath = "second-lan-endpoint-book.pdf",
                        FileStatus = 0,
                        LastSeenUtc = DateTimeOffset.UtcNow,
                    },
                ],
            },
            new BookRow
            {
                BookId = "01LANENDPOINT000000000003",
                Title = "Unpublished LAN Endpoint Book",
                RelativePath = "unpublished-lan-endpoint-book.pdf",
                Sha256Hash = new string('e', 64),
                SizeBytes = 512,
                MtimeTicks = DateTimeOffset.UtcNow.UtcTicks,
                Status = 1,
                IndexStatus = 0,
                EmbeddingStatus = 0,
                IsOcrDerived = false,
                IsPasswordProtected = false,
                Year = 2026,
            },
            new BookRow
            {
                BookId = "01LANENDPOINT000000000004",
                Title = "Inactive LAN Endpoint Book",
                RelativePath = "inactive-lan-endpoint-book.pdf",
                Sha256Hash = new string('f', 64),
                SizeBytes = 512,
                MtimeTicks = DateTimeOffset.UtcNow.UtcTicks,
                Status = 2,
                IndexStatus = 0,
                EmbeddingStatus = 0,
                IsOcrDerived = false,
                IsPasswordProtected = false,
                Year = 2026,
            });
        await context.SaveChangesAsync();
    }

    private static HttpClient CreatePinnedTestClient(int port)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://127.0.0.1:{port}"),
        };
    }

    private static async Task<string> ReadJsonStringAsync(HttpResponseMessage response, string propertyName)
    {
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static JsonElement GetJsonProperty(JsonElement element, string camelName)
    {
        if (element.TryGetProperty(camelName, out JsonElement camelValue))
        {
            return camelValue;
        }

        string pascalName = char.ToUpperInvariant(camelName[0]) + camelName[1..];
        return element.GetProperty(pascalName);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-lanhost-endpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private sealed class StaticLanBindAddressSelector(IPAddress address) : ILanBindAddressSelector
    {
        public IPAddress SelectBindAddress() => address;
    }

    private sealed class FakeAiProvider : IAiProvider
    {
        public string ProviderKey => "fake";

        public bool IsLocalOnly => false;

        public Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletion(
                "Use LAN Endpoint Book [[book:01LANENDPOINT000000000001]] and not [[book:01FAKEBOOK000000000001]].",
                PromptTokens: 20,
                CompletionTokens: 8));
    }
}
