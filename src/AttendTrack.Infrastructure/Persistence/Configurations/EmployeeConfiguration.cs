using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendTrack.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("employees");
        b.HasKey(e => e.Id);

        // [Gap 6] DPDP soft-delete: global query filter excludes soft-deleted employees
        b.HasQueryFilter(e => e.DeletedAt == null);

        // EmployeeId value object — stored as UUID
        b.Property(e => e.Id)
            .HasConversion(
                id    => id.Value,
                value => EmployeeId.From(value))
            .HasColumnName("id");

        // KioskPin value object — stored as BCrypt hash string
        b.Property(e => e.KioskPin)
            .HasConversion(
                pin   => pin.Value,
                value => PinHash.From(value))
            .HasColumnName("kiosk_pin_hash")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(e => e.EmployeeCode).HasMaxLength(20) .IsRequired();
        b.Property(e => e.FullName)    .HasMaxLength(200).IsRequired();
        b.Property(e => e.Email)       .HasMaxLength(200).IsRequired();
        b.Property(e => e.Phone)       .HasMaxLength(30);
        b.Property(e => e.BadgeRfidCard)   .HasMaxLength(100);
        b.Property(e => e.HikvisionUserId) .HasMaxLength(50);
        b.Property(e => e.FacePhotoPath)   .HasMaxLength(500);

        b.Property(e => e.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        b.Property(e => e.CreatedAt).HasColumnType("timestamptz").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnType("timestamptz").IsRequired();
        b.Property(e => e.DeletedAt).HasColumnType("timestamptz");

        b.HasIndex(e => e.EmployeeCode).IsUnique();
        b.HasIndex(e => e.Email)       .IsUnique();
    }
}

