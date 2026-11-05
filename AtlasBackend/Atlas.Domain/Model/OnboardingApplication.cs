using Atlas.Domain.Common;
using Atlas.Domain.Exceptions;
using Atlas.Domain.Model.Applicants;
using Atlas.Domain.Model.Markets;
using Atlas.Domain.Model.Verifications;

namespace Atlas.Domain.Model.Applications;

public class OnboardingApplication : AggregateRoot
{
    public MarketCode Market { get; private set; }
    public Applicant Applicant { get; private set; } = default!;
    public ApplicationStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? AccountNumber { get; private set; }
    public string? CardReference { get; private set; }
    public string? ComplianceReviewNotes { get; private set; }
    public string? ComplianceReviewedBy { get; private set; }
    public DateTime? ComplianceReviewedAtUtc { get; private set; }
    public DateTime? BranchVisitCompletedAtUtc { get; private set; }

    public IdentityVerificationRecord? IdentityVerification { get; private set; }
    public SanctionsScreeningRecord? SanctionsScreening { get; private set; }

    private readonly List<Document> _documents = new();
    public IReadOnlyCollection<Document> Documents => _documents.AsReadOnly();

    private OnboardingApplication() { } // EF Core

    public static OnboardingApplication Create(
        MarketCode market,
        Applicant applicant,
        IEnumerable<Document> documents)
    {
        if (!applicant.TermsAccepted)
        {
            throw new DomainException("Terms and conditions must be accepted to submit an application.");
        }

        // Validate identifier using market rules (Annex B)
        var rule = MarketRuleRegistry.GetRule(market);
        rule.ValidateIdentifier(applicant.NationalId, applicant.PassportNumber);

        var app = new OnboardingApplication
        {
            Market = market,
            Applicant = applicant,
            Status = ApplicationStatus.Submitted
        };

        foreach (var doc in documents)
        {
            app._documents.Add(doc);
        }

        return app;
    }

    public void StartIdentityVerification()
    {
        EnsureNotFinished();
        Status = ApplicationStatus.VerifyingIdentity;
    }

    public void RecordIdentityVerification(IdentityVerificationRecord record)
    {
        IdentityVerification = record;

        if (record.ResultStatus != IdNowResultStatus.Valid || !record.FaceMatch)
        {
            Reject($"Identity verification failed: Result={record.ResultStatus}, FaceMatch={record.FaceMatch}");
            return;
        }

        Status = ApplicationStatus.ScreeningSanctions;
    }

    public void RecordSanctionsScreening(SanctionsScreeningRecord record)
    {
        SanctionsScreening = record;

        if (record.Status == WorldCheckStatus.PossibleMatch)
        {
            // Per Compliance Notice GC-2026-0814 #3:
            // "Where screening returns a possible sanctions or PEP match, the application must be referred
            // for manual review by a compliance officer in the relevant market... Under no circumstances
            // may this review be automated or bypassed."
            Status = ApplicationStatus.PendingManualReview;
            return;
        }

        ProceedToAccountActivation();
    }
    private void ProceedToAccountActivation()
    {
        var rule = MarketRuleRegistry.GetRule(Market);
        if (rule.RequiresInPersonActivation && BranchVisitCompletedAtUtc == null)
        {
            // Annex B: Market MD requires in-person branch visit with wet signature before activation
            Status = ApplicationStatus.PendingBranchVisit;
            return;
        }

        // Approved! Account and card can now be provisioned
        Status = ApplicationStatus.Approved;
    }

    public void ProvisionAccountAndCard(string accountNumber, string cardReference)
    {
        if (Status != ApplicationStatus.Approved)
        {
            throw new InvalidApplicationStateException($"Cannot provision account/card unless application is Approved. Current status: {Status}");
        }

        AccountNumber = accountNumber;
        CardReference = cardReference;
    }

    public void Reject(string reason)
    {
        Status = ApplicationStatus.Rejected;
        RejectionReason = reason;
    }

    private void EnsureNotFinished()
    {
        if (Status is ApplicationStatus.Approved or ApplicationStatus.Rejected)
        {
            throw new InvalidApplicationStateException($"Application {Id} is already in a terminal state ({Status}).");
        }
    }
}
