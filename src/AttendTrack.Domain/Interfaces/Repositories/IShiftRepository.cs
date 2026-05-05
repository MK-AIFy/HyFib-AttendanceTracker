using AttendTrack.Domain.Entities;

namespace AttendTrack.Domain.Interfaces.Repositories;

public interface IShiftRepository
{
    Task<Shift?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Shift>> GetAllActiveAsync(CancellationToken ct = default);
    Task AddAsync(Shift shift, CancellationToken ct = default);
    void Update(Shift shift);
}
