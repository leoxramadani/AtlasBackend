using Atlas.Domain.Exceptions;
using Atlas.Domain.Model.Applicants;
using Atlas.Domain.Model.Applications;
using Atlas.Domain.Model.Markets;
using Atlas.Domain.Model.Verifications;
using Xunit;

namespace Atlas.Domain.Tests;

public class ApplicationDomainTests
{
    private static Applicant CreateApplicant(
        string nationalId = "1234567890123",
        string? passport = null,
        bool termsAccepted = true)
    {
        return new Applicant(
            firstName: "John",
            lastName: "Doe",
            dateOfBirth: new DateOnly(1990, 1, 1),
            nationalId: nationalId,
            passportNumber: passport,
            email: "john.doe@example.com",
            phone: "+1234567890",
            termsAccepted: termsAccepted
        );
    }

    [Fact]
    public void Create_WithoutTermsAccepted_ThrowsDomainException()
    {
        var applicant = CreateApplicant(termsAccepted: false);
        Assert.Throws<DomainException>(() =>
            OnboardingApplication.Create(MarketCode.MA, applicant, Enumerable.Empty<Document>()));
    }

    [Fact]
    public void Create_MarketMA_InvalidNationalId_ThrowsMarketValidationException()
    {
        var applicant = CreateApplicant(nationalId: "12345"); // Not 13 digits
        Assert.Throws<MarketValidationException>(() =>
            OnboardingApplication.Create(MarketCode.MA, applicant, Enumerable.Empty<Document>()));
    }

    [Fact]
    public void Create_MarketMC_RequiresAlphanumericEndingWithLetter()
    {
        // 9 chars, letter at index 8
        var validApplicant = CreateApplicant(nationalId: "12345678A");
        var app = OnboardingApplication.Create(MarketCode.MC, validApplicant, Enumerable.Empty<Document>());
        Assert.Equal(ApplicationStatus.Submitted, app.Status);

        var invalidApplicant = CreateApplicant(nationalId: "123456789"); // ends with digit
        Assert.Throws<MarketValidationException>(() =>
            OnboardingApplication.Create(MarketCode.MC, invalidApplicant, Enumerable.Empty<Document>()));
    }

    [Fact]
    public void Create_MarketMF_AllowsPassportNumberInLieuOfNationalId()
    {
        var applicantWithPassport = CreateApplicant(nationalId: null!, passport: "P1234567");
        var app = OnboardingApplication.Create(MarketCode.MF, applicantWithPassport, Enumerable.Empty<Document>());
        Assert.Equal(ApplicationStatus.Submitted, app.Status);

        var emptyApplicant = CreateApplicant(nationalId: null!, passport: null);
        Assert.Throws<MarketValidationException>(() =>
            OnboardingApplication.Create(MarketCode.MF, emptyApplicant, Enumerable.Empty<Document>()));
    }

    [Fact]
    public void Flow_IDNowInvalid_TransitionsToRejected()
    {
        var app = OnboardingApplication.Create(MarketCode.MA, CreateApplicant(), Enumerable.Empty<Document>());
        app.StartIdentityVerification();

        app.RecordIdentityVerification(new IdentityVerificationRecord(
            identificationId: "idn_123",
            resultStatus: IdNowResultStatus.Invalid,
            faceMatch: false,
            confidence: 0.2
        ));

        Assert.Equal(ApplicationStatus.Rejected, app.Status);
        Assert.Contains("failed", app.RejectionReason);
    }

    [Fact]
    public void Flow_WorldCheckPossibleMatch_MustGoToManualReview()
    {
        var app = OnboardingApplication.Create(MarketCode.MA, CreateApplicant(), Enumerable.Empty<Document>());
        app.StartIdentityVerification();

        app.RecordIdentityVerification(new IdentityVerificationRecord(
            identificationId: "idn_123",
            resultStatus: IdNowResultStatus.Valid,
            faceMatch: true,
            confidence: 0.98
        ));

        // World-Check returns possible sanctions match
        app.RecordSanctionsScreening(new SanctionsScreeningRecord(
            caseId: "wc_999",
            status: WorldCheckStatus.PossibleMatch,
            matchDetails: "Sanctions list match score 0.85"
        ));

        // Must NOT be auto-rejected or auto-approved
        Assert.Equal(ApplicationStatus.PendingManualReview, app.Status);

        // Compliance Officer approves after review
        app.ResolveManualReview(approved: true, reviewedBy: "officer_alice", notes: "False positive confirmed.");
        Assert.Equal(ApplicationStatus.Approved, app.Status);
        Assert.Equal("officer_alice", app.ComplianceReviewedBy);
    }

    [Fact]
    public void Flow_MarketMD_RequiresInPersonBranchVisitBeforeApproval()
    {
        var applicant = CreateApplicant(nationalId: "1234567890"); // 10 digits for MD
        var app = OnboardingApplication.Create(MarketCode.MD, applicant, Enumerable.Empty<Document>());
        app.StartIdentityVerification();

        app.RecordIdentityVerification(new IdentityVerificationRecord("idn_1", IdNowResultStatus.Valid, true, 0.99));
        app.RecordSanctionsScreening(new SanctionsScreeningRecord("wc_1", WorldCheckStatus.Clear, null));

        // MD requires branch visit before activation!
        Assert.Equal(ApplicationStatus.PendingBranchVisit, app.Status);

        // Applicant visits branch and signs
        app.RecordBranchVisit(branchOfficer: "officer_bob", completedAtUtc: DateTime.UtcNow);
        Assert.Equal(ApplicationStatus.Approved, app.Status);
    }

    [Fact]
    public void ApplyRightToErasure_AnonymizesApplicantData()
    {
        var applicant = CreateApplicant();
        var app = OnboardingApplication.Create(MarketCode.MA, applicant, Enumerable.Empty<Document>());

        app.ApplyRightToErasure();

        Assert.Equal("[ERASED]", app.Applicant.FirstName);
        Assert.Equal("[ERASED]", app.Applicant.LastName);
        Assert.Null(app.Applicant.NationalId);
        Assert.True(app.Applicant.IsErased);
    }
}
