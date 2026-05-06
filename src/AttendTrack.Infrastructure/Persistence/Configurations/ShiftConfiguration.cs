using AttendTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendTrack.Infrastructure.Persistence.Configurations;

public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> b)
    {
        b.ToTable("Shifts");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedOnAdd();
        b.Property(x => x.Name)                     .HasColumnType("text").IsRequired();
        b.Property(x => x.StartTime)                .HasColumnType("time without time zone").IsRequired();
        b.Property(x => x.EndTime)                  .HasColumnType("time without time zone").IsRequired();
        b.Property(x => x.GracePeriodMinutes)       .HasColumnType("integer").IsRequired();
        b.Property(x => x.OvertimeThresholdMinutes) .HasColumnType("integer").IsRequired();
        b.Property(x => x.IsNightShift)             .HasColumnType("boolean").IsRequired();
        b.Property(x => x.IsActive)                 .HasColumnType("boolean").IsRequired();
        b.Property(x => x.CreatedAt)                .HasColumnType("timestamp with time zone").IsRequired();
    }
}
