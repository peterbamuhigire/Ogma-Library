using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiQueryHistory",
                columns: table => new
                {
                    QueryId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QueryText = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    ProviderKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PrivacyTier = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    RequestPayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ResponseSummary = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    TokensIn = table.Column<int>(type: "INTEGER", nullable: true),
                    TokensOut = table.Column<int>(type: "INTEGER", nullable: true),
                    CostEstimate = table.Column<decimal>(type: "REAL", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiQueryHistory", x => x.QueryId);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    EventId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    BeforeJson = table.Column<string>(type: "TEXT", nullable: true),
                    AfterJson = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsLocalOnly = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Authors",
                columns: table => new
                {
                    AuthorId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SortName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Authors", x => x.AuthorId);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    JobId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    Payload = table.Column<string>(type: "TEXT", maxLength: 65536, nullable: true),
                    StartedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "Shelves",
                columns: table => new
                {
                    ShelfId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ShelfType = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Query = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelves", x => x.ShelfId);
                });

            migrationBuilder.CreateTable(
                name: "Works",
                columns: table => new
                {
                    WorkId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CanonicalTitle = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CanonicalAuthorId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Works", x => x.WorkId);
                });

            migrationBuilder.CreateTable(
                name: "Editions",
                columns: table => new
                {
                    EditionId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkId = table.Column<long>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    PublicationYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Publisher = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Editions", x => x.EditionId);
                    table.ForeignKey(
                        name: "FK_Editions_Works_WorkId",
                        column: x => x.WorkId,
                        principalTable: "Works",
                        principalColumn: "WorkId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Sha256Hash = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: true),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    MtimeTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    PdfFingerprint = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    IsbnNormalized = table.Column<string>(type: "TEXT", maxLength: 13, nullable: true),
                    Doi = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    EditionId = table.Column<long>(type: "INTEGER", nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.BookId);
                    table.ForeignKey(
                        name: "FK_Books_Editions_EditionId",
                        column: x => x.EditionId,
                        principalTable: "Editions",
                        principalColumn: "EditionId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Annotations",
                columns: table => new
                {
                    AnnotationId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Page = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ColorKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Annotations", x => x.AnnotationId);
                    table.ForeignKey(
                        name: "FK_Annotations_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookAuthors",
                columns: table => new
                {
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    AuthorId = table.Column<long>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookAuthors", x => new { x.BookId, x.AuthorId });
                    table.ForeignKey(
                        name: "FK_BookAuthors_Authors_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Authors",
                        principalColumn: "AuthorId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookAuthors_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookFiles",
                columns: table => new
                {
                    BookFileId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    FileStatus = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookFiles", x => x.BookFileId);
                    table.ForeignKey(
                        name: "FK_BookFiles_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookmarks",
                columns: table => new
                {
                    BookmarkId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Page = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookmarks", x => x.BookmarkId);
                    table.ForeignKey(
                        name: "FK_Bookmarks_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookMetadataFields",
                columns: table => new
                {
                    FieldId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SourceTimestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    IsOverridden = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookMetadataFields", x => x.FieldId);
                    table.ForeignKey(
                        name: "FK_BookMetadataFields_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExtractedPages",
                columns: table => new
                {
                    ExtractedPageId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TextContent = table.Column<string>(type: "TEXT", nullable: true),
                    ExtractionMethod = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ExtractionUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractedPages", x => x.ExtractedPageId);
                    table.ForeignKey(
                        name: "FK_ExtractedPages_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetadataLookups",
                columns: table => new
                {
                    LookupId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RequestIsbn = table.Column<string>(type: "TEXT", maxLength: 13, nullable: true),
                    ResponseJson = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    Applied = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataLookups", x => x.LookupId);
                    table.ForeignKey(
                        name: "FK_MetadataLookups_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadingProgress",
                columns: table => new
                {
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    CurrentPage = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ScrollOffsetPx = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    LastReadUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TotalPagesRead = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CompletionPct = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingProgress", x => x.BookId);
                    table.ForeignKey(
                        name: "FK_ReadingProgress_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShelfBooks",
                columns: table => new
                {
                    ShelfId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    AddedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShelfBooks", x => new { x.ShelfId, x.BookId });
                    table.ForeignKey(
                        name: "FK_ShelfBooks_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShelfBooks_Shelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "Shelves",
                        principalColumn: "ShelfId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnnotationBodies",
                columns: table => new
                {
                    AnnotationId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    QuoteText = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    NoteText = table.Column<string>(type: "TEXT", maxLength: 65536, nullable: true),
                    RectJson = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnotationBodies", x => x.AnnotationId);
                    table.ForeignKey(
                        name: "FK_AnnotationBodies_Annotations_AnnotationId",
                        column: x => x.AnnotationId,
                        principalTable: "Annotations",
                        principalColumn: "AnnotationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SearchChunks",
                columns: table => new
                {
                    ChunkId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    ExtractedPageId = table.Column<long>(type: "INTEGER", nullable: true),
                    ChunkIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ChunkText = table.Column<string>(type: "TEXT", nullable: true),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchChunks", x => x.ChunkId);
                    table.ForeignKey(
                        name: "FK_SearchChunks_ExtractedPages_ExtractedPageId",
                        column: x => x.ExtractedPageId,
                        principalTable: "ExtractedPages",
                        principalColumn: "ExtractedPageId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EmbeddingVectors",
                columns: table => new
                {
                    VectorId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChunkId = table.Column<long>(type: "INTEGER", nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DimensionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    VectorBlob = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingVectors", x => x.VectorId);
                    table.ForeignKey(
                        name: "FK_EmbeddingVectors_SearchChunks_ChunkId",
                        column: x => x.ChunkId,
                        principalTable: "SearchChunks",
                        principalColumn: "ChunkId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiQueryHistory_CreatedUtc",
                table: "AiQueryHistory",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Annotations_BookId_Page",
                table: "Annotations",
                columns: new[] { "BookId", "Page" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityId_EntityType",
                table: "AuditEvents",
                columns: new[] { "EntityId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Timestamp",
                table: "AuditEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Authors_NormalizedName",
                table: "Authors",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_BookAuthors_AuthorId",
                table: "BookAuthors",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_BookFiles_BookId_FileStatus",
                table: "BookFiles",
                columns: new[] { "BookId", "FileStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_BookFiles_RelativePath",
                table: "BookFiles",
                column: "RelativePath");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_BookId_Page",
                table: "Bookmarks",
                columns: new[] { "BookId", "Page" });

            migrationBuilder.CreateIndex(
                name: "IX_BookMetadataFields_BookId_FieldName_Source",
                table: "BookMetadataFields",
                columns: new[] { "BookId", "FieldName", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_Books_EditionId",
                table: "Books",
                column: "EditionId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_IsbnNormalized",
                table: "Books",
                column: "IsbnNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_Books_RelativePath",
                table: "Books",
                column: "RelativePath");

            migrationBuilder.CreateIndex(
                name: "IX_Books_Sha256Hash",
                table: "Books",
                column: "Sha256Hash");

            migrationBuilder.CreateIndex(
                name: "IX_Books_Status",
                table: "Books",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Editions_WorkId",
                table: "Editions",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingVectors_ChunkId_ModelId",
                table: "EmbeddingVectors",
                columns: new[] { "ChunkId", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractedPages_BookId_PageNumber",
                table: "ExtractedPages",
                columns: new[] { "BookId", "PageNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_JobType",
                table: "Jobs",
                columns: new[] { "Status", "JobType" });

            migrationBuilder.CreateIndex(
                name: "UQ_Jobs_IdempotencyKey",
                table: "Jobs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataLookups_BookId_Provider_Timestamp",
                table: "MetadataLookups",
                columns: new[] { "BookId", "Provider", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchChunks_BookId_ChunkIndex",
                table: "SearchChunks",
                columns: new[] { "BookId", "ChunkIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchChunks_ExtractedPageId",
                table: "SearchChunks",
                column: "ExtractedPageId");

            migrationBuilder.CreateIndex(
                name: "IX_ShelfBooks_BookId",
                table: "ShelfBooks",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_Shelves_Name",
                table: "Shelves",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiQueryHistory");

            migrationBuilder.DropTable(
                name: "AnnotationBodies");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "BookAuthors");

            migrationBuilder.DropTable(
                name: "BookFiles");

            migrationBuilder.DropTable(
                name: "Bookmarks");

            migrationBuilder.DropTable(
                name: "BookMetadataFields");

            migrationBuilder.DropTable(
                name: "EmbeddingVectors");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "MetadataLookups");

            migrationBuilder.DropTable(
                name: "ReadingProgress");

            migrationBuilder.DropTable(
                name: "ShelfBooks");

            migrationBuilder.DropTable(
                name: "Annotations");

            migrationBuilder.DropTable(
                name: "Authors");

            migrationBuilder.DropTable(
                name: "SearchChunks");

            migrationBuilder.DropTable(
                name: "Shelves");

            migrationBuilder.DropTable(
                name: "ExtractedPages");

            migrationBuilder.DropTable(
                name: "Books");

            migrationBuilder.DropTable(
                name: "Editions");

            migrationBuilder.DropTable(
                name: "Works");
        }
    }
}
