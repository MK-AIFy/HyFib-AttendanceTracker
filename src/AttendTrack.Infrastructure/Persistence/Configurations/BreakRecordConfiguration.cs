using AttendTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendTrack.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for BreakRecord. Schema matches the InitialCreate migration:
/// table "BreakRecords", enum columns stored as int, timestamps as timestamptz.
/// Keep this file aligned with the migration to prevent EF from generating
/// spurious column rewrites.
/// </summary>
public sealed class BreakRecordConfiguration : IEntityTypeConfiguration<BreakRecord>
{
    public void Configure(EntityTypeBuilder<BreakRecord> b)
    {
        b.ToTable("BreakRecords");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedOnAdd();
        b.Property(x => x.AttendanceRecordId).HasColumnType("uuid").IsRequired();
        b.Property(x => x.EmployeeId)         .HasColumnType("uuid").IsRequired();

        b.Property(x => x.StartTime).HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.EndTime)  .HasColumnType("timestamp with time zone");

        // Enums kept as int to match the InitialCreate migration.
        b.Property(x => x.Type)       .HasColumnType("integer").IsRequired();
        b.Property(x => x.StartSource).HasColumnType("integer").IsRequired();
        b.Property(x => x.EndSource)  .HasColumnType("integer");

        b.HasIndex(x => x.AttendanceRecordId);
        b.HasIndex(x => x.EmployeeId);
        b.HasIndex(x => new { x.EmployeeId, x.StartTime });

        b.Ignore(x => x.Duration);
        b.Ignore(x => x.IsActive);
        b.Ignore(x => x.DomainEvents);
    }
}
