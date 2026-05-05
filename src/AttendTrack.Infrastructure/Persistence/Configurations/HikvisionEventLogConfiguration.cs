using AttendTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendTrack.Infrastructure.Persistence.Configurations;

public sealed class HikvisionEventLogConfiguration : IEntityTypeConfiguration<HikvisionEventLog>
{
    public void Configure(EntityTypeBuilder<HikvisionEventLog> b)
    {
        b.ToTable("hikvision_event_logs");
        b.HasKey(e => e.Id);

        b.Property(e => e.DeviceSerialNumber)    .HasMaxLength(100).IsRequired();
        b.Property(e => e.EmployeeCode)          .HasMaxLength(20).IsRequired();
        b.Property(e => e.EmployeeName)          .HasMaxLength(200).IsRequired();
        b.Property(e => e.AttendanceStatus)      .HasMaxLength(30).IsRequired();
        b.Property(e => e.VerifyMode)            .HasMaxLength(50);
        b.Property(e => e.CardNo)                .HasMaxLength(100);
        b.Property(e => e.FaceCaptureStoragePath).HasMaxLength(500);
        b.Property(e => e.ProcessingError)       .HasMaxLength(500);
        b.Property(e => e.DeviceLocalTime)       .HasColumnType("timestamptz").IsRequired();
        b.Property(e => e.ReceivedAtUtc)         .HasColumnType("timestamptz").IsRequired();
        b.Property(e => e.RawPayload)            .HasColumnType("text").IsRequired();

        // UNIQUE dedup key: prevents double-processing the same event
        b.HasIndex(e => new { e.DeviceSerialNumber, e.DeviceLocalTime, e.EmployeeCode })
            .IsUnique();

        // Partial index for efficient unprocessed event queries
        b.HasIndex(e => e.IsProcessed)
            .HasFilter("is_processed = false");

        b.HasOne<AttendanceRecord>()
            .WithMany()
            .HasForeignKey(e => e.AttendanceRecordId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
