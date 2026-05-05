using System.Text;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using AttendTrack.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Commands.Employee;

/// <summary>
/// DPDP Act 2023 Gap 6 — exports all personal data for a data subject.
/// Returns UTF-8 JSON bytes for download by the employee.
/// </summary>
public sealed record ExportEmployeeDataCommand(
    Guid EmployeeId) : IRequest<byte[]>;

public sealed class ExportEmployeeDataHandler : IRequestHandler<ExportEmployeeDataCommand, byte[]>
{
    private readonly IDataSubjectService   _dataSubjectService;
    private readonly IEmployeeRepository   _empRepo;
    private readonly ILogger<ExportEmployeeDataHandler> _logger;

    public ExportEmployeeDataHandler(
        IDataSubjectService                  dataSubjectService,
        IEmployeeRepository                  empRepo,
        ILogger<ExportEmployeeDataHandler>   logger)
    {
        _dataSubjectService = dataSubjectService;
        _empRepo            = empRepo;
        _logger             = logger;
    }

    public async Task<byte[]> Handle(ExportEmployeeDataCommand cmd, CancellationToken ct)
    {
        var employee = await _empRepo.GetByIdAsync(
            EmployeeId.From(cmd.EmployeeId), ct).ConfigureAwait(false)
            ?? throw new DomainException($"Employee {cmd.EmployeeId} not found.");

        // IDataSubjectService returns pre-serialised JSON string
        var json = await _dataSubjectService
            .ExportDataAsync(cmd.EmployeeId, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "DPDP data export for employee {Code} ({Id})",
            employee.EmployeeCode, cmd.EmployeeId);

        return Encoding.UTF8.GetBytes(json);
    }
}
