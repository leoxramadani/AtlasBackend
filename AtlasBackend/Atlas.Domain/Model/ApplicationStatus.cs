namespace Atlas.Domain.Model.Applications;

public enum ApplicationStatus
{
    Submitted,
    VerifyingIdentity,
    ScreeningSanctions,
    PendingManualReview,
    PendingBranchVisit,
    Approved,
    Rejected
}
