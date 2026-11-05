using Atlas.Domain.Common;

namespace Atlas.Domain.Audit;

public enum AuditAction
{
    ApplicationCreated,
    ApplicationRead,
    IdentityVerificationPerformed,
    SanctionsScreeningPerformed,
    ManualReviewProcessed,
    BranchVisitRecorded,
    AccountProvisioned,
    ApplicationRejected,
    RightToErasureExercised
}

public class AuditLogEntry : Entity
{
    public Guid ApplicationId { get; private set; }
    public string PerformedBy { get; private set; } = default!;
    public AuditAction Action { get; private set; }
    public string Details { get; private set; } = default!;
    public DateTime RetainUntilUtc { get; private set; }

    private AuditLogEntry() { }

    public AuditLogEntry(Guid applicationId, string performedBy, AuditAction action, string details)
    {
        ApplicationId = applicationId;
        PerformedBy = performedBy;
        Action = action;
        Details = details;
        // 10 years retention under AML compliance (GC-2026-0814 #4)
        RetainUntilUtc = DateTime.UtcNow.AddYears(10);
    }
}
