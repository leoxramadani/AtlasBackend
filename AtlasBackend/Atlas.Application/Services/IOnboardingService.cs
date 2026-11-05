using Atlas.Application.DTOs;

namespace Atlas.Application.Services;

public interface IOnboardingService
{
    Task<ApplicationResponse?> GetApplicationAsync(Guid id, RequestContext context, CancellationToken cancellationToken = default);
    Task<ApplicationResponse> RecordBranchVisitAsync(Guid id, BranchVisitRequest request, RequestContext context, CancellationToken cancellationToken = default);
    Task<bool> RequestErasureAsync(Guid id, RequestContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationResponse>> GetPendingReviewsAsync(RequestContext context, CancellationToken cancellationToken = default);
    Task<ApplicationResponse> ResolveManualReviewAsync(Guid id, ManualReviewRequest request, RequestContext context, CancellationToken cancellationToken = default);


}
