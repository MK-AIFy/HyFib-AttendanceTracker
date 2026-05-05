using AttendTrack.Domain.Common;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.ValueObjects;

namespace AttendTrack.Domain.Entities;

/// <summary>
/// Employee aggregate root.
/// Supports Hikvision biometric enrollment, RFID cards, and DPDP soft-delete.
/// EmployeeCode maps 1:1 to Hikvision device employeeNoString field.
/// </summary>
public sealed class Employee : AggregateRoot<EmployeeId>
{
    /// <summary>Maps to Hikvision employeeNoString — e.g. "EMP-001".</summary>
    public string   EmployeeCode          { get; private set; } = default!;
    public string   FullName              { get; private set; } = default!;
    public string   Email                 { get; private set; } = default!;
    public string   Phone                 { get; private set; } = default!;
    /// <summary>BCrypt hash of 6-digit kiosk PIN (fallback auth only).</summary>
    public PinHash  KioskPin              { get; private set; } = default!;
    /// <summary>Hikvision RFID / NFC card number (Mifare 13.56 MHz or EM 125 kHz).</summary>
    public string?  BadgeRfidCard         { get; private set; }
    /// <summary>User ID assigned on the Hikvision device after ISAPI enrollment.</summary>
    public string?  HikvisionUserId       { get; private set; }
    /// <summary>True once face + fingerprint templates have been enrolled on the device.</summary>
    public bool     IsBiometricEnrolled   { get; private set; }
    /// <summary>Path to enrollment JPEG stored locally (NOT biometric template).</summary>
    public string?  FacePhotoPath         { get; private set; }
    public Guid     DepartmentId          { get; private set; }
    public Guid     DefaultShiftId        { get; private set; }
    public UserRole Role                  { get; private set; }
    public bool     IsActive              { get; private set; }
    public DateOnly JoinedAt              { get; private set; }
    /// <summary>Non-null after DPDP soft-delete; global query filter excludes these rows.</summary>
    public DateTime? DeletedAt            { get; private set; }
    public DateTime  CreatedAt            { get; private set; }
    public DateTime  UpdatedAt            { get; private set; }

    private Employee() { }

    public static Employee Create(
        EmployeeId id,
        string     employeeCode,
        string     fullName,
        string     email,
        string     phone,
        PinHash    kioskPin,
        Guid       departmentId,
        Guid       defaultShiftId,
        UserRole   role,
        DateOnly   joinedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new Employee
        {
            Id                  = id,
            EmployeeCode        = employeeCode,
            FullName            = fullName,
            Email               = email,
            Phone               = phone,
            KioskPin            = kioskPin,
            DepartmentId        = departmentId,
            DefaultShiftId      = defaultShiftId,
            Role                = role,
            IsActive            = true,
            JoinedAt            = joinedAt,
            IsBiometricEnrolled = false,
            CreatedAt           = DateTime.UtcNow,
            UpdatedAt           = DateTime.UtcNow
        };
    }

    /// <summary>
    /// DPDP Act 2023 — soft-delete.
    /// Sets DeletedAt; EF Core global query filter excludes soft-deleted rows.
    /// Call DataSubjectService.DeleteDataAsync to hard-purge biometric data.
    /// </summary>
    public void SoftDelete()
    {
        if (DeletedAt.HasValue)
            throw new DomainException("Employee is already soft-deleted.");

        IsActive  = false;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Called after successful ISAPI enrollment on the Hikvision device.</summary>
    public void MarkBiometricEnrolled(string? hikvisionUserId = null)
    {
        IsBiometricEnrolled = true;
        if (hikvisionUserId is not null)
            HikvisionUserId = hikvisionUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateBadgeCard(string? cardNumber)
    {
        BadgeRfidCard = cardNumber;
        UpdatedAt     = DateTime.UtcNow;
    }

    public void SetFacePhotoPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FacePhotoPath = path;
        UpdatedAt     = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive  = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullName, string email, string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        FullName  = fullName;
        Email     = email;
        Phone     = phone;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetPin(PinHash newPin)
    {
        KioskPin  = newPin;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Changes the employee's default shift assignment.</summary>
    public void AssignShift(Guid shiftId)
    {
        DefaultShiftId = shiftId;
        UpdatedAt      = DateTime.UtcNow;
    }
}
