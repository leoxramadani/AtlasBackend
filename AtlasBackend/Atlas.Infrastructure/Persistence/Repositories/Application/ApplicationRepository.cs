using Atlas.Domain.Model.Applications;
using Atlas.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Infrastructure.Persistence.Repositories.Application;

public class ApplicationRepository : IApplicationRepository
{
    private readonly AtlasDbContext _context;

    public ApplicationRepository(AtlasDbContext context)
    {
        _context = context;
    }

    public async Task<OnboardingApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<OnboardingApplication>> GetPendingManualReviewsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.Documents)
            .Where(a => a.Status == ApplicationStatus.PendingManualReview)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(OnboardingApplication application, CancellationToken cancellationToken = default)
    {
        await _context.Applications.AddAsync(application, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OnboardingApplication application, CancellationToken cancellationToken = default)
    {
        _context.Applications.Update(application);
        await _context.SaveChangesAsync(cancellationToken);
    }
}


