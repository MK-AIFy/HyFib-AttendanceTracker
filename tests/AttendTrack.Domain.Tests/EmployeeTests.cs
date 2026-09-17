using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.ValueObjects;
using FluentAssertions;

namespace AttendTrack.Domain.Tests;

public sealed class EmployeeTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Employee MakeEmployee(string code = "EMP-001") =>
        Employee.Create(
            id:             EmployeeId.New(),
            employeeCode:   code,
            fullName:       "John Doe",
            email:          $"{code}@test.com",
            phone:          "9876543210",
            kioskPin:       PinHash.From("$2a$04$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
            departmentId:   Guid.NewGuid(),
            defaultShiftId: Guid.NewGuid(),
            role:           UserRole.Employee,
            joinedAt:       new DateOnly(2024, 1, 1));

    // ── Test 1: Create sets expected defaults ─────────────────────────────────

    [Fact]
    public void Create_SetsDefaultValues()
    {
        var employee = MakeEmployee("EMP-101");

        employee.EmployeeCode.Should().Be("EMP-101");
        employee.FullName.Should().Be("John Doe");
        employee.IsActive.Should().BeTrue();
        employee.IsBiometricEnrolled.Should().BeFalse();
        employee.DeletedAt.Should().BeNull();
        employee.HikvisionUserId.Should().BeNull();
        employee.FacePhotoPath.Should().BeNull();
        employee.BadgeRfidCard.Should().BeNull();
        employee.Role.Should().Be(UserRole.Employee);
    }

    // ── Test 1b: EmployeeCode is normalized to uppercase ──────────────────────

    [Fact]
    public void Create_LowercaseEmployeeCode_IsStoredUppercase()
    {
        var employee = MakeEmployee("emp-lower");

        employee.EmployeeCode.Should().Be("EMP-LOWER",
            "storage must be case-normalized so the unique index actually enforces " +
            "case-insensitive uniqueness and lookups match regardless of input casing");
    }

    // ── Test 2: SoftDelete sets DeletedAt and deactivates ─────────────────────

    [Fact]
    public void SoftDelete_SetsDeletedAt_AndDeactivatesEmployee()
    {
        var employee = MakeEmployee();

        var before = DateTime.UtcNow;
        employee.SoftDelete();
        var after  = DateTime.UtcNow;

        employee.IsActive.Should().BeFalse();
        employee.DeletedAt.Should().NotBeNull();
        employee.DeletedAt!.Value.Should().BeOnOrAfter(before);
        employee.DeletedAt!.Value.Should().BeOnOrBefore(after);
    }

    // ── Test 3: Double SoftDelete throws DomainException ─────────────────────

    [Fact]
    public void SoftDelete_AlreadyDeleted_ThrowsDomainException()
    {
        var employee = MakeEmployee();
        employee.SoftDelete();

        var act = () => employee.SoftDelete();
        act.Should().Throw<DomainException>()
            .WithMessage("*already soft-deleted*");
    }

    // ── Test 4: MarkBiometricEnrolled sets flag and userId ────────────────────

    [Fact]
    public void MarkBiometricEnrolled_SetsIsBiometricEnrolled_AndHikvisionUserId()
    {
        var employee = MakeEmployee();

        employee.MarkBiometricEnrolled(hikvisionUserId: "HIKUSER-42");

        employee.IsBiometricEnrolled.Should().BeTrue();
        employee.HikvisionUserId.Should().Be("HIKUSER-42");
    }

    // ── Test 5: MarkBiometricEnrolled without userId only sets flag ───────────

    [Fact]
    public void MarkBiometricEnrolled_WithoutUserId_LeavesUserIdNull()
    {
        var employee = MakeEmployee();

        employee.MarkBiometricEnrolled();

        employee.IsBiometricEnrolled.Should().BeTrue();
        employee.HikvisionUserId.Should().BeNull();
    }

    // ── Test 6: Create with empty code throws ─────────────────────────────────

    [Fact]
    public void Create_EmptyEmployeeCode_Throws()
    {
        var act = () => Employee.Create(
            EmployeeId.New(), string.Empty, "Jane", "jane@test.com", "9999",
            PinHash.From("$2a$04$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"),
            Guid.NewGuid(), Guid.NewGuid(), UserRole.Employee,
            new DateOnly(2024, 1, 1));

        act.Should().Throw<ArgumentException>();
    }
}
