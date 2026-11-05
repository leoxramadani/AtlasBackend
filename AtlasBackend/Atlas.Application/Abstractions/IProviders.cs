using Atlas.Domain.Model.Applicants;
using Atlas.Domain.Model.Verifications;

namespace Atlas.Application.Abstractions;

public record IdentityVerificationRequest(
    DocumentType DocumentType,
    string? DocumentImageBase64,
    string? SelfieImageBase64,
    string? OverrideOutcome = null);

public interface IIdentityVerificationProvider
{
    Task<IdentityVerificationRecord> VerifyAsync(IdentityVerificationRequest request, CancellationToken cancellationToken = default);
}

public record SanctionsScreeningRequest(
    string FullName,
    DateOnly DateOfBirth,
    string Nationality,
    string? OverrideOutcome = null);

public interface ISanctionsScreeningProvider
{
    Task<SanctionsScreeningRecord> ScreenAsync(SanctionsScreeningRequest request, CancellationToken cancellationToken = default);
}

public interface ICoreBankingService
{
    Task<string> OpenAccountAsync(Guid applicationId, string marketCode, CancellationToken cancellationToken = default);
}

public interface ICardIssuanceService
{
    Task<string> OrderCardAsync(Guid applicationId, string accountNumber, CancellationToken cancellationToken = default);
}
