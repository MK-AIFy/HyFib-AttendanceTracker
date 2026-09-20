using AttendTrack.Domain.Common;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;

namespace AttendTrack.Domain.Entities;

public sealed class BreakRecord : AggregateRoot<Guid>
{
    public Guid        AttendanceRecordId { get; private set; }
    public Guid        EmployeeId         { get; private set; }
    public DateTime    StartTime          { get; private set; }  // UTC
    public DateTime?   EndTime            { get; private set; }  // UTC
    public BreakType   Type               { get; private set; }
    public PunchSource StartSource        { get; private set; }
    public PunchSource? EndSource         { get; private set; }

    public TimeSpan Duration  => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
    public bool     IsActive  => EndTime is null;

    private BreakRecord() { }

    public static BreakRecord Create(
        Guid attendanceRecordId,
        Guid employeeId,
        DateTime startTimeUtc,
        BreakType type,
        PunchSource source)
    {
        return new BreakRecord
        {
            Id                 = Guid.NewGuid(),
            AttendanceRecordId = attendanceRecordId,
            EmployeeId         = employeeId,
            StartTime          = startTimeUtc,
            Type               = type,
            StartSource        = source
        };
    }

    public void End(PunchSource source, DateTime endTimeUtc)
    {
        if (EndTime.HasValue)
            throw new DomainException($"Break {Id} is already ended.");

        EndTime   = endTimeUtc;
        EndSource = source;
    }
}
