using Atlas.Domain.Model.Applications;

namespace Atlas.Application.DTOs;

public static class ApplicationMappingExtensions
{
    public static ApplicationResponse ToResponse(this OnboardingApplication app)
    {
        return new ApplicationResponse(
            ApplicationId: app.Id,
            Status: app.Status.ToString().ToUpperInvariant(),
            Market: app.Market.ToString(),
            RejectionReason: app.RejectionReason,
            AccountNumber: app.AccountNumber,
            CardReference: app.CardReference,
            RequiresManualReview: app.Status == ApplicationStatus.PendingManualReview,
            RequiresBranchVisit: app.Status == ApplicationStatus.PendingBranchVisit,
            CreatedAtUtc: app.CreatedAtUtc
        );
    }
}

