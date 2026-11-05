using Atlas.Domain.Model.Applications;

namespace Atlas.Domain.Repositories;

public interface IApplicationRepository
{
    Task<OnboardingApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OnboardingApplication>> GetPendingManualReviewsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(OnboardingApplication application, CancellationToken cancellationToken = default);
    Task UpdateAsync(OnboardingApplication application, CancellationToken cancellationToken = default);
}

