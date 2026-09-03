using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.Catalogue.Configurations;

/// <summary>EF constraints for reviewed identity grouping and undo history.</summary>
public sealed class IdentityGroupConfiguration : IEntityTypeConfiguration<IdentityGroupRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityGroupRow> builder)
    {
        builder.ToTable("IdentityGroups", table =>
        {
            table.HasCheckConstraint("CK_IdentityGroups_Id", "length(IdentityGroupId) = 26");
            table.HasCheckConstraint("CK_IdentityGroups_Kind", "Kind BETWEEN 0 AND 1");
            table.HasCheckConstraint("CK_IdentityGroups_Version", "Version > 0");
        });
        builder.HasKey(row => row.IdentityGroupId);
        builder.Property(row => row.IdentityGroupId).HasMaxLength(26);
        builder.Property(row => row.Version).HasDefaultValue(1);
    }
}

/// <summary>EF constraints for identity group members.</summary>
public sealed class IdentityGroupMemberConfiguration : IEntityTypeConfiguration<IdentityGroupMemberRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityGroupMemberRow> builder)
    {
        builder.ToTable("IdentityGroupMembers", table =>
        {
            table.HasCheckConstraint("CK_IdentityGroupMembers_GroupId", "length(IdentityGroupId) = 26");
            table.HasCheckConstraint("CK_IdentityGroupMembers_OccurrenceId", "length(FileOccurrenceId) = 26");
        });
        builder.HasKey(row => new { row.IdentityGroupId, row.FileOccurrenceId });
        builder.Property(row => row.IdentityGroupId).HasMaxLength(26);
        builder.Property(row => row.FileOccurrenceId).HasMaxLength(26);
        builder.HasIndex(row => new { row.FileOccurrenceId, row.IsActive })
            .HasDatabaseName("IX_IdentityGroupMembers_Occurrence_Active");
        builder.HasOne<IdentityGroupRow>()
            .WithMany()
            .HasForeignKey(row => row.IdentityGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>EF constraints for identity group mutation history.</summary>
public sealed class IdentityGroupChangeConfiguration : IEntityTypeConfiguration<IdentityGroupChangeRow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentityGroupChangeRow> builder)
    {
        builder.ToTable("IdentityGroupChanges");
        builder.HasKey(row => row.IdentityGroupChangeId);
        builder.Property(row => row.Operation).IsRequired().HasMaxLength(32);
        builder.Property(row => row.BeforeMembersJson).IsRequired().HasMaxLength(262144);
        builder.Property(row => row.AfterMembersJson).IsRequired().HasMaxLength(262144);
        builder.Property(row => row.Actor).IsRequired().HasMaxLength(128);
        builder.HasIndex(row => new { row.IdentityGroupId, row.IdentityGroupChangeId })
            .HasDatabaseName("IX_IdentityGroupChanges_Group_Order");
        builder.HasOne<IdentityGroupRow>()
            .WithMany()
            .HasForeignKey(row => row.IdentityGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
