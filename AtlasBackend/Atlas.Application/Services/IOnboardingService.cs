using Atlas.Application.DTOs;

namespace Atlas.Application.Services;

public interface IOnboardingService
{
    Task<ApplicationResponse> SubmitApplicationAsync(SubmitApplicationRequest request, RequestContext context, CancellationToken cancellationToken = default);
    Task<ApplicationResponse?> GetApplicationAsync(Guid id, RequestContext context, CancellationToken cancellationToken = default);
    Task<ApplicationResponse> RecordBranchVisitAsync(Guid id, BranchVisitRequest request, RequestContext context, CancellationToken cancellationToken = default);
    Task<bool> RequestErasureAsync(Guid id, RequestContext context, CancellationToken cancellationToken = default);


}
