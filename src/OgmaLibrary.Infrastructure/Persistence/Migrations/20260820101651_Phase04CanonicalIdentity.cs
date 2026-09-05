using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase04CanonicalIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanonicalWorks",
                columns: table => new
                {
                    WorkId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    ResolutionState = table.Column<int>(type: "INTEGER", nullable: false),
                    CanonicalTitle = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalWorks", x => x.WorkId);
                    table.CheckConstraint("CK_CanonicalWorks_Id", "length(WorkId) = 26");
                    table.CheckConstraint("CK_CanonicalWorks_State", "ResolutionState BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "ContentAssets",
                columns: table => new
                {
                    ContentAssetId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Sha256Hash = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    FingerprintVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    VerificationStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentAssets", x => x.ContentAssetId);
                    table.CheckConstraint("CK_ContentAssets_FingerprintVersion", "FingerprintVersion > 0");
                    table.CheckConstraint("CK_ContentAssets_Id", "length(ContentAssetId) = 26");
                    table.CheckConstraint("CK_ContentAssets_Sha256", "length(Sha256Hash) = 64 AND Sha256Hash NOT GLOB '*[^0-9a-f]*'");
                    table.CheckConstraint("CK_ContentAssets_Size", "SizeBytes IS NULL OR SizeBytes > 0");
                    table.CheckConstraint("CK_ContentAssets_Verification", "VerificationStatus IN (0, 1)");
                });

            migrationBuilder.CreateTable(
                name: "LibraryRoots",
                columns: table => new
                {
                    LibraryRootId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RootStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCompatibilityRoot = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryRoots", x => x.LibraryRootId);
                    table.CheckConstraint("CK_LibraryRoots_Id", "length(LibraryRootId) = 26");
                    table.CheckConstraint("CK_LibraryRoots_Status", "RootStatus BETWEEN 0 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "CanonicalEditions",
                columns: table => new
                {
                    EditionId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    WorkId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    ResolutionState = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    PublicationYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Publisher = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalEditions", x => x.EditionId);
                    table.UniqueConstraint("AK_CanonicalEditions_EditionId_WorkId", x => new { x.EditionId, x.WorkId });
                    table.CheckConstraint("CK_CanonicalEditions_Id", "length(EditionId) = 26");
                    table.CheckConstraint("CK_CanonicalEditions_State", "ResolutionState BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_CanonicalEditions_WorkId", "length(WorkId) = 26");
                    table.ForeignKey(
                        name: "FK_CanonicalEditions_CanonicalWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CanonicalWorks",
                        principalColumn: "WorkId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FileOccurrences",
                columns: table => new
                {
                    FileOccurrenceId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    LibraryRootId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    ContentAssetId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    NormalizedRelativePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    AvailabilityStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ModifiedUtcTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    PdfFingerprint = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileOccurrences", x => x.FileOccurrenceId);
                    table.CheckConstraint("CK_FileOccurrences_AssetId", "ContentAssetId IS NULL OR length(ContentAssetId) = 26");
                    table.CheckConstraint("CK_FileOccurrences_Availability", "AvailabilityStatus IN (0, 1)");
                    table.CheckConstraint("CK_FileOccurrences_Id", "length(FileOccurrenceId) = 26");
                    table.CheckConstraint("CK_FileOccurrences_RootId", "length(LibraryRootId) = 26");
                    table.CheckConstraint("CK_FileOccurrences_Size", "SizeBytes IS NULL OR SizeBytes >= 0");
                    table.ForeignKey(
                        name: "FK_FileOccurrences_ContentAssets_ContentAssetId",
                        column: x => x.ContentAssetId,
                        principalTable: "ContentAssets",
                        principalColumn: "ContentAssetId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FileOccurrences_LibraryRoots_LibraryRootId",
                        column: x => x.LibraryRootId,
                        principalTable: "LibraryRoots",
                        principalColumn: "LibraryRootId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BibliographicIdentifiers",
                columns: table => new
                {
                    BibliographicIdentifierId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerScope = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    EditionId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IdentifierKind = table.Column<int>(type: "INTEGER", nullable: false),
                    NormalizedValue = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibliographicIdentifiers", x => x.BibliographicIdentifierId);
                    table.CheckConstraint("CK_BibliographicIdentifiers_Kind", "IdentifierKind BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_BibliographicIdentifiers_Owner", "(OwnerScope = 0 AND WorkId IS NOT NULL AND EditionId IS NULL) OR (OwnerScope = 1 AND EditionId IS NOT NULL AND WorkId IS NULL)");
                    table.CheckConstraint("CK_BibliographicIdentifiers_Scope", "OwnerScope IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_BibliographicIdentifiers_CanonicalEditions_EditionId",
                        column: x => x.EditionId,
                        principalTable: "CanonicalEditions",
                        principalColumn: "EditionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BibliographicIdentifiers_CanonicalWorks_WorkId",
                        column: x => x.WorkId,
                        principalTable: "CanonicalWorks",
                        principalColumn: "WorkId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EditionContentAssets",
                columns: table => new
                {
                    EditionId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    ContentAssetId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditionContentAssets", x => new { x.EditionId, x.ContentAssetId });
                    table.ForeignKey(
                        name: "FK_EditionContentAssets_CanonicalEditions_EditionId",
                        column: x => x.EditionId,
                        principalTable: "CanonicalEditions",
                        principalColumn: "EditionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EditionContentAssets_ContentAssets_ContentAssetId",
                        column: x => x.ContentAssetId,
                        principalTable: "ContentAssets",
                        principalColumn: "ContentAssetId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogueItems",
                columns: table => new
                {
                    CatalogueItemId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    WorkId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    EditionId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    PreferredOccurrenceId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogueItems", x => x.CatalogueItemId);
                    table.CheckConstraint("CK_CatalogueItems_EditionId", "length(EditionId) = 26");
                    table.CheckConstraint("CK_CatalogueItems_Id", "length(CatalogueItemId) = 26");
                    table.CheckConstraint("CK_CatalogueItems_PreferredOccurrenceId", "PreferredOccurrenceId IS NULL OR length(PreferredOccurrenceId) = 26");
                    table.CheckConstraint("CK_CatalogueItems_WorkId", "length(WorkId) = 26");
                    table.ForeignKey(
                        name: "FK_CatalogueItems_CanonicalEditions_EditionId_WorkId",
                        columns: x => new { x.EditionId, x.WorkId },
                        principalTable: "CanonicalEditions",
                        principalColumns: new[] { "EditionId", "WorkId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatalogueItems_FileOccurrences_PreferredOccurrenceId",
                        column: x => x.PreferredOccurrenceId,
                        principalTable: "FileOccurrences",
                        principalColumn: "FileOccurrenceId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IdentityDecisions",
                columns: table => new
                {
                    IdentityDecisionId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    SubjectOccurrenceId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    CandidateOccurrenceId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Relationship = table.Column<int>(type: "INTEGER", nullable: false),
                    Disposition = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidenceTier = table.Column<int>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    PolicyVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityDecisions", x => x.IdentityDecisionId);
                    table.CheckConstraint("CK_IdentityDecisions_Confidence", "Confidence BETWEEN 0.0 AND 1.0");
                    table.CheckConstraint("CK_IdentityDecisions_Disposition", "Disposition IN (0, 1)");
                    table.CheckConstraint("CK_IdentityDecisions_DistinctOccurrences", "SubjectOccurrenceId <> CandidateOccurrenceId");
                    table.CheckConstraint("CK_IdentityDecisions_Id", "length(IdentityDecisionId) = 26");
                    table.CheckConstraint("CK_IdentityDecisions_PolicyVersion", "PolicyVersion > 0");
                    table.CheckConstraint("CK_IdentityDecisions_Relationship", "Relationship BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_IdentityDecisions_Tier", "EvidenceTier BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_IdentityDecisions_FileOccurrences_CandidateOccurrenceId",
                        column: x => x.CandidateOccurrenceId,
                        principalTable: "FileOccurrences",
                        principalColumn: "FileOccurrenceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdentityDecisions_FileOccurrences_SubjectOccurrenceId",
                        column: x => x.SubjectOccurrenceId,
                        principalTable: "FileOccurrences",
                        principalColumn: "FileOccurrenceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogueItemOccurrences",
                columns: table => new
                {
                    CatalogueItemId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    FileOccurrenceId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogueItemOccurrences", x => new { x.CatalogueItemId, x.FileOccurrenceId });
                    table.ForeignKey(
                        name: "FK_CatalogueItemOccurrences_CatalogueItems_CatalogueItemId",
                        column: x => x.CatalogueItemId,
                        principalTable: "CatalogueItems",
                        principalColumn: "CatalogueItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogueItemOccurrences_FileOccurrences_FileOccurrenceId",
                        column: x => x.FileOccurrenceId,
                        principalTable: "FileOccurrences",
                        principalColumn: "FileOccurrenceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegacyIdentityAliases",
                columns: table => new
                {
                    LegacyBookId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CatalogueItemId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    WorkId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    EditionId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    MigrationVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyIdentityAliases", x => x.LegacyBookId);
                    table.CheckConstraint("CK_LegacyIdentityAliases_CatalogueItemId", "length(CatalogueItemId) = 26");
                    table.CheckConstraint("CK_LegacyIdentityAliases_EditionId", "length(EditionId) = 26");
                    table.CheckConstraint("CK_LegacyIdentityAliases_Version", "MigrationVersion > 0");
                    table.CheckConstraint("CK_LegacyIdentityAliases_WorkId", "length(WorkId) = 26");
                    table.ForeignKey(
                        name: "FK_LegacyIdentityAliases_CanonicalEditions_EditionId_WorkId",
                        columns: x => new { x.EditionId, x.WorkId },
                        principalTable: "CanonicalEditions",
                        principalColumns: new[] { "EditionId", "WorkId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegacyIdentityAliases_CatalogueItems_CatalogueItemId",
                        column: x => x.CatalogueItemId,
                        principalTable: "CatalogueItems",
                        principalColumn: "CatalogueItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BibliographicIdentifiers_EditionId",
                table: "BibliographicIdentifiers",
                column: "EditionId");

            migrationBuilder.CreateIndex(
                name: "IX_BibliographicIdentifiers_WorkId",
                table: "BibliographicIdentifiers",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "UX_BibliographicIdentifiers_ScopedValue",
                table: "BibliographicIdentifiers",
                columns: new[] { "OwnerScope", "Source", "IdentifierKind", "NormalizedValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalEditions_Work_State",
                table: "CanonicalEditions",
                columns: new[] { "WorkId", "ResolutionState" });

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalWorks_State",
                table: "CanonicalWorks",
                column: "ResolutionState");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueItemOccurrences_OccurrenceId",
                table: "CatalogueItemOccurrences",
                column: "FileOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueItems_EditionId_WorkId",
                table: "CatalogueItems",
                columns: new[] { "EditionId", "WorkId" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueItems_PreferredOccurrenceId",
                table: "CatalogueItems",
                column: "PreferredOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogueItems_Work_Edition",
                table: "CatalogueItems",
                columns: new[] { "WorkId", "EditionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ContentAssets_Hash_Version",
                table: "ContentAssets",
                columns: new[] { "Sha256Hash", "FingerprintVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditionContentAssets_AssetId",
                table: "EditionContentAssets",
                column: "ContentAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FileOccurrences_Asset_Availability",
                table: "FileOccurrences",
                columns: new[] { "ContentAssetId", "AvailabilityStatus" });

            migrationBuilder.CreateIndex(
                name: "UX_FileOccurrences_Root_NormalizedPath",
                table: "FileOccurrences",
                columns: new[] { "LibraryRootId", "NormalizedRelativePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityDecisions_CandidateOccurrenceId",
                table: "IdentityDecisions",
                column: "CandidateOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityDecisions_Pair_Version",
                table: "IdentityDecisions",
                columns: new[] { "SubjectOccurrenceId", "CandidateOccurrenceId", "PolicyVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyIdentityAliases_EditionId_WorkId",
                table: "LegacyIdentityAliases",
                columns: new[] { "EditionId", "WorkId" });

            migrationBuilder.CreateIndex(
                name: "UX_LegacyIdentityAliases_CatalogueItemId",
                table: "LegacyIdentityAliases",
                column: "CatalogueItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BibliographicIdentifiers");

            migrationBuilder.DropTable(
                name: "CatalogueItemOccurrences");

            migrationBuilder.DropTable(
                name: "EditionContentAssets");

            migrationBuilder.DropTable(
                name: "IdentityDecisions");

            migrationBuilder.DropTable(
                name: "LegacyIdentityAliases");

            migrationBuilder.DropTable(
                name: "CatalogueItems");

            migrationBuilder.DropTable(
                name: "CanonicalEditions");

            migrationBuilder.DropTable(
                name: "FileOccurrences");

            migrationBuilder.DropTable(
                name: "CanonicalWorks");

            migrationBuilder.DropTable(
                name: "ContentAssets");

            migrationBuilder.DropTable(
                name: "LibraryRoots");
        }
    }
}
