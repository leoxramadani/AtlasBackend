using Atlas.Application.DTOs;
using Atlas.Domain.Audit;

namespace Atlas.Domain.Repositories;

public interface IAuditRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<List<AuditLogDto>> GetAuditLogs(Guid id, CancellationToken cancellationToken);
}
