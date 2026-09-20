using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendTrack.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.ToTable("attendance_records");
        b.HasKey(r => r.Id);

        // [Gap 2] UNIQUE constraint prevents duplicate check-in for same employee+date
        b.HasIndex(r => new { r.EmployeeId, r.WorkDate }).IsUnique();

        // [Gap 2] PostgreSQL xmin optimistic concurrency token
        // Map the uint Version property to the PostgreSQL xmin system column.
        // xmin is automatically incremented by Postgres on every row update.
        b.Property(r => r.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // EmployeeId value object — stored as UUID column
        b.Property(r => r.EmployeeId)
            .HasConversion(
                id    => id.Value,
                value => EmployeeId.From(value))
            .HasColumnName("employee_id")
            .IsRequired();

        b.Property(r => r.WorkDate).IsRequired();

        // All DateTime properties stored as UTC TIMESTAMPTZ
        b.Property(r => r.CheckInTime) .HasColumnType("timestamptz");
        b.Property(r => r.CheckOutTime).HasColumnType("timestamptz");
        b.Property(r => r.CreatedAt)   .HasColumnType("timestamptz").IsRequired();

        // Enums stored as strings
        b.Property(r => r.CheckInSource)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        b.Property(r => r.CheckOutSource)
            .HasConversion<string>()
            .HasMaxLength(30);

        b.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(AttendanceStatus.Present);

        b.Property(r => r.VerifyMode)           .HasMaxLength(50);
        b.Property(r => r.FaceCaptureImagePath) .HasMaxLength(500);
        b.Property(r => r.HikvisionSerialNo)    .HasMaxLength(100);
        b.Property(r => r.DeviceId)             .HasMaxLength(100);
        b.Property(r => r.Notes)                .HasColumnType("text");

        // BreakDuration — stored as total minutes INTEGER
        b.Property(r => r.BreakDuration)
            .HasConversion(
                ts  => (int)ts.TotalMinutes,
                min => TimeSpan.FromMinutes(min))
            .HasColumnName("break_duration_minutes")
            .HasDefaultValueSql("0");

        // No FK to Employee existed here at all — an AttendanceRecord could reference a
        // nonexistent employee_id with zero DB-level protection, and
        // DataRetentionPurgeService's hard-delete of expired employees (7-year DPDP
        // window) silently orphaned every attendance_records/hourly_slots/BreakRecords
        // row instead of actually cascading, despite its own doc comment claiming
        // otherwise. Cascade (not Restrict): employee deletion is meant to remove the
        // employee's attendance history, that's the entire point of the purge service.
        b.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(r => r.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // EF Core navigation via backing fields
        b.HasMany(r => r.HourlySlots)
            .WithOne()
            .HasForeignKey(s => s.AttendanceRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(r => r.Breaks)
            .WithOne()
            .HasForeignKey(br => br.AttendanceRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Map readonly collection properties to their backing fields
        b.Navigation(r => r.HourlySlots).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(r => r.Breaks)     .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

