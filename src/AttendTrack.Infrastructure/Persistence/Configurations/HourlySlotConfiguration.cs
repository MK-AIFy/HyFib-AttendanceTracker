using AttendTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendTrack.Infrastructure.Persistence.Configurations;

public sealed class HourlySlotConfiguration : IEntityTypeConfiguration<HourlySlot>
{
    public void Configure(EntityTypeBuilder<HourlySlot> b)
    {
        b.ToTable("hourly_slots");
        b.HasKey(s => s.Id);

        // UNIQUE: one slot per hour per attendance record
        b.HasIndex(s => new { s.AttendanceRecordId, s.HourSlotNumber }).IsUnique();

        b.Property(s => s.AttendanceRecordId).IsRequired();
        b.Property(s => s.EmployeeId)        .IsRequired();
        b.Property(s => s.WorkDate)          .IsRequired();

        b.Property(s => s.HourSlotNumber)
            .HasColumnName("hour_slot")
            .IsRequired();

        b.Property(s => s.MinutesWorked)
            .HasColumnName("minutes_worked")
            .HasDefaultValue(0)
            .IsRequired();

        b.Property(s => s.IsBreak)    .HasDefaultValue(false);
        b.Property(s => s.IsOvertime) .HasDefaultValue(false);

        b.Property(s => s.CreatedAt).HasColumnType("timestamptz").IsRequired();
        b.Property(s => s.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        // EmployeeId here (a plain Guid, unlike AttendanceRecord.EmployeeId which is the
        // EmployeeId value object — EF can't build a direct FK across that CLR type
        // boundary without widening this entity's public API) is a denormalized copy of
        // AttendanceRecord.EmployeeId. It's already cascade-deleted transitively via
        // AttendanceRecordId -> AttendanceRecord -> Employee (see
        // AttendanceRecordConfiguration), which is what actually fixes the orphaned-data
        // bug in DataRetentionPurgeService's employee purge.
    }
}

