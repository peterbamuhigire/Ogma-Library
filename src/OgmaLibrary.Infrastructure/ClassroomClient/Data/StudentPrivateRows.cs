using Microsoft.EntityFrameworkCore;

namespace OgmaLibrary.Infrastructure.ClassroomClient.Data;

internal sealed class StudentReadingProgressRow
{
    public string HostId { get; set; } = string.Empty;

    public string BookId { get; set; } = string.Empty;

    public int LastPage { get; set; }

    public double LastOffsetY { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
}

internal sealed class StudentAnnotationRow
{
    public string Id { get; set; } = string.Empty;

    public string HostId { get; set; } = string.Empty;

    public string BookId { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public string Type { get; set; } = string.Empty;

    public string? Color { get; set; }

    public string? Body { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public bool IsDeleted { get; set; }
}

internal sealed class StudentBookmarkRow
{
    public string Id { get; set; } = string.Empty;

    public string HostId { get; set; } = string.Empty;

    public string BookId { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public string? Label { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public bool IsDeleted { get; set; }
}

internal sealed class StudentAiHistoryRow
{
    public string Id { get; set; } = string.Empty;

    public string HostId { get; set; } = string.Empty;

    public string Query { get; set; } = string.Empty;

    public string? ResponseSummary { get; set; }

    public string Tier { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public bool IsDeleted { get; set; }
}

internal sealed class StudentSyncStateRow
{
    public string HostId { get; set; } = string.Empty;

    public DateTimeOffset? LastSyncedUtc { get; set; }

    public string? LastSyncBlobHash { get; set; }

    public int ConflictCount { get; set; }
}

internal sealed class StudentDbContext : DbContext
{
    public StudentDbContext(DbContextOptions<StudentDbContext> options)
        : base(options)
    {
    }

    public DbSet<StudentReadingProgressRow> ReadingProgress => Set<StudentReadingProgressRow>();

    public DbSet<StudentAnnotationRow> Annotations => Set<StudentAnnotationRow>();

    public DbSet<StudentBookmarkRow> Bookmarks => Set<StudentBookmarkRow>();

    public DbSet<StudentAiHistoryRow> AiHistory => Set<StudentAiHistoryRow>();

    public DbSet<StudentSyncStateRow> SyncState => Set<StudentSyncStateRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudentReadingProgressRow>(builder =>
        {
            builder.ToTable("StudentReadingProgress");
            builder.HasKey(row => new { row.BookId, row.HostId });
            builder.Property(row => row.BookId).HasMaxLength(64);
            builder.Property(row => row.HostId).HasMaxLength(128);
        });

        modelBuilder.Entity<StudentAnnotationRow>(builder =>
        {
            builder.ToTable("StudentAnnotations");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).HasMaxLength(64);
            builder.Property(row => row.BookId).HasMaxLength(64);
            builder.Property(row => row.HostId).HasMaxLength(128);
            builder.Property(row => row.Type).HasMaxLength(32);
            builder.Property(row => row.Color).HasMaxLength(32);
            builder.HasIndex(row => new { row.HostId, row.BookId, row.PageNumber });
        });

        modelBuilder.Entity<StudentBookmarkRow>(builder =>
        {
            builder.ToTable("StudentBookmarks");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).HasMaxLength(64);
            builder.Property(row => row.BookId).HasMaxLength(64);
            builder.Property(row => row.HostId).HasMaxLength(128);
            builder.HasIndex(row => new { row.HostId, row.BookId, row.PageNumber });
        });

        modelBuilder.Entity<StudentAiHistoryRow>(builder =>
        {
            builder.ToTable("StudentAiHistory");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).HasMaxLength(64);
            builder.Property(row => row.HostId).HasMaxLength(128);
            builder.Property(row => row.Tier).HasMaxLength(32);
            builder.HasIndex(row => row.HostId);
        });

        modelBuilder.Entity<StudentSyncStateRow>(builder =>
        {
            builder.ToTable("StudentSyncState");
            builder.HasKey(row => row.HostId);
            builder.Property(row => row.HostId).HasMaxLength(128);
            builder.Property(row => row.LastSyncBlobHash).HasMaxLength(128);
        });
    }
}
