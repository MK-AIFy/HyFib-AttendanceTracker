using AttendTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendTrack.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedOnAdd();
        b.Property(x => x.Name)       .HasColumnType("text").IsRequired();
        b.Property(x => x.Description).HasColumnType("text");
        b.Property(x => x.IsActive)   .HasColumnType("boolean").IsRequired();
        b.Property(x => x.CreatedAt)  .HasColumnType("timestamp with time zone").IsRequired();
    }
}
