using Atlas.Application.Abstractions;
using Atlas.Domain.Model.Verifications;

namespace Atlas.Infrastructure.Providers;

public class MockIdNowProvider : IIdentityVerificationProvider
{
    public Task<IdentityVerificationRecord> VerifyAsync(IdentityVerificationRequest request, CancellationToken cancellationToken = default)
    {
        var outcome = request.OverrideOutcome?.ToUpperInvariant();

        if (outcome == "TIMEOUT")
        {
            throw new TimeoutException("IDNow provider request timed out after 30000ms.");
        }

        if (outcome == "INVALID")
        {
            return Task.FromResult(new IdentityVerificationRecord(
                identificationId: $"idn_mock_{Guid.NewGuid():N}",
                resultStatus: IdNowResultStatus.Invalid,
                faceMatch: false,
                confidence: 0.12
            ));
        }

        if (outcome == "INCONCLUSIVE")
        {
            return Task.FromResult(new IdentityVerificationRecord(
                identificationId: $"idn_mock_{Guid.NewGuid():N}",
                resultStatus: IdNowResultStatus.Inconclusive,
                faceMatch: true,
                confidence: 0.55
            ));
        }

        // Default: Clean and valid
        return Task.FromResult(new IdentityVerificationRecord(
            identificationId: $"idn_mock_{Guid.NewGuid():N}",
            resultStatus: IdNowResultStatus.Valid,
            faceMatch: true,
            confidence: 0.98
        ));
    }
}

public class MockWorldCheckProvider : ISanctionsScreeningProvider
{
    public Task<SanctionsScreeningRecord> ScreenAsync(SanctionsScreeningRequest request, CancellationToken cancellationToken = default)
    {
        var outcome = request.OverrideOutcome?.ToUpperInvariant();

        if (outcome == "TIMEOUT")
        {
            throw new TimeoutException("Refinitiv World-Check provider request timed out after 30000ms.");
        }

        // Triggered by override header OR specific trigger names in test data
        bool isSanctionsHit = outcome == "POSSIBLE_MATCH"
            || request.FullName.Contains("Sanction", StringComparison.OrdinalIgnoreCase)
            || request.FullName.Contains("PEP", StringComparison.OrdinalIgnoreCase)
            || request.FullName.Contains("Vladimir", StringComparison.OrdinalIgnoreCase);

        if (isSanctionsHit)
        {
            return Task.FromResult(new SanctionsScreeningRecord(
                caseId: $"wc_case_{Guid.NewGuid():N}",
                status: WorldCheckStatus.PossibleMatch,
                matchDetails: "Match found on EU Sanctions List (Score: 0.89, Category: SANCTIONS)"
            ));
        }

        // Default: Clear
        return Task.FromResult(new SanctionsScreeningRecord(
            caseId: $"wc_case_{Guid.NewGuid():N}",
            status: WorldCheckStatus.Clear,
            matchDetails: null
        ));
    }
}

public class MockCoreBankingService : ICoreBankingService
{
    public Task<string> OpenAccountAsync(Guid applicationId, string marketCode, CancellationToken cancellationToken = default)
    {
        var randomSuffix = Random.Shared.Next(10000000, 99999999);
        var iban = $"{marketCode}89ATLAS00{randomSuffix}";
        return Task.FromResult(iban);
    }
}

public class MockCardIssuanceService : ICardIssuanceService
{
    public Task<string> OrderCardAsync(Guid applicationId, string accountNumber, CancellationToken cancellationToken = default)
    {
        var cardRef = $"CRD-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        return Task.FromResult(cardRef);
    }
}
