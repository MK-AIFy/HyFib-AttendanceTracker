using AttendTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendTrack.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(a => a.Id);

        b.Property(a => a.Action)    .HasMaxLength(100).IsRequired();
        b.Property(a => a.ActorRole) .HasMaxLength(50);
        b.Property(a => a.EntityType).HasMaxLength(100);
        b.Property(a => a.IpAddress) .HasMaxLength(45);

        // [Gap 6] JSONB columns for before/after state (DPDP audit compliance)
        b.Property(a => a.OldValues).HasColumnType("jsonb");
        b.Property(a => a.NewValues).HasColumnType("jsonb");

        b.Property(a => a.OccurredAt).HasColumnType("timestamptz").IsRequired();

        // Efficient queries by entity for audit trail views
        b.HasIndex(a => new { a.EntityType, a.EntityId });
        b.HasIndex(a => a.ActorId);
        b.HasIndex(a => a.OccurredAt);
    }
}

