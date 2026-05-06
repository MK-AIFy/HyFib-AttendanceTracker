using AttendTrack.Application.Commands.Attendance;
using AttendTrack.Application.Commands.Employee;
using AttendTrack.Application.Commands.Hikvision;
using AttendTrack.Application.Commands.Shift;
using FluentValidation;

namespace AttendTrack.Application.Validators;

/// <summary>
/// FluentValidation validators for Application commands.
/// Auto-discovered by AddValidatorsFromAssembly in Application/DependencyInjection.cs.
/// ValidationBehaviour is the outermost MediatR pipeline behaviour, so failures
/// short-circuit the request before logging/audit/handler execute.
/// </summary>
public sealed class CheckInCommandValidator : AbstractValidator<CheckInCommand>
{
    public CheckInCommandValidator()
    {
        RuleFor(x => x.EmployeeCode)
            .NotEmpty().WithMessage("EmployeeCode is required.")
            .MaximumLength(20);

        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("PIN is required.")
            .Length(6).WithMessage("PIN must be exactly 6 digits.")
            .Matches("^[0-9]{6}$").WithMessage("PIN must contain only digits.");
    }
}

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(20)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("EmployeeCode may contain letters, digits, hyphen, underscore.");

        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);

        RuleFor(x => x.Phone).MaximumLength(30);

        RuleFor(x => x.Pin)
            .NotEmpty()
            .Length(6).WithMessage("PIN must be exactly 6 digits.")
            .Matches("^[0-9]{6}$");

        RuleFor(x => x.DepartmentId)  .NotEqual(Guid.Empty);
        RuleFor(x => x.DefaultShiftId).NotEqual(Guid.Empty);
        RuleFor(x => x.Role)          .IsInEnum();
        RuleFor(x => x.JoinedAt)      .NotEqual(default(DateOnly));
    }
}

public sealed class CreateShiftCommandValidator : AbstractValidator<CreateShiftCommand>
{
    public CreateShiftCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StartTime).NotEqual(default(TimeOnly));
        RuleFor(x => x.EndTime)  .NotEqual(default(TimeOnly));
        RuleFor(x => x.GracePeriodMinutes)      .InclusiveBetween(0, 240);
        RuleFor(x => x.OvertimeThresholdMinutes).InclusiveBetween(0, 480);
    }
}

public sealed class RegisterHikvisionDeviceCommandValidator
    : AbstractValidator<RegisterHikvisionDeviceCommand>
{
    public RegisterHikvisionDeviceCommandValidator()
    {
        RuleFor(x => x.DeviceName)   .NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model)        .NotEmpty().MaximumLength(100);
        RuleFor(x => x.SerialNumber) .NotEmpty().MaximumLength(100);
        RuleFor(x => x.IpAddress).NotEmpty()
            .Matches(@"^(\d{1,3}\.){3}\d{1,3}$")
            .WithMessage("IpAddress must be a valid IPv4 dotted-quad.");
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.AdminUsername).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Location)        .MaximumLength(200);
        RuleFor(x => x.FirmwareVersion) .MaximumLength(100);
    }
}

public sealed class EnrollEmployeeToDeviceCommandValidator
    : AbstractValidator<EnrollEmployeeToDeviceCommand>
{
    public EnrollEmployeeToDeviceCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEqual(Guid.Empty);
        RuleFor(x => x.DeviceId)  .NotEqual(Guid.Empty);
        RuleFor(x => x.FacePhotoBytes)
            .NotNull()
            .Must(b => b is { Length: > 0 })
            .WithMessage("FacePhotoBytes must contain image data.")
            // 5 MB upper bound — Hikvision will reject larger uploads anyway.
            .Must(b => b == null || b.Length <= 5 * 1024 * 1024)
            .WithMessage("FacePhotoBytes must be 5 MB or smaller.");
    }
}

public sealed class CheckOutCommandValidator : AbstractValidator<CheckOutCommand>
{
    public CheckOutCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Pin).NotEmpty().Length(6).Matches("^[0-9]{6}$");
    }
}

public sealed class StartBreakCommandValidator : AbstractValidator<StartBreakCommand>
{
    public StartBreakCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Pin).NotEmpty().Length(6).Matches("^[0-9]{6}$");
        RuleFor(x => x.BreakType).IsInEnum();
    }
}

public sealed class EndBreakCommandValidator : AbstractValidator<EndBreakCommand>
{
    public EndBreakCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Pin).NotEmpty().Length(6).Matches("^[0-9]{6}$");
    }
}

public sealed class ExportEmployeeDataCommandValidator
    : AbstractValidator<ExportEmployeeDataCommand>
{
    public ExportEmployeeDataCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEqual(Guid.Empty);
    }
}
