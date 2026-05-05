using AttendTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendTrack.Infrastructure.Persistence.Configurations;

public sealed class HikvisionDeviceConfiguration : IEntityTypeConfiguration<HikvisionDevice>
{
    public void Configure(EntityTypeBuilder<HikvisionDevice> b)
    {
        b.ToTable("hikvision_devices");
        b.HasKey(d => d.Id);

        b.Property(d => d.DeviceName)         .HasMaxLength(100).IsRequired();
        b.Property(d => d.Model)              .HasMaxLength(100).HasDefaultValue("DS-K1T320MFWX");
        b.Property(d => d.SerialNumber)       .HasMaxLength(100).IsRequired();
        b.Property(d => d.IpAddress)          .HasMaxLength(45).IsRequired();
        b.Property(d => d.AdminUsername)      .HasMaxLength(100).IsRequired();
        b.Property(d => d.AdminPasswordHash)  .HasMaxLength(256).IsRequired();
        b.Property(d => d.AdminPasswordProtected).HasMaxLength(512);
        b.Property(d => d.Location)           .HasMaxLength(200);
        b.Property(d => d.FirmwareVersion)    .HasMaxLength(100);
        b.Property(d => d.Port)               .HasDefaultValue(80);
        b.Property(d => d.RegisteredAt)       .HasColumnType("timestamptz");
        b.Property(d => d.LastEventReceivedAt).HasColumnType("timestamptz");
        b.Property(d => d.LastPollAt)         .HasColumnType("timestamptz");

        b.HasIndex(d => d.SerialNumber).IsUnique();
        b.HasIndex(d => d.IpAddress).IsUnique();
    }
}
