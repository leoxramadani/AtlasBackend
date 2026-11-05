using Atlas.Application.DTOs;
using Atlas.Domain.Audit;
using Atlas.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Infrastructure.Persistence.Repositories.Audit
{
    public class AuditRepository : IAuditRepository
    {
        private readonly AtlasDbContext _context;

        public AuditRepository(AtlasDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
        {
            await _context.AuditLogs.AddAsync(entry, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AuditLogEntry>> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
        {
            return await _context.AuditLogs
                .Where(l => l.ApplicationId == applicationId)
                .OrderBy(l => l.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<AuditLogDto>> GetAuditLogs(Guid id, CancellationToken cancellationToken)
        {
            return await _context.AuditLogs
                    .Where(l => l.ApplicationId == id)
                    .OrderBy(l => l.CreatedAtUtc)
                    .Select(l => new AuditLogDto
                    {
                        Id = l.Id,
                        ApplicationId = l.ApplicationId,
                        PerformedBy = l.PerformedBy,
                        Action = l.Action.ToString(),
                        Details = l.Details,
                        CreatedAtUtc = l.CreatedAtUtc,
                        RetainUntilUtc = l.RetainUntilUtc
                    })
                    .ToListAsync(cancellationToken);
        }

    }

}
