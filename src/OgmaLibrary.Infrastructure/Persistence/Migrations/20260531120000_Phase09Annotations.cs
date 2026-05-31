using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OgmaLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase09Annotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnotationLayers",
                columns: table => new
                {
                    LayerId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "#FFD700"),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnotationLayers", x => x.LayerId);
                    table.ForeignKey(
                        name: "FK_AnnotationLayers_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnnotationsV2",
                columns: table => new
                {
                    AnnotationId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    LayerId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    RegionsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    ColorKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    QuoteText = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    NoteText = table.Column<string>(type: "TEXT", maxLength: 65536, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnotationsV2", x => x.AnnotationId);
                    table.ForeignKey(
                        name: "FK_AnnotationsV2_AnnotationLayers_LayerId",
                        column: x => x.LayerId,
                        principalTable: "AnnotationLayers",
                        principalColumn: "LayerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AnnotationsV2_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadingMemory",
                columns: table => new
                {
                    BookId = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    OpenedBecause = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    KeyInsight = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    OpenQuestions = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: true),
                    Disposition = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingMemory", x => x.BookId);
                    table.ForeignKey(
                        name: "FK_ReadingMemory_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnotationLayers_BookId",
                table: "AnnotationLayers",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnotationsV2_BookId_LayerId",
                table: "AnnotationsV2",
                columns: new[] { "BookId", "LayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnnotationsV2_LayerId",
                table: "AnnotationsV2",
                column: "LayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AnnotationsV2");
            migrationBuilder.DropTable(name: "AnnotationLayers");
            migrationBuilder.DropTable(name: "ReadingMemory");
        }
    }
}
