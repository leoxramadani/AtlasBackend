namespace Atlas.Application.DTOs;

public record DocumentSubmissionDto(
    string Type,
    string? Image);

public record SubmitApplicationRequest(
    string FirstName,
    string LastName,
    string DateOfBirth,
    string Country,
    string? NationalId,
    string? PassportNumber,
    string Email,
    string Phone,
    List<DocumentSubmissionDto>? Documents,
    bool TermsAccepted);

public record ApplicationResponse(
    Guid ApplicationId,
    string Status,
    string Market,
    string? RejectionReason,
    string? AccountNumber,
    string? CardReference,
    bool RequiresManualReview,
    bool RequiresBranchVisit,
    DateTime CreatedAtUtc);

public record RequestContext(
    string AttributedActor,
    string? IdNowOverride = null,
    string? WorldCheckOverride = null);

public record BranchVisitRequest(
    string OfficerName,
    DateTime? CompletedAtUtc = null);

public record ManualReviewRequest(
    bool Approve,
    string ReviewedBy,
    string Notes);