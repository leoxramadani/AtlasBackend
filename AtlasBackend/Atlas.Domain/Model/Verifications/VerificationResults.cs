namespace Atlas.Domain.Model.Verifications;

public enum IdNowResultStatus
{
    Valid,
    Invalid,
    Inconclusive
}

public class IdentityVerificationRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string IdentificationId { get; private set; } = default!;
    public IdNowResultStatus ResultStatus { get; private set; }
    public bool FaceMatch { get; private set; }
    public double Confidence { get; private set; }
    public DateTime VerifiedAtUtc { get; private set; }

    private IdentityVerificationRecord() { }

    public IdentityVerificationRecord(
        string identificationId,
        IdNowResultStatus resultStatus,
        bool faceMatch,
        double confidence)
    {
        IdentificationId = identificationId;
        ResultStatus = resultStatus;
        FaceMatch = faceMatch;
        Confidence = confidence;
        VerifiedAtUtc = DateTime.UtcNow;
    }
}

public enum WorldCheckStatus
{
    Clear,
    PossibleMatch
}

public class SanctionsScreeningRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string CaseId { get; private set; } = default!;
    public WorldCheckStatus Status { get; private set; }
    public string? MatchDetails { get; private set; }
    public DateTime ScreenedAtUtc { get; private set; }

    private SanctionsScreeningRecord() { }

    public SanctionsScreeningRecord(
        string caseId,
        WorldCheckStatus status,
        string? matchDetails)
    {
        CaseId = caseId;
        Status = status;
        MatchDetails = matchDetails;
        ScreenedAtUtc = DateTime.UtcNow;
    }
}
